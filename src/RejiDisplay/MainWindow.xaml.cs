using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using RejiDisplay.Helpers;
using RejiDisplay.Models;
using RejiDisplay.Services;

namespace RejiDisplay
{
    public partial class MainWindow : Window
    {
        private readonly DisplayService _displayService;
        private readonly SettingsService _settingsService;
        private readonly OutputManager _outputManager;
        private readonly VenuePresetService _venuePresetService;

        private List<DisplayInfo> _allDisplays = new();
        private DisplayInfo? _reservedCenterDisplay;

        private OutputCardState _leftState = new() { CardId = "LEFT", Title = "LEFT LED" };
        private OutputCardState _rightState = new() { CardId = "RIGHT", Title = "RIGHT LED" };

        private AppSettings _appSettings = new();
        private bool _isInitializing = true;
        private bool _isUpdatingUI = false;
        private readonly System.Windows.Threading.DispatcherTimer _videoTimer;
        private readonly Dictionary<string, BitmapImage> _bitmapCache = new(StringComparer.OrdinalIgnoreCase);

        public MainWindow()
        {
            _isInitializing = true;
            _displayService = new DisplayService();
            _settingsService = new SettingsService();
            _outputManager = new OutputManager();
            _venuePresetService = new VenuePresetService();

            InitializeComponent();

            AppLogger.LogOccurred += (sender, args) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (args.Level == LogLevel.Error && TxtGlobalStatus != null)
                    {
                        TxtGlobalStatus.Text = $"HATA: {args.Message}";
                    }
                });
            };

            _videoTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _videoTimer.Tick += VideoTimer_Tick;
            _videoTimer.Start();

            _displayService.DisplayTopologyChanged += OnDisplayTopologyChanged;

            Loaded += MainWindow_Loaded;
            Unloaded += MainWindow_Unloaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isUpdatingUI = true;
            try
            {
                _appSettings = _settingsService.LoadSettings();
                PopulateVenuePresets();
                RefreshDisplaysAndUI();
                RestoreSavedSettings();
                TestPatternGenerator.SafeCleanUpStalePatterns(_leftState.DraftLayout.MediaPath, _rightState.DraftLayout.MediaPath);
            }
            finally
            {
                _isUpdatingUI = false;
                _isInitializing = false;
            }

            RenderDraftPreview("LEFT");
            RenderDraftPreview("RIGHT");
        }

        private void MainWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            _settingsService.FlushPendingSave();
            _outputManager.StopAllOutputs();
        }

        private void OnDisplayTopologyChanged(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                TxtGlobalStatus.Text = "Ekran yapısı değişti. Ekranlar taranıyor...";
                RefreshDisplaysAndUI();
                VerifyActiveOutputsAfterTopologyChange();
            });
        }

        private void PopulateVenuePresets()
        {
            CmbVenuePresets.SelectionChanged -= CmbVenuePresets_SelectionChanged;
            CmbVenuePresets.Items.Clear();

            var presets = _venuePresetService.GetPresets();
            foreach (var preset in presets)
            {
                CmbVenuePresets.Items.Add(new ComboBoxItem
                {
                    Content = preset.Name,
                    Tag = preset
                });
            }

            var selected = _venuePresetService.GetPresetByName(_appSettings.SelectedVenuePresetName) ?? VenuePreset.NovaStarVX2000ProStandard;
            foreach (ComboBoxItem item in CmbVenuePresets.Items)
            {
                if (item.Tag is VenuePreset vp && string.Equals(vp.Name, selected.Name, StringComparison.OrdinalIgnoreCase))
                {
                    CmbVenuePresets.SelectedItem = item;
                    break;
                }
            }

            CmbVenuePresets.SelectionChanged += CmbVenuePresets_SelectionChanged;
        }

        private void RefreshDisplaysAndUI()
        {
            _allDisplays = _displayService.GetDisplays();

            CmbReservedCenter.SelectionChanged -= CmbReservedCenter_SelectionChanged;
            CmbReservedCenter.Items.Clear();

            CmbReservedCenter.Items.Add(new ComboBoxItem { Content = "-- Seçilmedi / Varsayılan --", Tag = null });

            foreach (var display in _allDisplays)
            {
                CmbReservedCenter.Items.Add(new ComboBoxItem
                {
                    Content = display.DisplayLabel,
                    Tag = display
                });
            }

            if (!string.IsNullOrEmpty(_appSettings.ReservedCenterDeviceName) || !string.IsNullOrEmpty(_appSettings.ReservedCenterDeviceId))
            {
                _reservedCenterDisplay = _displayService.FindMatchingDisplay(_allDisplays, _appSettings.ReservedCenterDeviceName, _appSettings.ReservedCenterDeviceId);
            }

            if (_reservedCenterDisplay != null)
            {
                foreach (ComboBoxItem item in CmbReservedCenter.Items)
                {
                    if (item.Tag is DisplayInfo d && _displayService.IsSameDisplay(d, _reservedCenterDisplay))
                    {
                        CmbReservedCenter.SelectedItem = item;
                        break;
                    }
                }
            }
            else
            {
                CmbReservedCenter.SelectedIndex = 0;
            }

            CmbReservedCenter.SelectionChanged += CmbReservedCenter_SelectionChanged;

            UpdateCardComboBoxes();
        }

        private void UpdateCardComboBoxes()
        {
            DisplayInfo? leftSelected = GetSelectedDisplayFromCombo(CmbLeftDisplay);
            DisplayInfo? rightSelected = GetSelectedDisplayFromCombo(CmbRightDisplay);

            PopulateCombo(CmbLeftDisplay, leftSelected, _reservedCenterDisplay, rightSelected, CmbLeftDisplay_SelectionChanged);
            PopulateCombo(CmbRightDisplay, rightSelected, _reservedCenterDisplay, leftSelected, CmbRightDisplay_SelectionChanged);
        }

        private void PopulateCombo(
            ComboBox combo,
            DisplayInfo? currentlySelected,
            DisplayInfo? reservedCenter,
            DisplayInfo? claimedByOther,
            SelectionChangedEventHandler handler)
        {
            combo.SelectionChanged -= handler;
            combo.Items.Clear();

            combo.Items.Add(new ComboBoxItem { Content = "-- Ekran Seçin --", Tag = null });

            var assignable = _displayService.GetAssignableDisplays(_allDisplays, reservedCenter, claimedByOther);

            ComboBoxItem? itemToSelect = null;

            foreach (var display in assignable)
            {
                var item = new ComboBoxItem
                {
                    Content = display.FriendlyName,
                    Tag = display
                };

                combo.Items.Add(item);

                if (currentlySelected != null && _displayService.IsSameDisplay(display, currentlySelected))
                {
                    itemToSelect = item;
                }
            }

            if (itemToSelect != null)
            {
                combo.SelectedItem = itemToSelect;
            }
            else
            {
                combo.SelectedIndex = 0;
            }

            combo.SelectionChanged += handler;
        }

        private DisplayInfo? GetSelectedDisplayFromCombo(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is DisplayInfo display)
            {
                return display;
            }
            return null;
        }

        private void RestoreSavedSettings()
        {
            // Restore LEFT Card
            if (_appSettings.LeftOutput != null)
            {
                _leftState.DraftCalibration = _appSettings.LeftOutput.Calibration ?? new OutputCalibration();
                _leftState.LiveAppliedCalibration = _leftState.DraftCalibration.Clone();
                _leftState.DraftLayout = _appSettings.LeftOutput.DraftLayout ?? new ImageLayoutState();
                _leftState.LiveAppliedLayout = _appSettings.LeftOutput.LiveAppliedLayout ?? new ImageLayoutState();
                _leftState.IsLiveUpdateEnabled = _appSettings.LeftOutput.IsLiveUpdateEnabled;
                _leftState.ContentBank = _appSettings.LeftOutput.ContentBank ?? new List<ContentBankItem>();

                var match = _displayService.FindMatchingDisplay(_allDisplays, _appSettings.LeftOutput.DeviceName, _appSettings.LeftOutput.DeviceId);
                if (match != null && !match.IsPrimary && (_reservedCenterDisplay == null || !_displayService.IsSameDisplay(match, _reservedCenterDisplay)))
                {
                    SelectDisplayInCombo(CmbLeftDisplay, match);
                    _leftState.AssignedDeviceName = match.DeviceName;
                    _leftState.AssignedDeviceId = match.DeviceId;
                }
                else if (!string.IsNullOrEmpty(_appSettings.LeftOutput.DeviceName))
                {
                    SetCardStatus(_leftState, BadgeLeftStatus, TxtLeftStatus, "DISCONNECTED", Colors.OrangeRed);
                    TxtLeftError.Text = "Kayıtlı ekran bulunamadı. Atama yapılması gerekiyor.";
                    TxtLeftError.Visibility = Visibility.Visible;
                }

                SyncCardStateToUI("LEFT", _leftState);
                UpdateLiveBannerUI("LEFT");
                RenderContentBankUI("LEFT");

                if (!string.IsNullOrEmpty(_leftState.DraftLayout.MediaPath))
                {
                    LoadMediaForCard("LEFT", _leftState, _leftState.DraftLayout.MediaPath, ImgLeftPreview, MediaLeftPreviewVideo, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, PanelLeftVideoControls);
                }
            }

            // Restore RIGHT Card
            if (_appSettings.RightOutput != null)
            {
                _rightState.DraftCalibration = _appSettings.RightOutput.Calibration ?? new OutputCalibration();
                _rightState.LiveAppliedCalibration = _rightState.DraftCalibration.Clone();
                _rightState.DraftLayout = _appSettings.RightOutput.DraftLayout ?? new ImageLayoutState();
                _rightState.LiveAppliedLayout = _appSettings.RightOutput.LiveAppliedLayout ?? new ImageLayoutState();
                _rightState.IsLiveUpdateEnabled = _appSettings.RightOutput.IsLiveUpdateEnabled;
                _rightState.ContentBank = _appSettings.RightOutput.ContentBank ?? new List<ContentBankItem>();

                var match = _displayService.FindMatchingDisplay(_allDisplays, _appSettings.RightOutput.DeviceName, _appSettings.RightOutput.DeviceId);
                if (match != null && !match.IsPrimary && (_reservedCenterDisplay == null || !_displayService.IsSameDisplay(match, _reservedCenterDisplay)))
                {
                    SelectDisplayInCombo(CmbRightDisplay, match);
                    _rightState.AssignedDeviceName = match.DeviceName;
                    _rightState.AssignedDeviceId = match.DeviceId;
                }
                else if (!string.IsNullOrEmpty(_appSettings.RightOutput.DeviceName))
                {
                    SetCardStatus(_rightState, BadgeRightStatus, TxtRightStatus, "DISCONNECTED", Colors.OrangeRed);
                    TxtRightError.Text = "Kayıtlı ekran bulunamadı. Atama yapılması gerekiyor.";
                    TxtRightError.Visibility = Visibility.Visible;
                }

                SyncCardStateToUI("RIGHT", _rightState);
                UpdateLiveBannerUI("RIGHT");
                RenderContentBankUI("RIGHT");

                if (!string.IsNullOrEmpty(_rightState.DraftLayout.MediaPath))
                {
                    LoadMediaForCard("RIGHT", _rightState, _rightState.DraftLayout.MediaPath, ImgRightPreview, MediaRightPreviewVideo, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, PanelRightVideoControls);
                }
            }
        }

        private void SyncCardStateToUI(string cardId, OutputCardState state)
        {
            _isUpdatingUI = true;
            try
            {
                if (cardId == "LEFT")
                {
                    RadioLeftFit.IsChecked = (state.DraftLayout.ScaleMode == ScaleMode.Fit);
                    RadioLeftFill.IsChecked = (state.DraftLayout.ScaleMode == ScaleMode.Fill);
                    RadioLeftStretch.IsChecked = (state.DraftLayout.ScaleMode == ScaleMode.Stretch);
                    RadioLeftCustom.IsChecked = (state.DraftLayout.ScaleMode == ScaleMode.Custom);

                    SliderLeftZoom.Value = state.DraftLayout.Zoom * 100.0;
                    TxtLeftZoom.Text = $"{(int)(state.DraftLayout.Zoom * 100.0)}%";

                    SliderLeftOffsetX.Value = state.DraftLayout.OffsetX;
                    TxtLeftOffsetX.Text = $"{(int)state.DraftLayout.OffsetX} px";

                    SliderLeftOffsetY.Value = state.DraftLayout.OffsetY;
                    TxtLeftOffsetY.Text = $"{(int)state.DraftLayout.OffsetY} px";

                    ChkLeftLiveSync.IsChecked = state.IsLiveUpdateEnabled;

                    TxtLeftLedW.Text = state.Calibration.LogicalLedWidth.ToString();
                    TxtLeftLedH.Text = state.Calibration.LogicalLedHeight.ToString();
                    TxtLeftVpX.Text = state.Calibration.ViewportX.ToString();
                    TxtLeftVpY.Text = state.Calibration.ViewportY.ToString();

                    bool isVideo = (state.DraftLayout.MediaSource.Type == MediaSourceType.Video);
                    bool isWeb = (state.DraftLayout.MediaSource.Type == MediaSourceType.Website);

                    PanelLeftVideoControls.Visibility = isVideo ? Visibility.Visible : Visibility.Collapsed;
                    PanelLeftWebControls.Visibility = isWeb ? Visibility.Visible : Visibility.Collapsed;
                    if (isWeb)
                    {
                        TxtLeftWebUrl.Text = state.DraftLayout.WebsiteState.Url;
                        ChkLeftWebMute.IsChecked = state.DraftLayout.WebsiteState.IsMuted;
                    }

                    ChkLeftLoop.IsChecked = state.DraftLayout.VideoState.IsLooping;
                    ChkLeftMute.IsChecked = state.DraftLayout.VideoState.IsMuted;
                    SliderLeftVolume.Value = state.DraftLayout.VideoState.Volume * 100.0;
                    TxtLeftVolume.Text = $"{(int)(state.DraftLayout.VideoState.Volume * 100.0)}%";
                }
                else
                {
                    RadioRightFit.IsChecked = (state.DraftLayout.ScaleMode == ScaleMode.Fit);
                    RadioRightFill.IsChecked = (state.DraftLayout.ScaleMode == ScaleMode.Fill);
                    RadioRightStretch.IsChecked = (state.DraftLayout.ScaleMode == ScaleMode.Stretch);
                    RadioRightCustom.IsChecked = (state.DraftLayout.ScaleMode == ScaleMode.Custom);

                    SliderRightZoom.Value = state.DraftLayout.Zoom * 100.0;
                    TxtRightZoom.Text = $"{(int)(state.DraftLayout.Zoom * 100.0)}%";

                    SliderRightOffsetX.Value = state.DraftLayout.OffsetX;
                    TxtRightOffsetX.Text = $"{(int)state.DraftLayout.OffsetX} px";

                    SliderRightOffsetY.Value = state.DraftLayout.OffsetY;
                    TxtRightOffsetY.Text = $"{(int)state.DraftLayout.OffsetY} px";

                    ChkRightLiveSync.IsChecked = state.IsLiveUpdateEnabled;

                    TxtRightLedW.Text = state.Calibration.LogicalLedWidth.ToString();
                    TxtRightLedH.Text = state.Calibration.LogicalLedHeight.ToString();
                    TxtRightVpX.Text = state.Calibration.ViewportX.ToString();
                    TxtRightVpY.Text = state.Calibration.ViewportY.ToString();

                    bool isVideo = (state.DraftLayout.MediaSource.Type == MediaSourceType.Video);
                    bool isWeb = (state.DraftLayout.MediaSource.Type == MediaSourceType.Website);

                    PanelRightVideoControls.Visibility = isVideo ? Visibility.Visible : Visibility.Collapsed;
                    PanelRightWebControls.Visibility = isWeb ? Visibility.Visible : Visibility.Collapsed;
                    if (isWeb)
                    {
                        TxtRightWebUrl.Text = state.DraftLayout.WebsiteState.Url;
                        ChkRightWebMute.IsChecked = state.DraftLayout.WebsiteState.IsMuted;
                    }

                    ChkRightLoop.IsChecked = state.DraftLayout.VideoState.IsLooping;
                    ChkRightMute.IsChecked = state.DraftLayout.VideoState.IsMuted;
                    SliderRightVolume.Value = state.DraftLayout.VideoState.Volume * 100.0;
                    TxtRightVolume.Text = $"{(int)(state.DraftLayout.VideoState.Volume * 100.0)}%";
                }
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

        private void SelectDisplayInCombo(ComboBox combo, DisplayInfo target)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag is DisplayInfo d && _displayService.IsSameDisplay(d, target))
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        private void VerifyActiveOutputsAfterTopologyChange()
        {
            if (_outputManager.IsOutputActive("LEFT"))
            {
                var activeDisplay = _outputManager.GetActiveDisplay("LEFT");
                if (activeDisplay != null)
                {
                    var current = _displayService.FindMatchingDisplay(_allDisplays, activeDisplay.DeviceName, activeDisplay.DeviceId);
                    if (current == null)
                    {
                        _outputManager.StopOutput("LEFT");
                        SetCardStatus(_leftState, BadgeLeftStatus, TxtLeftStatus, "DISCONNECTED", Colors.Red);
                        TxtLeftError.Text = "Atanan fiziksel ekran bağlantısı kesildi! Çıkış durduruldu.";
                        TxtLeftError.Visibility = Visibility.Visible;
                    }
                }
            }

            if (_outputManager.IsOutputActive("RIGHT"))
            {
                var activeDisplay = _outputManager.GetActiveDisplay("RIGHT");
                if (activeDisplay != null)
                {
                    var current = _displayService.FindMatchingDisplay(_allDisplays, activeDisplay.DeviceName, activeDisplay.DeviceId);
                    if (current == null)
                    {
                        _outputManager.StopOutput("RIGHT");
                        SetCardStatus(_rightState, BadgeRightStatus, TxtRightStatus, "DISCONNECTED", Colors.Red);
                        TxtRightError.Text = "Atanan fiziksel ekran bağlantısı kesildi! Çıkış durduruldu.";
                        TxtRightError.Visibility = Visibility.Visible;
                    }
                }
            }

            UpdateCardComboBoxes();
        }

        // --- Layout Preview Matrix Transform ---

        private void RenderDraftPreview(string cardId)
        {
            var cardState = (cardId == "LEFT") ? _leftState : _rightState;
            var previewImg = (cardId == "LEFT") ? ImgLeftPreview : ImgRightPreview;
            var previewVideo = (cardId == "LEFT") ? MediaLeftPreviewVideo : MediaRightPreviewVideo;
            var webPreviewPrompt = (cardId == "LEFT") ? PanelLeftWebPreviewPrompt : PanelRightWebPreviewPrompt;
            var webPreviewHostTxt = (cardId == "LEFT") ? TxtLeftWebPreviewHost : TxtRightWebPreviewHost;
            var previewCanvas = (cardId == "LEFT") ? CanvasLeftPreviewViewport : CanvasRightPreviewViewport;

            if (previewCanvas == null) return;

            // Constrain viewport box in control panel to 140x170 px
            double maxW = 140;
            double maxH = 170;

            double viewportW = maxW;
            double viewportH = maxH;

            if (cardState.Calibration.LogicalLedWidth > 0 && cardState.Calibration.LogicalLedHeight > 0)
            {
                double aspect = (double)cardState.Calibration.LogicalLedWidth / cardState.Calibration.LogicalLedHeight;
                if (aspect > 1.0)
                {
                    viewportH = maxW / aspect;
                }
                else
                {
                    viewportW = maxH * aspect;
                }
            }

            previewCanvas.Width = viewportW;
            previewCanvas.Height = viewportH;

            if (cardState.DraftLayout.MediaSource.Type == MediaSourceType.Website)
            {
                if (previewImg != null) previewImg.Visibility = Visibility.Collapsed;
                if (previewVideo != null) previewVideo.Visibility = Visibility.Collapsed;

                if (webPreviewPrompt != null)
                {
                    webPreviewPrompt.Visibility = Visibility.Visible;
                    webPreviewPrompt.Width = Math.Max(40, viewportW - 10);
                    webPreviewPrompt.Height = Math.Max(40, viewportH - 10);
                    Canvas.SetLeft(webPreviewPrompt, 5);
                    Canvas.SetTop(webPreviewPrompt, 5);

                    if (webPreviewHostTxt != null)
                    {
                        webPreviewHostTxt.Text = cardState.DraftLayout.MediaSource.DisplayName;
                    }
                }
            }
            else if (cardState.DraftLayout.MediaSource.Type == MediaSourceType.Video && previewVideo != null && previewVideo.Visibility == Visibility.Visible)
            {
                if (webPreviewPrompt != null) webPreviewPrompt.Visibility = Visibility.Collapsed;
                double vidW = previewVideo.NaturalVideoWidth > 0 ? previewVideo.NaturalVideoWidth : 1920;
                double vidH = previewVideo.NaturalVideoHeight > 0 ? previewVideo.NaturalVideoHeight : 1080;

                var rect = LayoutTransformHelper.CalculateLayoutRect(
                    vidW,
                    vidH,
                    viewportW,
                    viewportH,
                    cardState.DraftLayout);

                previewVideo.Width = rect.Width;
                previewVideo.Height = rect.Height;
                Canvas.SetLeft(previewVideo, rect.Left);
                Canvas.SetTop(previewVideo, rect.Top);
            }
            else if (previewImg != null && previewImg.Source is BitmapImage bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
            {
                if (webPreviewPrompt != null) webPreviewPrompt.Visibility = Visibility.Collapsed;
                var rect = LayoutTransformHelper.CalculateLayoutRect(
                    bitmap.PixelWidth,
                    bitmap.PixelHeight,
                    viewportW,
                    viewportH,
                    cardState.DraftLayout);

                previewImg.Width = rect.Width;
                previewImg.Height = rect.Height;
                Canvas.SetLeft(previewImg, rect.Left);
                Canvas.SetTop(previewImg, rect.Top);
                previewImg.RenderTransform = Transform.Identity;
            }
        }

        private void MediaLeftPreviewVideo_MediaOpened(object sender, RoutedEventArgs e)
        {
            RenderDraftPreview("LEFT");
        }

        private void MediaRightPreviewVideo_MediaOpened(object sender, RoutedEventArgs e)
        {
            RenderDraftPreview("RIGHT");
        }

        // --- Event Handlers ---

        private void CmbVenuePresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || CmbVenuePresets == null) return;

            if (CmbVenuePresets.SelectedItem is ComboBoxItem item && item.Tag is VenuePreset preset)
            {
                _appSettings.SelectedVenuePresetName = preset.Name;

                _leftState.DraftCalibration.LogicalLedWidth = preset.DefaultLeftLogicalWidth;
                _leftState.DraftCalibration.LogicalLedHeight = preset.DefaultLeftLogicalHeight;

                _rightState.DraftCalibration.LogicalLedWidth = preset.DefaultRightLogicalWidth;
                _rightState.DraftCalibration.LogicalLedHeight = preset.DefaultRightLogicalHeight;

                SyncCardStateToUI("LEFT", _leftState);
                SyncCardStateToUI("RIGHT", _rightState);

                if (!string.IsNullOrEmpty(_leftState.DraftLayout.MediaPath) && _leftState.DraftLayout.MediaPath.Contains("TestPattern_"))
                {
                    GenerateAndApplyTestPattern("LEFT", _leftState, ImgLeftPreview, MediaLeftPreviewVideo, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, BtnLeftRestoreMedia, PanelLeftVideoControls);
                }
                else
                {
                    RenderDraftPreview("LEFT");
                }

                if (!string.IsNullOrEmpty(_rightState.DraftLayout.MediaPath) && _rightState.DraftLayout.MediaPath.Contains("TestPattern_"))
                {
                    GenerateAndApplyTestPattern("RIGHT", _rightState, ImgRightPreview, MediaRightPreviewVideo, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, BtnRightRestoreMedia, PanelRightVideoControls);
                }
                else
                {
                    RenderDraftPreview("RIGHT");
                }

                SaveAppSettings();

                if (_leftState.IsLiveUpdateEnabled && _outputManager.IsOutputActive("LEFT")) ApplyDraftToLive("LEFT");
                if (_rightState.IsLiveUpdateEnabled && _outputManager.IsOutputActive("RIGHT")) ApplyDraftToLive("RIGHT");

                TxtGlobalStatus.Text = $"Venue Preset seçildi: {preset.Name}";
            }
        }

        private void CmbReservedCenter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || CmbReservedCenter == null || _settingsService == null || _appSettings == null) return;

            if (CmbReservedCenter.SelectedItem is ComboBoxItem item && item.Tag is DisplayInfo display)
            {
                _reservedCenterDisplay = display;
                _appSettings.ReservedCenterDeviceName = display.DeviceName;
                _appSettings.ReservedCenterDeviceId = display.DeviceId;
            }
            else
            {
                _reservedCenterDisplay = null;
                _appSettings.ReservedCenterDeviceName = null;
                _appSettings.ReservedCenterDeviceId = null;
            }

            SaveAppSettings();
            UpdateCardComboBoxes();
        }

        private void CmbLeftDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || CmbLeftDisplay == null || _settingsService == null || _appSettings?.LeftOutput == null || _leftState == null) return;

            var selected = GetSelectedDisplayFromCombo(CmbLeftDisplay);
            if (selected != null)
            {
                _leftState.AssignedDeviceName = selected.DeviceName;
                _leftState.AssignedDeviceId = selected.DeviceId;
                _appSettings.LeftOutput.DeviceName = selected.DeviceName;
                _appSettings.LeftOutput.DeviceId = selected.DeviceId;

                _leftState.Calibration.ValidateAndClamp(selected.Width, selected.Height);
                TxtLeftError.Visibility = Visibility.Collapsed;
            }
            else
            {
                _leftState.AssignedDeviceName = null;
                _leftState.AssignedDeviceId = null;
                _appSettings.LeftOutput.DeviceName = null;
                _appSettings.LeftOutput.DeviceId = null;
            }

            SaveAppSettings();
            UpdateCardComboBoxes();
        }

        private void CmbRightDisplay_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || CmbRightDisplay == null || _settingsService == null || _appSettings?.RightOutput == null || _rightState == null) return;

            var selected = GetSelectedDisplayFromCombo(CmbRightDisplay);
            if (selected != null)
            {
                _rightState.AssignedDeviceName = selected.DeviceName;
                _rightState.AssignedDeviceId = selected.DeviceId;
                _appSettings.RightOutput.DeviceName = selected.DeviceName;
                _appSettings.RightOutput.DeviceId = selected.DeviceId;

                _rightState.Calibration.ValidateAndClamp(selected.Width, selected.Height);
                TxtRightError.Visibility = Visibility.Collapsed;
            }
            else
            {
                _rightState.AssignedDeviceName = null;
                _rightState.AssignedDeviceId = null;
                _appSettings.RightOutput.DeviceName = null;
                _appSettings.RightOutput.DeviceId = null;
            }

            SaveAppSettings();
            UpdateCardComboBoxes();
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DropZoneLeft_Drop(object sender, DragEventArgs e)
        {
            HandleFileDrop("LEFT", _leftState, e, ImgLeftPreview, MediaLeftPreviewVideo, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, PanelLeftVideoControls);
        }

        private void DropZoneRight_Drop(object sender, DragEventArgs e)
        {
            HandleFileDrop("RIGHT", _rightState, e, ImgRightPreview, MediaRightPreviewVideo, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, PanelRightVideoControls);
        }

        private void BtnLeftChooseMedia_Click(object sender, RoutedEventArgs e)
        {
            ChooseMediaForCard("LEFT", _leftState, ImgLeftPreview, MediaLeftPreviewVideo, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, PanelLeftVideoControls);
        }

        private void BtnRightChooseMedia_Click(object sender, RoutedEventArgs e)
        {
            ChooseMediaForCard("RIGHT", _rightState, ImgRightPreview, MediaRightPreviewVideo, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, PanelRightVideoControls);
        }

        private void ChooseMediaForCard(
            string cardId,
            OutputCardState cardState,
            Image previewImg,
            MediaElement previewVideo,
            StackPanel promptPanel,
            TextBlock mediaPathTxt,
            TextBlock errorTxt,
            Border videoControlsPanel)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = $"{cardId} LED için Medya Dosyası Seçin",
                Filter = "Desteklenen Tüm Medyalar (*.mp4;*.mov;*.mkv;*.webm;*.png;*.jpg;*.jpeg;*.bmp)|*.mp4;*.mov;*.mkv;*.webm;*.png;*.jpg;*.jpeg;*.bmp|Video Dosyaları (*.mp4;*.mov;*.mkv;*.webm;*.avi;*.wmv)|*.mp4;*.mov;*.mkv;*.webm;*.avi;*.wmv|Görsel Dosyaları (*.png;*.jpg;*.jpeg;*.bmp;*.webp)|*.png;*.jpg;*.jpeg;*.bmp;*.webp|Tüm Dosyalar (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                if (LoadMediaForCard(cardId, cardState, dialog.FileName, previewImg, previewVideo, promptPanel, mediaPathTxt, errorTxt, videoControlsPanel))
                {
                    SaveAppSettings();
                    if (cardState.IsLiveUpdateEnabled && _outputManager.IsOutputActive(cardId))
                    {
                        ApplyDraftToLive(cardId);
                    }
                }
            }
        }

        private void HandleFileDrop(
            string cardId,
            OutputCardState cardState,
            DragEventArgs e,
            Image previewImg,
            MediaElement previewVideo,
            StackPanel promptPanel,
            TextBlock mediaPathTxt,
            TextBlock errorTxt,
            Border videoControlsPanel)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    if (LoadMediaForCard(cardId, cardState, filePath, previewImg, previewVideo, promptPanel, mediaPathTxt, errorTxt, videoControlsPanel))
                    {
                        SaveAppSettings();
                        if (cardState.IsLiveUpdateEnabled && _outputManager.IsOutputActive(cardId))
                        {
                            ApplyDraftToLive(cardId);
                        }
                    }
                }
            }
        }

        private void VideoTimer_Tick(object? sender, EventArgs e)
        {
            UpdateVideoProgressUI("LEFT", MediaLeftPreviewVideo, SliderLeftVideoPosition, TxtLeftVideoTime);
            UpdateVideoProgressUI("RIGHT", MediaRightPreviewVideo, SliderRightVideoPosition, TxtRightVideoTime);
        }

        private void UpdateVideoProgressUI(string cardId, MediaElement mediaVideo, Slider slider, TextBlock timeText)
        {
            if (mediaVideo != null && mediaVideo.Visibility == Visibility.Visible && mediaVideo.NaturalDuration.HasTimeSpan && mediaVideo.NaturalDuration.TimeSpan.TotalSeconds > 0)
            {
                double totalSec = mediaVideo.NaturalDuration.TimeSpan.TotalSeconds;
                double currentSec = mediaVideo.Position.TotalSeconds;

                if (slider != null && !_isUpdatingUI)
                {
                    _isUpdatingUI = true;
                    try
                    {
                        slider.Maximum = totalSec;
                        slider.Value = currentSec;
                    }
                    finally
                    {
                        _isUpdatingUI = false;
                    }
                }

                if (timeText != null)
                {
                    timeText.Text = $"{mediaVideo.Position:mm\\:ss} / {mediaVideo.NaturalDuration.TimeSpan:mm\\:ss}";
                }
            }
        }

        private void BtnLeftChooseWeb_Click(object sender, RoutedEventArgs e)
        {
            OpenWebPanelForCard("LEFT", _leftState, PanelLeftWebControls, TxtLeftWebUrl);
        }

        private void BtnRightChooseWeb_Click(object sender, RoutedEventArgs e)
        {
            OpenWebPanelForCard("RIGHT", _rightState, PanelRightWebControls, TxtRightWebUrl);
        }

        private void OpenWebPanelForCard(string cardId, OutputCardState cardState, Border webPanel, TextBox urlTxt)
        {
            if (cardId == "LEFT") PanelLeftVideoControls.Visibility = Visibility.Collapsed;
            else PanelRightVideoControls.Visibility = Visibility.Collapsed;

            webPanel.Visibility = Visibility.Visible;

            if (string.IsNullOrWhiteSpace(urlTxt.Text) || urlTxt.Text == "https://subtitles.live.com")
            {
                if (!string.IsNullOrWhiteSpace(cardState.DraftLayout.WebsiteState.Url))
                {
                    urlTxt.Text = cardState.DraftLayout.WebsiteState.Url;
                }
            }
        }

        private void BtnLeftLoadWeb_Click(object sender, RoutedEventArgs e)
        {
            if (LoadWebForCard("LEFT", _leftState, TxtLeftWebUrl.Text))
            {
                SaveAppSettings();
                if (_leftState.IsLiveUpdateEnabled && _outputManager.IsOutputActive("LEFT"))
                {
                    ApplyDraftToLive("LEFT");
                }
            }
        }

        private void BtnRightLoadWeb_Click(object sender, RoutedEventArgs e)
        {
            if (LoadWebForCard("RIGHT", _rightState, TxtRightWebUrl.Text))
            {
                SaveAppSettings();
                if (_rightState.IsLiveUpdateEnabled && _outputManager.IsOutputActive("RIGHT"))
                {
                    ApplyDraftToLive("RIGHT");
                }
            }
        }

        private void BtnLeftRefreshWeb_Click(object sender, RoutedEventArgs e)
        {
            RefreshWebForCard("LEFT", _leftState);
        }

        private void BtnRightRefreshWeb_Click(object sender, RoutedEventArgs e)
        {
            RefreshWebForCard("RIGHT", _rightState);
        }

        private void RefreshWebForCard(string cardId, OutputCardState cardState)
        {
            try
            {
                if (cardState.IsActive && _outputManager.IsOutputActive(cardId))
                {
                    ApplyDraftToLive(cardId);
                    TxtGlobalStatus.Text = $"{cardId} LED web yayını yenilendi.";
                }
                else
                {
                    TxtGlobalStatus.Text = $"{cardId} LED taslak web adresi hazır.";
                }
            }
            catch (Exception ex)
            {
                TxtGlobalStatus.Text = $"Yenileme Hatası ({cardId}): {ex.Message}";
            }
        }

        private void BtnLeftOpenExternalWeb_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalBrowser(_leftState.DraftLayout.WebsiteState.Url);
        }

        private void BtnRightOpenExternalWeb_Click(object sender, RoutedEventArgs e)
        {
            OpenExternalBrowser(_rightState.DraftLayout.WebsiteState.Url);
        }

        private void OpenExternalBrowser(string url)
        {
            if (MediaValidationHelper.ValidateUrl(url, out string formattedUrl, out _))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(formattedUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    TxtGlobalStatus.Text = $"Harici Tarayıcı Açma Hatası: {ex.Message}";
                }
            }
        }

        private void ChkLeftWebMute_Click(object sender, RoutedEventArgs e)
        {
            _leftState.DraftLayout.WebsiteState.IsMuted = (ChkLeftWebMute.IsChecked == true);
            if (_leftState.IsActive && _leftState.IsLiveUpdateEnabled) ApplyDraftToLive("LEFT");
        }

        private void ChkRightWebMute_Click(object sender, RoutedEventArgs e)
        {
            _rightState.DraftLayout.WebsiteState.IsMuted = (ChkRightWebMute.IsChecked == true);
            if (_rightState.IsActive && _rightState.IsLiveUpdateEnabled) ApplyDraftToLive("RIGHT");
        }

        private bool LoadWebForCard(string cardId, OutputCardState cardState, string urlInput)
        {
            var previewImg = (cardId == "LEFT") ? ImgLeftPreview : ImgRightPreview;
            var previewVideo = (cardId == "LEFT") ? MediaLeftPreviewVideo : MediaRightPreviewVideo;
            var webPreviewPrompt = (cardId == "LEFT") ? PanelLeftWebPreviewPrompt : PanelRightWebPreviewPrompt;
            var webPreviewHostTxt = (cardId == "LEFT") ? TxtLeftWebPreviewHost : TxtRightWebPreviewHost;
            var promptPanel = (cardId == "LEFT") ? PanelLeftDropPrompt : PanelRightDropPrompt;
            var mediaPathTxt = (cardId == "LEFT") ? TxtLeftMediaPath : TxtRightMediaPath;
            var errorTxt = (cardId == "LEFT") ? TxtLeftError : TxtRightError;
            var videoPanel = (cardId == "LEFT") ? PanelLeftVideoControls : PanelRightVideoControls;
            var webPanel = (cardId == "LEFT") ? PanelLeftWebControls : PanelRightWebControls;
            var webUrlTxt = (cardId == "LEFT") ? TxtLeftWebUrl : TxtRightWebUrl;
            var webStatusTxt = (cardId == "LEFT") ? TxtLeftWebStatus : TxtRightWebStatus;

            if (!MediaValidationHelper.ValidateUrl(urlInput, out string formattedUrl, out string err))
            {
                errorTxt.Text = $"HATA: {err}";
                errorTxt.Visibility = Visibility.Visible;
                if (webStatusTxt != null) webStatusTxt.Text = "Durum: Geçersiz URL";
                return false;
            }

            try
            {
                var mediaSource = MediaSource.FromUrl(formattedUrl);
                cardState.DraftLayout.MediaSource = mediaSource;
                cardState.DraftLayout.MediaPath = formattedUrl;
                cardState.DraftLayout.WebsiteState.Url = formattedUrl;

                previewImg.Visibility = Visibility.Collapsed;
                previewImg.Source = null;

                previewVideo.Stop();
                previewVideo.Source = null;
                previewVideo.Visibility = Visibility.Collapsed;

                videoPanel.Visibility = Visibility.Collapsed;
                webPanel.Visibility = Visibility.Visible;
                promptPanel.Visibility = Visibility.Collapsed;

                if (webPreviewPrompt != null)
                {
                    webPreviewPrompt.Visibility = Visibility.Visible;
                    if (webPreviewHostTxt != null) webPreviewHostTxt.Text = mediaSource.DisplayName;
                }

                webUrlTxt.Text = formattedUrl;
                mediaPathTxt.Text = $"🌐 {mediaSource.DisplayName}";
                errorTxt.Visibility = Visibility.Collapsed;
                if (webStatusTxt != null) webStatusTxt.Text = "Durum: Web Sitesi Hazır";

                RenderDraftPreview(cardId);
                return true;
            }
            catch (Exception ex)
            {
                errorTxt.Text = $"Web Yükleme Hatası: {ex.Message}";
                errorTxt.Visibility = Visibility.Visible;
            }

            return false;
        }

        private bool LoadMediaForCard(
            string cardId,
            OutputCardState cardState,
            string filePath,
            Image previewImg,
            MediaElement previewVideo,
            StackPanel promptPanel,
            TextBlock mediaPathTxt,
            TextBlock errorTxt,
            Border videoControlsPanel)
        {
            if (!string.IsNullOrWhiteSpace(filePath) &&
                (filePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 filePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                 filePath.StartsWith("www.", StringComparison.OrdinalIgnoreCase)))
            {
                return LoadWebForCard(cardId, cardState, filePath);
            }

            if (!MediaValidationHelper.ValidateMediaFile(filePath, out string err))
            {
                errorTxt.Text = $"HATA: {err}";
                errorTxt.Visibility = Visibility.Visible;
                return false;
            }

            try
            {
                var mediaSource = MediaSource.FromFile(filePath);
                cardState.DraftLayout.MediaSource = mediaSource;
                cardState.DraftLayout.MediaPath = filePath;

                var webPanel = (cardId == "LEFT") ? PanelLeftWebControls : PanelRightWebControls;
                var webPreviewPrompt = (cardId == "LEFT") ? PanelLeftWebPreviewPrompt : PanelRightWebPreviewPrompt;
                if (webPanel != null) webPanel.Visibility = Visibility.Collapsed;
                if (webPreviewPrompt != null) webPreviewPrompt.Visibility = Visibility.Collapsed;

                if (mediaSource.Type == MediaSourceType.Video)
                {
                    previewImg.Visibility = Visibility.Collapsed;
                    previewImg.Source = null;

                    previewVideo.Visibility = Visibility.Visible;
                    previewVideo.Source = new Uri(filePath, UriKind.Absolute);
                    previewVideo.Play();
                    previewVideo.Pause(); // Display first frame thumbnail in draft preview!

                    videoControlsPanel.Visibility = Visibility.Visible;
                    promptPanel.Visibility = Visibility.Collapsed;
                    mediaPathTxt.Text = $"🎬 {mediaSource.DisplayName}";
                    errorTxt.Visibility = Visibility.Collapsed;

                    RenderDraftPreview(cardId);
                    return true;
                }
                else
                {
                    previewVideo.Stop();
                    previewVideo.Source = null;
                    previewVideo.Visibility = Visibility.Collapsed;
                    videoControlsPanel.Visibility = Visibility.Collapsed;

                    var bitmap = CreateBitmap(filePath);
                    if (bitmap != null)
                    {
                        previewImg.Source = bitmap;
                        previewImg.Visibility = Visibility.Visible;
                        promptPanel.Visibility = Visibility.Collapsed;
                        mediaPathTxt.Text = $"🖼️ {mediaSource.DisplayName}";
                        errorTxt.Visibility = Visibility.Collapsed;
                        RenderDraftPreview(cardId);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                errorTxt.Text = $"Yükleme Hatası: {ex.Message}";
                errorTxt.Visibility = Visibility.Visible;
            }

            return false;
        }

        private BitmapImage? CreateBitmap(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            if (_bitmapCache.TryGetValue(filePath, out var cached) && cached != null)
            {
                return cached;
            }

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                _bitmapCache[filePath] = bitmap;
                return bitmap;
            }
            catch (Exception ex)
            {
                AppLogger.LogError($"Görsel yükleme hatası ({filePath}): {ex.Message}", ex);
                return null;
            }
        }

        // --- Video Control Handlers ---

        private void BtnLeftPlay_Click(object sender, RoutedEventArgs e)
        {
            SetVideoPlaybackStatus("LEFT", _leftState, PlaybackStatus.Playing);
        }

        private void BtnLeftPause_Click(object sender, RoutedEventArgs e)
        {
            SetVideoPlaybackStatus("LEFT", _leftState, PlaybackStatus.Paused);
        }

        private void BtnLeftStopVideo_Click(object sender, RoutedEventArgs e)
        {
            SetVideoPlaybackStatus("LEFT", _leftState, PlaybackStatus.Stopped);
        }

        private void BtnRightPlay_Click(object sender, RoutedEventArgs e)
        {
            SetVideoPlaybackStatus("RIGHT", _rightState, PlaybackStatus.Playing);
        }

        private void BtnRightPause_Click(object sender, RoutedEventArgs e)
        {
            SetVideoPlaybackStatus("RIGHT", _rightState, PlaybackStatus.Paused);
        }

        private void BtnRightStopVideo_Click(object sender, RoutedEventArgs e)
        {
            SetVideoPlaybackStatus("RIGHT", _rightState, PlaybackStatus.Stopped);
        }

        private void SetVideoPlaybackStatus(string cardId, OutputCardState cardState, PlaybackStatus status)
        {
            if (_isInitializing || _isUpdatingUI) return;
            cardState.DraftLayout.VideoState.Status = status;

            var previewVideo = (cardId == "LEFT") ? MediaLeftPreviewVideo : MediaRightPreviewVideo;
            if (previewVideo != null && previewVideo.Visibility == Visibility.Visible)
            {
                if (status == PlaybackStatus.Playing) previewVideo.Play();
                else if (status == PlaybackStatus.Paused) previewVideo.Pause();
                else { previewVideo.Stop(); previewVideo.Position = TimeSpan.Zero; }
            }

            if (cardState.IsActive && _outputManager.IsOutputActive(cardId))
            {
                cardState.LiveAppliedLayout.VideoState.Status = status;
                ApplyDraftToLive(cardId);
            }
        }

        private void ChkLeftLoop_Click(object sender, RoutedEventArgs e)
        {
            _leftState.DraftLayout.VideoState.IsLooping = (ChkLeftLoop.IsChecked == true);
            if (_leftState.IsActive) ApplyDraftToLive("LEFT");
        }

        private void ChkLeftMute_Click(object sender, RoutedEventArgs e)
        {
            _leftState.DraftLayout.VideoState.IsMuted = (ChkLeftMute.IsChecked == true);
            if (_leftState.IsActive) ApplyDraftToLive("LEFT");
        }

        private void SliderLeftVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _isUpdatingUI || _leftState == null) return;
            _leftState.DraftLayout.VideoState.Volume = SliderLeftVolume.Value / 100.0;
            if (TxtLeftVolume != null) TxtLeftVolume.Text = $"{(int)SliderLeftVolume.Value}%";
            if (_leftState.IsActive && _leftState.IsLiveUpdateEnabled) ApplyDraftToLive("LEFT");
        }

        private void SliderLeftVideoPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Optional timeline seek
        }

        private void ChkRightLoop_Click(object sender, RoutedEventArgs e)
        {
            _rightState.DraftLayout.VideoState.IsLooping = (ChkRightLoop.IsChecked == true);
            if (_rightState.IsActive) ApplyDraftToLive("RIGHT");
        }

        private void ChkRightMute_Click(object sender, RoutedEventArgs e)
        {
            _rightState.DraftLayout.VideoState.IsMuted = (ChkRightMute.IsChecked == true);
            if (_rightState.IsActive) ApplyDraftToLive("RIGHT");
        }

        private void SliderRightVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _isUpdatingUI || _rightState == null) return;
            _rightState.DraftLayout.VideoState.Volume = SliderRightVolume.Value / 100.0;
            if (TxtRightVolume != null) TxtRightVolume.Text = $"{(int)SliderRightVolume.Value}%";
            if (_rightState.IsActive && _rightState.IsLiveUpdateEnabled) ApplyDraftToLive("RIGHT");
        }

        private void SliderRightVideoPosition_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // Optional timeline seek
        }

        // --- Test Pattern Handlers ---

        private void BtnLeftTestPattern_Click(object sender, RoutedEventArgs e)
        {
            GenerateAndApplyTestPattern("LEFT", _leftState, ImgLeftPreview, MediaLeftPreviewVideo, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, BtnLeftRestoreMedia, PanelLeftVideoControls);
        }

        private void BtnRightTestPattern_Click(object sender, RoutedEventArgs e)
        {
            GenerateAndApplyTestPattern("RIGHT", _rightState, ImgRightPreview, MediaRightPreviewVideo, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, BtnRightRestoreMedia, PanelRightVideoControls);
        }

        private void GenerateAndApplyTestPattern(
            string cardId,
            OutputCardState cardState,
            Image previewImg,
            MediaElement previewVideo,
            StackPanel promptPanel,
            TextBlock mediaPathTxt,
            TextBlock errorTxt,
            Button restoreBtn,
            Border videoControlsPanel)
        {
            if (_isInitializing || _isUpdatingUI) return;

            if (!string.IsNullOrEmpty(cardState.DraftLayout.MediaPath) && !cardState.DraftLayout.MediaPath.Contains("TestPattern_"))
            {
                cardState.PreviousMediaPath = cardState.DraftLayout.MediaPath;
                restoreBtn.Visibility = Visibility.Visible;
            }

            try
            {
                string patternPath = TestPatternGenerator.SaveTestPatternToTempFile(cardState.Title, cardState.Calibration.LogicalLedWidth, cardState.Calibration.LogicalLedHeight);
                if (LoadMediaForCard(cardId, cardState, patternPath, previewImg, previewVideo, promptPanel, mediaPathTxt, errorTxt, videoControlsPanel))
                {
                    SaveAppSettings();
                    RenderDraftPreview(cardId);

                    if (cardState.IsLiveUpdateEnabled && _outputManager.IsOutputActive(cardId))
                    {
                        ApplyDraftToLive(cardId);
                    }

                    TxtGlobalStatus.Text = $"{cardId} LED Test Deseni oluşturuldu ve önizlemeye yüklendi ({cardState.Calibration.LogicalLedWidth}x{cardState.Calibration.LogicalLedHeight}).";
                }
            }
            catch (Exception ex)
            {
                errorTxt.Text = $"Test deseni hatası: {ex.Message}";
                errorTxt.Visibility = Visibility.Visible;
            }
        }

        private void BtnLeftRestoreMedia_Click(object sender, RoutedEventArgs e)
        {
            RestorePreviousMedia("LEFT", _leftState, ImgLeftPreview, MediaLeftPreviewVideo, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, BtnLeftRestoreMedia, PanelLeftVideoControls);
        }

        private void BtnRightRestoreMedia_Click(object sender, RoutedEventArgs e)
        {
            RestorePreviousMedia("RIGHT", _rightState, ImgRightPreview, MediaRightPreviewVideo, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, BtnRightRestoreMedia, PanelRightVideoControls);
        }

        private void RestorePreviousMedia(
            string cardId,
            OutputCardState cardState,
            Image previewImg,
            MediaElement previewVideo,
            StackPanel promptPanel,
            TextBlock mediaPathTxt,
            TextBlock errorTxt,
            Button restoreBtn,
            Border videoControlsPanel)
        {
            if (_isInitializing || _isUpdatingUI) return;

            if (!string.IsNullOrEmpty(cardState.PreviousMediaPath) && File.Exists(cardState.PreviousMediaPath))
            {
                string path = cardState.PreviousMediaPath;
                cardState.PreviousMediaPath = null;
                restoreBtn.Visibility = Visibility.Collapsed;

                if (LoadMediaForCard(cardId, cardState, path, previewImg, previewVideo, promptPanel, mediaPathTxt, errorTxt, videoControlsPanel))
                {
                    SaveAppSettings();
                    RenderDraftPreview(cardId);

                    if (cardState.IsLiveUpdateEnabled && _outputManager.IsOutputActive(cardId))
                    {
                        ApplyDraftToLive(cardId);
                    }

                    TxtGlobalStatus.Text = $"{cardId} LED için önceki medya geri yüklendi.";
                }
            }
        }

        // --- Layout Control Handlers (Draft Edits) ---

        private void RadioLeftScale_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || RadioLeftFit == null || _leftState == null) return;

            if (RadioLeftFit.IsChecked == true) _leftState.DraftLayout.ScaleMode = ScaleMode.Fit;
            else if (RadioLeftFill.IsChecked == true) _leftState.DraftLayout.ScaleMode = ScaleMode.Fill;
            else if (RadioLeftStretch.IsChecked == true) _leftState.DraftLayout.ScaleMode = ScaleMode.Stretch;
            else if (RadioLeftCustom.IsChecked == true) _leftState.DraftLayout.ScaleMode = ScaleMode.Custom;

            OnDraftLayoutChanged("LEFT");
        }

        private void RadioRightScale_Checked(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || RadioRightFit == null || _rightState == null) return;

            if (RadioRightFit.IsChecked == true) _rightState.DraftLayout.ScaleMode = ScaleMode.Fit;
            else if (RadioRightFill.IsChecked == true) _rightState.DraftLayout.ScaleMode = ScaleMode.Fill;
            else if (RadioRightStretch.IsChecked == true) _rightState.DraftLayout.ScaleMode = ScaleMode.Stretch;
            else if (RadioRightCustom.IsChecked == true) _rightState.DraftLayout.ScaleMode = ScaleMode.Custom;

            OnDraftLayoutChanged("RIGHT");
        }

        private void SliderLeftLayout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _isUpdatingUI || _leftState == null || SliderLeftZoom == null || SliderLeftOffsetX == null || SliderLeftOffsetY == null) return;

            _leftState.DraftLayout.Zoom = SliderLeftZoom.Value / 100.0;
            _leftState.DraftLayout.OffsetX = SliderLeftOffsetX.Value;
            _leftState.DraftLayout.OffsetY = SliderLeftOffsetY.Value;

            if (TxtLeftZoom != null) TxtLeftZoom.Text = $"{(int)SliderLeftZoom.Value}%";
            if (TxtLeftOffsetX != null) TxtLeftOffsetX.Text = $"{(int)SliderLeftOffsetX.Value} px";
            if (TxtLeftOffsetY != null) TxtLeftOffsetY.Text = $"{(int)SliderLeftOffsetY.Value} px";

            OnDraftLayoutChanged("LEFT");
        }

        private void SliderRightLayout_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isInitializing || _isUpdatingUI || _rightState == null || SliderRightZoom == null || SliderRightOffsetX == null || SliderRightOffsetY == null) return;

            _rightState.DraftLayout.Zoom = SliderRightZoom.Value / 100.0;
            _rightState.DraftLayout.OffsetX = SliderRightOffsetX.Value;
            _rightState.DraftLayout.OffsetY = SliderRightOffsetY.Value;

            if (TxtRightZoom != null) TxtRightZoom.Text = $"{(int)SliderRightZoom.Value}%";
            if (TxtRightOffsetX != null) TxtRightOffsetX.Text = $"{(int)SliderRightOffsetX.Value} px";
            if (TxtRightOffsetY != null) TxtRightOffsetY.Text = $"{(int)SliderRightOffsetY.Value} px";

            OnDraftLayoutChanged("RIGHT");
        }

        private void OnDraftLayoutChanged(string cardId)
        {
            if (_isInitializing || _isUpdatingUI || _settingsService == null || _appSettings == null) return;

            RenderDraftPreview(cardId);
            SaveAppSettingsDebounced();

            var cardState = (cardId == "LEFT") ? _leftState : _rightState;
            if (cardState.IsLiveUpdateEnabled && _outputManager.IsOutputActive(cardId))
            {
                // Live Sync constraint: Only sync position/scale if staged media matches current live media!
                if (string.Equals(cardState.DraftLayout.MediaPath, cardState.LiveAppliedLayout.MediaPath, StringComparison.OrdinalIgnoreCase) &&
                    cardState.DraftLayout.MediaSource.Type == cardState.LiveAppliedLayout.MediaSource.Type)
                {
                    ApplyDraftToLive(cardId);
                }
            }
        }

        private void BtnLeftResetLayout_Click(object sender, RoutedEventArgs e)
        {
            _leftState.DraftLayout.Zoom = 1.0;
            _leftState.DraftLayout.OffsetX = 0;
            _leftState.DraftLayout.OffsetY = 0;
            _leftState.DraftLayout.ScaleMode = ScaleMode.Fit;

            SyncCardStateToUI("LEFT", _leftState);
            OnDraftLayoutChanged("LEFT");
        }

        private void BtnRightResetLayout_Click(object sender, RoutedEventArgs e)
        {
            _rightState.DraftLayout.Zoom = 1.0;
            _rightState.DraftLayout.OffsetX = 0;
            _rightState.DraftLayout.OffsetY = 0;
            _rightState.DraftLayout.ScaleMode = ScaleMode.Fit;

            SyncCardStateToUI("RIGHT", _rightState);
            OnDraftLayoutChanged("RIGHT");
        }

        private void ChkLeftLiveSync_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || _leftState == null) return;
            _leftState.IsLiveUpdateEnabled = (ChkLeftLiveSync.IsChecked == true);
            _appSettings.LeftOutput.IsLiveUpdateEnabled = _leftState.IsLiveUpdateEnabled;
            SaveAppSettings();
        }

        private void ChkRightLiveSync_Changed(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || _rightState == null) return;
            _rightState.IsLiveUpdateEnabled = (ChkRightLiveSync.IsChecked == true);
            _appSettings.RightOutput.IsLiveUpdateEnabled = _rightState.IsLiveUpdateEnabled;
            SaveAppSettings();
        }

        // --- Content Bank / Presets Management ---

        private void BtnLeftAddToBank_Click(object sender, RoutedEventArgs e)
        {
            AddDraftToContentBank("LEFT", _leftState);
        }

        private void BtnRightAddToBank_Click(object sender, RoutedEventArgs e)
        {
            AddDraftToContentBank("RIGHT", _rightState);
        }

        private void AddDraftToContentBank(string cardId, OutputCardState cardState)
        {
            if (_isInitializing || _isUpdatingUI) return;

            if (string.IsNullOrEmpty(cardState.DraftLayout.MediaPath) && cardState.DraftLayout.MediaSource.Type != MediaSourceType.Website)
            {
                var errTxt = (cardId == "LEFT") ? TxtLeftError : TxtRightError;
                errTxt.Text = "Bankaya eklemek için önce sıradaki medyayı seçin.";
                errTxt.Visibility = Visibility.Visible;
                return;
            }

            string defaultTitle = cardState.DraftLayout.MediaSource.DisplayName;
            var item = ContentBankItem.FromLayoutState(defaultTitle, cardState.DraftLayout);
            cardState.ContentBank.Add(item);

            if (cardId == "LEFT") _appSettings.LeftOutput.ContentBank = cardState.ContentBank;
            else _appSettings.RightOutput.ContentBank = cardState.ContentBank;

            SaveAppSettings();
            RenderContentBankUI(cardId);
            TxtGlobalStatus.Text = $"{cardId} İçerik Bankasına eklendi: {defaultTitle}";
        }

        private void RenderContentBankUI(string cardId)
        {
            var cardState = (cardId == "LEFT") ? _leftState : _rightState;
            var itemsPanel = (cardId == "LEFT") ? PanelLeftContentBankItems : PanelRightContentBankItems;

            if (itemsPanel == null) return;

            itemsPanel.Children.Clear();

            if (cardState.ContentBank == null || cardState.ContentBank.Count == 0)
            {
                itemsPanel.Children.Add(new TextBlock
                {
                    Text = "(Kayıtlı içerik yok. Medya seçip '➕ Bankaya Ekle' butonuna basın)",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                    Margin = new Thickness(0, 4, 0, 4)
                });
                return;
            }

            foreach (var item in cardState.ContentBank)
            {
                bool fileExists = item.CheckFileExists(out string? missingPath);
                bool isCurrentlyLive = cardState.IsActive && string.Equals(item.MediaSource.FilePath, cardState.LiveAppliedLayout.MediaPath, StringComparison.OrdinalIgnoreCase);
                bool isCurrentlyStaged = string.Equals(item.MediaSource.FilePath, cardState.DraftLayout.MediaPath, StringComparison.OrdinalIgnoreCase);

                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                    BorderBrush = isCurrentlyLive ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) :
                                  isCurrentlyStaged ? new SolidColorBrush(Color.FromRgb(245, 158, 11)) :
                                  new SolidColorBrush(Color.FromRgb(71, 85, 105)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 4, 6, 4),
                    Margin = new Thickness(0, 0, 0, 4)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var infoStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                infoStack.Children.Add(new TextBlock
                {
                    Text = $"{item.GetIcon()} {item.Title}",
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = fileExists ? Brushes.White : new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 180
                });

                if (!fileExists)
                {
                    infoStack.Children.Add(new TextBlock
                    {
                        Text = " ⚠️ BULUNAMADI",
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    });
                }
                else if (isCurrentlyLive)
                {
                    infoStack.Children.Add(new TextBlock
                    {
                        Text = " 🔴 YAYINDA",
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    });
                }
                else if (isCurrentlyStaged)
                {
                    infoStack.Children.Add(new TextBlock
                    {
                        Text = " 🟡 SIRADAKİ",
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    });
                }

                grid.Children.Add(infoStack);
                Grid.SetColumn(infoStack, 0);

                var btnStack = new StackPanel { Orientation = Orientation.Horizontal };

                var selectBtn = new Button
                {
                    Content = "🎯 SIRADAKİ YAP",
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(0, 0, 4, 0),
                    Style = (Style)FindResource("SecondaryBtnStyle")
                };
                selectBtn.Click += (s, e) => StageContentBankItem(cardId, item);
                btnStack.Children.Add(selectBtn);

                var delBtn = new Button
                {
                    Content = "🗑️",
                    FontSize = 9,
                    Padding = new Thickness(4, 2, 4, 2),
                    Style = (Style)FindResource("DangerBtnStyle")
                };
                delBtn.Click += (s, e) => RemoveFromContentBank(cardId, item);
                btnStack.Children.Add(delBtn);

                grid.Children.Add(btnStack);
                Grid.SetColumn(btnStack, 1);

                border.Child = grid;
                itemsPanel.Children.Add(border);
            }
        }

        private void StageContentBankItem(string cardId, ContentBankItem item)
        {
            var cardState = (cardId == "LEFT") ? _leftState : _rightState;
            var errorTxt = (cardId == "LEFT") ? TxtLeftError : TxtRightError;

            if (!item.CheckFileExists(out string? missingPath))
            {
                errorTxt.Text = $"⚠️ HATA: İçerik yüklenemedi! Dosya bulunamadı: {missingPath}";
                errorTxt.Visibility = Visibility.Visible;
                AppLogger.LogWarning($"Sıradaki yapma hatası: {missingPath} eksik.");
                return;
            }

            cardState.DraftLayout = item.ToLayoutState();
            SyncCardStateToUI(cardId, cardState);

            var previewImg = (cardId == "LEFT") ? ImgLeftPreview : ImgRightPreview;
            var previewVideo = (cardId == "LEFT") ? MediaLeftPreviewVideo : MediaRightPreviewVideo;
            var promptPanel = (cardId == "LEFT") ? PanelLeftDropPrompt : PanelRightDropPrompt;
            var mediaPathTxt = (cardId == "LEFT") ? TxtLeftMediaPath : TxtRightMediaPath;
            var videoControlsPanel = (cardId == "LEFT") ? PanelLeftVideoControls : PanelRightVideoControls;

            if (!string.IsNullOrEmpty(cardState.DraftLayout.MediaPath))
            {
                LoadMediaForCard(cardId, cardState, cardState.DraftLayout.MediaPath, previewImg, previewVideo, promptPanel, mediaPathTxt, errorTxt, videoControlsPanel);
            }

            errorTxt.Visibility = Visibility.Collapsed;
            RenderDraftPreview(cardId);
            RenderContentBankUI(cardId);
            SaveAppSettingsDebounced();

            TxtGlobalStatus.Text = $"🟡 {cardId} için SIRADAKİ içerik seçildi: {item.Title}. (Yayına almak için 🔴 UYGULA butonuna basın)";
        }

        private void RemoveFromContentBank(string cardId, ContentBankItem item)
        {
            var cardState = (cardId == "LEFT") ? _leftState : _rightState;
            cardState.ContentBank.RemoveAll(i => i.Id == item.Id);

            if (cardId == "LEFT") _appSettings.LeftOutput.ContentBank = cardState.ContentBank;
            else _appSettings.RightOutput.ContentBank = cardState.ContentBank;

            SaveAppSettings();
            RenderContentBankUI(cardId);
        }

        // --- Apply Draft to Live Applied State ---

        private void BtnLeftApply_Click(object sender, RoutedEventArgs e)
        {
            ApplyDraftToLive("LEFT");
        }

        private void BtnRightApply_Click(object sender, RoutedEventArgs e)
        {
            ApplyDraftToLive("RIGHT");
        }

        private void ApplyDraftToLive(string cardId)
        {
            var cardState = (cardId == "LEFT") ? _leftState : _rightState;
            var combo = (cardId == "LEFT") ? CmbLeftDisplay : CmbRightDisplay;
            var badge = (cardId == "LEFT") ? BadgeLeftStatus : BadgeRightStatus;
            var badgeTxt = (cardId == "LEFT") ? TxtLeftStatus : TxtRightStatus;
            var errorTxt = (cardId == "LEFT") ? TxtLeftError : TxtRightError;
            var liveTitleTxt = (cardId == "LEFT") ? TxtLeftLiveTitle : TxtRightLiveTitle;

            var staged = cardState.DraftLayout;

            // Safe pre-validation: verify file availability before switching live output!
            if (staged.MediaSource.Type == MediaSourceType.Image || staged.MediaSource.Type == MediaSourceType.Video)
            {
                if (!string.IsNullOrEmpty(staged.MediaPath) && !File.Exists(staged.MediaPath))
                {
                    errorTxt.Text = $"⚠️ HATA: YAYINA ALINAMADI! '{staged.MediaSource.DisplayName}' dosyası diske erişilemiyor. Mevcut canlı yayın korundu.";
                    errorTxt.Visibility = Visibility.Visible;
                    AppLogger.LogError($"ApplyDraftToLive hatası ({cardId}): {staged.MediaPath} bulunamadı.");
                    return;
                }
            }

            var display = GetSelectedDisplayFromCombo(combo);
            if (display == null)
            {
                errorTxt.Text = "Lütfen yayın için geçerli bir fiziksel ekran seçin.";
                errorTxt.Visibility = Visibility.Visible;
                return;
            }

            if (display.IsPrimary)
            {
                errorTxt.Text = "GÜVENLİK ENGELİ: Operatör birincil ekranına yayın yapılamaz!";
                errorTxt.Visibility = Visibility.Visible;
                return;
            }

            // Atomically copy DraftLayout and DraftCalibration to LiveApplied state
            cardState.LiveAppliedLayout = cardState.DraftLayout.Clone();
            cardState.LiveAppliedCalibration = cardState.DraftCalibration.Clone();

            // Ensure live applied calibration is validated against GPU signal bounds
            cardState.LiveAppliedCalibration.ValidateAndClamp(display.Width, display.Height);

            BitmapImage? bitmap = null;
            if (!string.IsNullOrEmpty(cardState.LiveAppliedLayout.MediaPath) && cardState.LiveAppliedLayout.MediaSource.Type == MediaSourceType.Image)
            {
                bitmap = CreateBitmap(cardState.LiveAppliedLayout.MediaPath);
            }

            try
            {
                if (_outputManager.IsOutputActive(cardId))
                {
                    _outputManager.UpdateLiveOutput(cardId, cardState.LiveAppliedCalibration, cardState.LiveAppliedLayout, bitmap, cardState.IsBlackout);
                }
                else
                {
                    _outputManager.StartOutput(cardId, display, cardState.LiveAppliedCalibration, cardState.LiveAppliedLayout, bitmap, cardState.IsBlackout);
                }

                cardState.IsActive = true;
                SetCardStatus(cardState, badge, badgeTxt, cardState.IsBlackout ? "BLACKOUT" : "ACTIVE", cardState.IsBlackout ? Colors.DarkOrange : Colors.LimeGreen);
                errorTxt.Visibility = Visibility.Collapsed;

                if (liveTitleTxt != null)
                {
                    liveTitleTxt.Text = cardState.LiveAppliedLayout.MediaSource.DisplayName;
                }

                TxtGlobalStatus.Text = $"🔴 {cardId} LED canlı yayını güncellendi: {cardState.LiveAppliedLayout.MediaSource.DisplayName} ({display.FriendlyName}).";
                RenderContentBankUI(cardId);
            }
            catch (Exception ex)
            {
                errorTxt.Text = $"Çıkış yayını uygulanamadı: {ex.Message}";
                errorTxt.Visibility = Visibility.Visible;
                AppLogger.LogError($"ApplyDraftToLive exception ({cardId}): {ex.Message}", ex);
            }

            SaveAppSettings();
        }

        // --- Immediate Live Safety Controls ---

        private void BtnLeftStop_Click(object sender, RoutedEventArgs e)
        {
            StopCardOutput("LEFT", _leftState, BadgeLeftStatus, TxtLeftStatus);
        }

        private void BtnRightStop_Click(object sender, RoutedEventArgs e)
        {
            StopCardOutput("RIGHT", _rightState, BadgeRightStatus, TxtRightStatus);
        }

        private void StopCardOutput(string cardId, OutputCardState state, Border badge, TextBlock badgeTxt)
        {
            _outputManager.StopOutput(cardId);
            state.IsActive = false;
            SetCardStatus(state, badge, badgeTxt, "INACTIVE", Colors.Gray);
            TxtGlobalStatus.Text = $"{cardId} LED çıktısı durduruldu.";
        }

        private void BtnLeftBlack_Click(object sender, RoutedEventArgs e)
        {
            ToggleBlackout("LEFT", _leftState, BtnLeftBlack, BadgeLeftStatus, TxtLeftStatus);
        }

        private void BtnRightBlack_Click(object sender, RoutedEventArgs e)
        {
            ToggleBlackout("RIGHT", _rightState, BtnRightBlack, BadgeRightStatus, TxtRightStatus);
        }

        private void ToggleBlackout(string cardId, OutputCardState state, Button blackBtn, Border badge, TextBlock badgeTxt)
        {
            state.IsBlackout = !state.IsBlackout;

            if (state.IsBlackout)
            {
                blackBtn.Content = "🟡 GERİ YÜKLE (RESTORE)";
                if (state.IsActive)
                {
                    SetCardStatus(state, badge, badgeTxt, "BLACKOUT", Colors.DarkOrange);
                }
            }
            else
            {
                blackBtn.Content = "⚫ SİYAH (BLACK)";
                if (state.IsActive)
                {
                    SetCardStatus(state, badge, badgeTxt, "ACTIVE", Colors.LimeGreen);
                }
            }

            _outputManager.UpdateBlackout(cardId, state.IsBlackout);
            SaveAppSettings();
        }

        private void BtnStopAll_Click(object sender, RoutedEventArgs e)
        {
            _outputManager.StopAllOutputs();
            _leftState.IsActive = false;
            _rightState.IsActive = false;

            SetCardStatus(_leftState, BadgeLeftStatus, TxtLeftStatus, "INACTIVE", Colors.Gray);
            SetCardStatus(_rightState, BadgeRightStatus, TxtRightStatus, "INACTIVE", Colors.Gray);

            TxtGlobalStatus.Text = "Tüm LED çıktıları durduruldu.";
        }

        private void BtnIdentify_Click(object sender, RoutedEventArgs e)
        {
            var displays = _displayService.GetDisplays();

            foreach (var display in displays)
            {
                string role = "Ekran";
                if (display.IsPrimary)
                {
                    role = "OPERATÖR / BİRİNCİL EKRAN";
                }
                else if (_reservedCenterDisplay != null && _displayService.IsSameDisplay(display, _reservedCenterDisplay))
                {
                    role = "REZERVE CENTER (Windows Desktop)";
                }
                else if (_outputManager.IsOutputActive("LEFT") && _outputManager.GetActiveDisplay("LEFT") != null && _displayService.IsSameDisplay(display, _outputManager.GetActiveDisplay("LEFT")!))
                {
                    role = "LEFT LED (YAYINDA)";
                }
                else if (_outputManager.IsOutputActive("RIGHT") && _outputManager.GetActiveDisplay("RIGHT") != null && _displayService.IsSameDisplay(display, _outputManager.GetActiveDisplay("RIGHT")!))
                {
                    role = "RIGHT LED (YAYINDA)";
                }

                var overlay = new IdentifyOverlayWindow(display, role);
                overlay.Show();
            }

            TxtGlobalStatus.Text = "Ekran numaraları tanımlama katmanı gösteriliyor (3.5s).";
        }

        // --- Calibration Input Handlers ---

        private void TxtLeftCalibration_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || _leftState == null) return;

            int oldW = _leftState.DraftCalibration.LogicalLedWidth;
            int oldH = _leftState.DraftCalibration.LogicalLedHeight;

            if (int.TryParse(TxtLeftLedW.Text, out int w) && w > 0) _leftState.DraftCalibration.LogicalLedWidth = w;
            else TxtLeftLedW.Text = _leftState.DraftCalibration.LogicalLedWidth.ToString();

            if (int.TryParse(TxtLeftLedH.Text, out int h) && h > 0) _leftState.DraftCalibration.LogicalLedHeight = h;
            else TxtLeftLedH.Text = _leftState.DraftCalibration.LogicalLedHeight.ToString();

            if (int.TryParse(TxtLeftVpX.Text, out int vx)) _leftState.DraftCalibration.ViewportX = vx;
            else TxtLeftVpX.Text = _leftState.DraftCalibration.ViewportX.ToString();

            if (int.TryParse(TxtLeftVpY.Text, out int vy)) _leftState.DraftCalibration.ViewportY = vy;
            else TxtLeftVpY.Text = _leftState.DraftCalibration.ViewportY.ToString();

            bool dimensionsChanged = (_leftState.DraftCalibration.LogicalLedWidth != oldW || _leftState.DraftCalibration.LogicalLedHeight != oldH);

            if (dimensionsChanged && !string.IsNullOrEmpty(_leftState.DraftLayout.MediaPath) && _leftState.DraftLayout.MediaPath.Contains("TestPattern_"))
            {
                GenerateAndApplyTestPattern("LEFT", _leftState, ImgLeftPreview, MediaLeftPreviewVideo, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, BtnLeftRestoreMedia, PanelLeftVideoControls);
            }
            else
            {
                RenderDraftPreview("LEFT");
            }

            SaveAppSettings();

            if (_leftState.IsLiveUpdateEnabled && _outputManager.IsOutputActive("LEFT"))
            {
                ApplyDraftToLive("LEFT");
            }
        }

        private void TxtRightCalibration_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isInitializing || _isUpdatingUI || _rightState == null) return;

            int oldW = _rightState.DraftCalibration.LogicalLedWidth;
            int oldH = _rightState.DraftCalibration.LogicalLedHeight;

            if (int.TryParse(TxtRightLedW.Text, out int w) && w > 0) _rightState.DraftCalibration.LogicalLedWidth = w;
            else TxtRightLedW.Text = _rightState.DraftCalibration.LogicalLedWidth.ToString();

            if (int.TryParse(TxtRightLedH.Text, out int h) && h > 0) _rightState.DraftCalibration.LogicalLedHeight = h;
            else TxtRightLedH.Text = _rightState.DraftCalibration.LogicalLedHeight.ToString();

            if (int.TryParse(TxtRightVpX.Text, out int vx)) _rightState.DraftCalibration.ViewportX = vx;
            else TxtRightVpX.Text = _rightState.DraftCalibration.ViewportX.ToString();

            if (int.TryParse(TxtRightVpY.Text, out int vy)) _rightState.DraftCalibration.ViewportY = vy;
            else TxtRightVpY.Text = _rightState.DraftCalibration.ViewportY.ToString();

            bool dimensionsChanged = (_rightState.DraftCalibration.LogicalLedWidth != oldW || _rightState.DraftCalibration.LogicalLedHeight != oldH);

            if (dimensionsChanged && !string.IsNullOrEmpty(_rightState.DraftLayout.MediaPath) && _rightState.DraftLayout.MediaPath.Contains("TestPattern_"))
            {
                GenerateAndApplyTestPattern("RIGHT", _rightState, ImgRightPreview, MediaRightPreviewVideo, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, BtnRightRestoreMedia, PanelRightVideoControls);
            }
            else
            {
                RenderDraftPreview("RIGHT");
            }

            SaveAppSettings();

            if (_rightState.IsLiveUpdateEnabled && _outputManager.IsOutputActive("RIGHT"))
            {
                ApplyDraftToLive("RIGHT");
            }
        }

        private void SaveAppSettings()
        {
            if (_isInitializing || _isUpdatingUI || _settingsService == null || _appSettings == null) return;

            _appSettings.LeftOutput.Calibration = _leftState.DraftCalibration;
            _appSettings.LeftOutput.DraftLayout = _leftState.DraftLayout;
            _appSettings.LeftOutput.LiveAppliedLayout = _leftState.LiveAppliedLayout;
            _appSettings.LeftOutput.IsLiveUpdateEnabled = _leftState.IsLiveUpdateEnabled;
            _appSettings.LeftOutput.IsBlackout = _leftState.IsBlackout;

            _appSettings.RightOutput.Calibration = _rightState.DraftCalibration;
            _appSettings.RightOutput.DraftLayout = _rightState.DraftLayout;
            _appSettings.RightOutput.LiveAppliedLayout = _rightState.LiveAppliedLayout;
            _appSettings.RightOutput.IsLiveUpdateEnabled = _rightState.IsLiveUpdateEnabled;
            _appSettings.RightOutput.IsBlackout = _rightState.IsBlackout;

            _settingsService.SaveSettings(_appSettings);
        }

        private void SaveAppSettingsDebounced()
        {
            if (_isInitializing || _isUpdatingUI || _settingsService == null || _appSettings == null) return;

            _appSettings.LeftOutput.Calibration = _leftState.DraftCalibration;
            _appSettings.LeftOutput.DraftLayout = _leftState.DraftLayout;
            _appSettings.LeftOutput.LiveAppliedLayout = _leftState.LiveAppliedLayout;
            _appSettings.LeftOutput.IsLiveUpdateEnabled = _leftState.IsLiveUpdateEnabled;
            _appSettings.LeftOutput.IsBlackout = _leftState.IsBlackout;

            _appSettings.RightOutput.Calibration = _rightState.DraftCalibration;
            _appSettings.RightOutput.DraftLayout = _rightState.DraftLayout;
            _appSettings.RightOutput.LiveAppliedLayout = _rightState.LiveAppliedLayout;
            _appSettings.RightOutput.IsLiveUpdateEnabled = _rightState.IsLiveUpdateEnabled;
            _appSettings.RightOutput.IsBlackout = _rightState.IsBlackout;

            _settingsService.SaveSettingsDebounced(_appSettings);
        }

        private void SetCardStatus(OutputCardState state, Border badge, TextBlock badgeTxt, string statusText, Color color)
        {
            state.StatusText = statusText;
            badgeTxt.Text = statusText;
            badge.Background = new SolidColorBrush(color) { Opacity = 0.85 };
            badgeTxt.Foreground = Brushes.White;
            UpdateLiveBannerUI(state.CardId);
        }

        private void UpdateLiveBannerUI(string cardId)
        {
            var cardState = (cardId == "LEFT") ? _leftState : _rightState;
            var banner = (cardId == "LEFT") ? BorderLeftLiveBanner : BorderRightLiveBanner;
            var header = (cardId == "LEFT") ? TxtLeftLiveHeader : TxtRightLiveHeader;
            var title = (cardId == "LEFT") ? TxtLeftLiveTitle : TxtRightLiveTitle;

            if (banner == null || header == null || title == null) return;

            bool isActive = cardState.IsActive && _outputManager.IsOutputActive(cardId);

            if (isActive)
            {
                if (cardState.IsBlackout)
                {
                    banner.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    header.Text = "🟡 BLACKOUT: ";
                    header.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                    title.Text = string.IsNullOrEmpty(cardState.LiveAppliedLayout.MediaSource.DisplayName)
                        ? "[Siyah Ekran]"
                        : cardState.LiveAppliedLayout.MediaSource.DisplayName;
                    title.Foreground = new SolidColorBrush(Color.FromRgb(245, 158, 11));
                }
                else
                {
                    banner.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    header.Text = "🔴 YAYINDA: ";
                    header.Foreground = new SolidColorBrush(Color.FromRgb(16, 185, 129));
                    title.Text = string.IsNullOrEmpty(cardState.LiveAppliedLayout.MediaSource.DisplayName)
                        ? "[İçerik Yayında]"
                        : cardState.LiveAppliedLayout.MediaSource.DisplayName;
                    title.Foreground = Brushes.White;
                }
            }
            else
            {
                banner.BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105));
                header.Text = "⚪ SON YAYINLANAN: ";
                header.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
                title.Text = string.IsNullOrEmpty(cardState.LiveAppliedLayout.MediaSource.DisplayName)
                    ? "[Çıkış Kapalı]"
                    : $"{cardState.LiveAppliedLayout.MediaSource.DisplayName} (Çıkış Kapalı)";
                title.Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
        }
    }
}