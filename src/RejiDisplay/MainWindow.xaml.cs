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

        public MainWindow()
        {
            _isInitializing = true;
            _displayService = new DisplayService();
            _settingsService = new SettingsService();
            _outputManager = new OutputManager();
            _venuePresetService = new VenuePresetService();

            InitializeComponent();

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

                if (!string.IsNullOrEmpty(_leftState.DraftLayout.MediaPath))
                {
                    LoadImageForCard("LEFT", _leftState, _leftState.DraftLayout.MediaPath, ImgLeftPreview, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError);
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

                if (!string.IsNullOrEmpty(_rightState.DraftLayout.MediaPath))
                {
                    LoadImageForCard("RIGHT", _rightState, _rightState.DraftLayout.MediaPath, ImgRightPreview, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError);
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
            var previewCanvas = (cardId == "LEFT") ? CanvasLeftPreviewViewport : CanvasRightPreviewViewport;

            if (previewImg == null || previewCanvas == null) return;

            if (previewImg.Source is BitmapImage bitmap && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
            {
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
                    GenerateAndApplyTestPattern("LEFT", _leftState, ImgLeftPreview, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, BtnLeftRestoreMedia);
                }
                else
                {
                    RenderDraftPreview("LEFT");
                }

                if (!string.IsNullOrEmpty(_rightState.DraftLayout.MediaPath) && _rightState.DraftLayout.MediaPath.Contains("TestPattern_"))
                {
                    GenerateAndApplyTestPattern("RIGHT", _rightState, ImgRightPreview, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, BtnRightRestoreMedia);
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
            HandleFileDrop("LEFT", _leftState, e, ImgLeftPreview, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError);
        }

        private void DropZoneRight_Drop(object sender, DragEventArgs e)
        {
            HandleFileDrop("RIGHT", _rightState, e, ImgRightPreview, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError);
        }

        private void HandleFileDrop(
            string cardId,
            OutputCardState cardState,
            DragEventArgs e,
            Image previewImg,
            StackPanel promptPanel,
            TextBlock mediaPathTxt,
            TextBlock errorTxt)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    string filePath = files[0];
                    if (LoadImageForCard(cardId, cardState, filePath, previewImg, promptPanel, mediaPathTxt, errorTxt))
                    {
                        cardState.DraftLayout.MediaPath = filePath;
                        SaveAppSettings();
                        RenderDraftPreview(cardId);

                        if (cardState.IsLiveUpdateEnabled && _outputManager.IsOutputActive(cardId))
                        {
                            ApplyDraftToLive(cardId);
                        }
                    }
                }
            }
        }

        private bool LoadImageForCard(
            string cardId,
            OutputCardState cardState,
            string filePath,
            Image previewImg,
            StackPanel promptPanel,
            TextBlock mediaPathTxt,
            TextBlock errorTxt)
        {
            if (!ImageValidationHelper.ValidateImageFile(filePath, out string err))
            {
                errorTxt.Text = $"HATA: {err}";
                errorTxt.Visibility = Visibility.Visible;
                return false;
            }

            try
            {
                var bitmap = CreateBitmap(filePath);
                if (bitmap != null)
                {
                    cardState.DraftLayout.MediaPath = filePath;
                    previewImg.Source = bitmap;
                    previewImg.Visibility = Visibility.Visible;
                    promptPanel.Visibility = Visibility.Collapsed;
                    mediaPathTxt.Text = Path.GetFileName(filePath);
                    errorTxt.Visibility = Visibility.Collapsed;
                    RenderDraftPreview(cardId);
                    return true;
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
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        // --- Test Pattern Handlers ---

        private void BtnLeftTestPattern_Click(object sender, RoutedEventArgs e)
        {
            GenerateAndApplyTestPattern("LEFT", _leftState, ImgLeftPreview, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, BtnLeftRestoreMedia);
        }

        private void BtnRightTestPattern_Click(object sender, RoutedEventArgs e)
        {
            GenerateAndApplyTestPattern("RIGHT", _rightState, ImgRightPreview, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, BtnRightRestoreMedia);
        }

        private void GenerateAndApplyTestPattern(
            string cardId,
            OutputCardState cardState,
            Image previewImg,
            StackPanel promptPanel,
            TextBlock mediaPathTxt,
            TextBlock errorTxt,
            Button restoreBtn)
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
                if (LoadImageForCard(cardId, cardState, patternPath, previewImg, promptPanel, mediaPathTxt, errorTxt))
                {
                    cardState.DraftLayout.MediaPath = patternPath;
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
            RestorePreviousMedia("LEFT", _leftState, ImgLeftPreview, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, BtnLeftRestoreMedia);
        }

        private void BtnRightRestoreMedia_Click(object sender, RoutedEventArgs e)
        {
            RestorePreviousMedia("RIGHT", _rightState, ImgRightPreview, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, BtnRightRestoreMedia);
        }

        private void RestorePreviousMedia(
            string cardId,
            OutputCardState cardState,
            Image previewImg,
            StackPanel promptPanel,
            TextBlock mediaPathTxt,
            TextBlock errorTxt,
            Button restoreBtn)
        {
            if (_isInitializing || _isUpdatingUI) return;

            if (!string.IsNullOrEmpty(cardState.PreviousMediaPath) && File.Exists(cardState.PreviousMediaPath))
            {
                string path = cardState.PreviousMediaPath;
                cardState.PreviousMediaPath = null;
                restoreBtn.Visibility = Visibility.Collapsed;

                if (LoadImageForCard(cardId, cardState, path, previewImg, promptPanel, mediaPathTxt, errorTxt))
                {
                    cardState.DraftLayout.MediaPath = path;
                    SaveAppSettings();
                    RenderDraftPreview(cardId);

                    if (cardState.IsLiveUpdateEnabled && _outputManager.IsOutputActive(cardId))
                    {
                        ApplyDraftToLive(cardId);
                    }

                    TxtGlobalStatus.Text = $"{cardId} LED için önceki görsel geri yüklendi.";
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
            SaveAppSettings();

            var cardState = (cardId == "LEFT") ? _leftState : _rightState;
            if (cardState.IsLiveUpdateEnabled && _outputManager.IsOutputActive(cardId))
            {
                ApplyDraftToLive(cardId);
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

            // Atomically copy DraftLayout and DraftCalibration to LiveApplied state
            cardState.LiveAppliedLayout = cardState.DraftLayout.Clone();
            cardState.LiveAppliedCalibration = cardState.DraftCalibration.Clone();

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

            // Ensure live applied calibration is validated against GPU signal bounds
            cardState.LiveAppliedCalibration.ValidateAndClamp(display.Width, display.Height);

            BitmapImage? bitmap = null;
            if (!string.IsNullOrEmpty(cardState.LiveAppliedLayout.MediaPath))
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
                TxtGlobalStatus.Text = $"{cardId} LED yayını güncellendi ({display.FriendlyName}).";
            }
            catch (Exception ex)
            {
                errorTxt.Text = $"Çıkış yayını uygulanamadı: {ex.Message}";
                errorTxt.Visibility = Visibility.Visible;
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
                GenerateAndApplyTestPattern("LEFT", _leftState, ImgLeftPreview, PanelLeftDropPrompt, TxtLeftMediaPath, TxtLeftError, BtnLeftRestoreMedia);
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
                GenerateAndApplyTestPattern("RIGHT", _rightState, ImgRightPreview, PanelRightDropPrompt, TxtRightMediaPath, TxtRightError, BtnRightRestoreMedia);
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

        private void SetCardStatus(OutputCardState state, Border badge, TextBlock badgeTxt, string statusText, Color color)
        {
            state.StatusText = statusText;
            badgeTxt.Text = statusText;
            badge.Background = new SolidColorBrush(color) { Opacity = 0.85 };
            badgeTxt.Foreground = Brushes.White;
        }
    }
}