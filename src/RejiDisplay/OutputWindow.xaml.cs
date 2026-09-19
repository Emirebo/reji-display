using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using RejiDisplay.Helpers;
using RejiDisplay.Models;

namespace RejiDisplay
{
    public partial class OutputWindow : Window
    {
        public DisplayInfo TargetDisplay { get; private set; }
        private ImageLayoutState? _currentLayout;
        private OutputCalibration? _currentCalibration;
        private bool _isWebInitialized = false;

        public OutputWindow(DisplayInfo targetDisplay)
        {
            InitializeComponent();
            TargetDisplay = targetDisplay;
            PositionOnDisplay(targetDisplay);
        }

        public void PositionOnDisplay(DisplayInfo display)
        {
            TargetDisplay = display;
            this.Left = display.Left;
            this.Top = display.Top;
            this.Width = display.Width;
            this.Height = display.Height;

            MainCanvas.Width = display.Width;
            MainCanvas.Height = display.Height;
            BlackoutOverlay.Width = display.Width;
            BlackoutOverlay.Height = display.Height;

            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, display.Left, display.Top, display.Width, display.Height, NativeMethods.SWP_SHOWWINDOW);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero && TargetDisplay != null)
            {
                NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, TargetDisplay.Left, TargetDisplay.Top, TargetDisplay.Width, TargetDisplay.Height, NativeMethods.SWP_SHOWWINDOW);
            }
        }

        public void RenderLiveAppliedState(OutputCalibration calibration, ImageLayoutState layout, BitmapImage? bitmap, bool isBlackout)
        {
            _currentCalibration = calibration;
            _currentLayout = layout;

            calibration.ValidateAndClamp(TargetDisplay.Width, TargetDisplay.Height);

            Canvas.SetLeft(ViewportCanvas, calibration.ViewportX);
            Canvas.SetTop(ViewportCanvas, calibration.ViewportY);
            ViewportCanvas.Width = calibration.ViewportWidth;
            ViewportCanvas.Height = calibration.ViewportHeight;

            bool isWebsite = (layout.MediaSource.Type == MediaSourceType.Website && !string.IsNullOrEmpty(layout.MediaSource.FilePath));
            bool isVideo = (layout.MediaSource.Type == MediaSourceType.Video && !string.IsNullOrEmpty(layout.MediaSource.FilePath));

            if (isWebsite)
            {
                MediaImage.Visibility = Visibility.Collapsed;
                MediaImage.Source = null;

                if (MediaVideo.Visibility == Visibility.Visible)
                {
                    MediaVideo.Stop();
                    MediaVideo.Source = null;
                    MediaVideo.Visibility = Visibility.Collapsed;
                }

                UpdateWebLayout();

                if (!isBlackout)
                {
                    MediaWeb.Visibility = Visibility.Visible;
                }

                string targetUrl = layout.MediaSource.FilePath!;
                _ = NavigateWebAsync(targetUrl, layout.WebsiteState);
            }
            else if (isVideo)
            {
                MediaWeb.Visibility = Visibility.Collapsed;

                MediaImage.Visibility = Visibility.Collapsed;
                MediaImage.Source = null;

                MediaVideo.Visibility = Visibility.Visible;
                MediaVideo.IsMuted = layout.VideoState.IsMuted;
                MediaVideo.Volume = layout.VideoState.Volume;

                string targetPath = layout.MediaSource.FilePath!;
                if (MediaVideo.Source == null || MediaVideo.Source.LocalPath != targetPath)
                {
                    try
                    {
                        MediaVideo.Source = new Uri(targetPath, UriKind.Absolute);
                    }
                    catch
                    {
                        MediaVideo.Visibility = Visibility.Collapsed;
                    }
                }

                UpdateVideoLayout();

                if (layout.VideoState.Status == PlaybackStatus.Playing)
                {
                    MediaVideo.Play();
                }
                else if (layout.VideoState.Status == PlaybackStatus.Paused)
                {
                    MediaVideo.Pause();
                }
                else
                {
                    MediaVideo.Stop();
                }
            }
            else
            {
                MediaWeb.Visibility = Visibility.Collapsed;

                if (MediaVideo.Visibility == Visibility.Visible)
                {
                    MediaVideo.Stop();
                    MediaVideo.Source = null;
                    MediaVideo.Visibility = Visibility.Collapsed;
                }

                MediaImage.Source = bitmap;
                MediaImage.Visibility = Visibility.Visible;

                if (bitmap != null && bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
                {
                    var rect = LayoutTransformHelper.CalculateLayoutRect(
                        bitmap.PixelWidth,
                        bitmap.PixelHeight,
                        calibration.ViewportWidth,
                        calibration.ViewportHeight,
                        layout);

                    MediaImage.Width = rect.Width;
                    MediaImage.Height = rect.Height;
                    Canvas.SetLeft(MediaImage, rect.Left);
                    Canvas.SetTop(MediaImage, rect.Top);
                    MediaImage.RenderTransform = Transform.Identity;
                }
                else
                {
                    MediaImage.Width = 0;
                    MediaImage.Height = 0;
                    MediaImage.RenderTransform = Transform.Identity;
                }
            }

            SetBlackout(isBlackout);
        }

        private async Task EnsureWebInitializedAsync()
        {
            if (!_isWebInitialized)
            {
                try
                {
                    await MediaWeb.EnsureCoreWebView2Async();
                    if (MediaWeb.CoreWebView2 != null)
                    {
                        MediaWeb.CoreWebView2.IsMuted = true;
                        MediaWeb.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                        MediaWeb.CoreWebView2.Settings.IsStatusBarEnabled = false;
                    }
                    _isWebInitialized = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"WebView2 initialization failed: {ex.Message}");
                }
            }
        }

        private async Task NavigateWebAsync(string url, WebsiteState state)
        {
            await EnsureWebInitializedAsync();
            if (MediaWeb.CoreWebView2 != null)
            {
                try
                {
                    MediaWeb.CoreWebView2.IsMuted = state.IsMuted;
                    if (state.ZoomFactor > 0)
                    {
                        MediaWeb.ZoomFactor = state.ZoomFactor;
                    }

                    if (MediaWeb.Source == null || MediaWeb.Source.AbsoluteUri != url)
                    {
                        MediaWeb.Source = new Uri(url);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"NavigateWebAsync error: {ex.Message}");
                }
            }
        }

        private void UpdateWebLayout()
        {
            if (_currentCalibration == null || _currentLayout == null) return;

            double srcW = _currentCalibration.LogicalLedWidth > 0 ? _currentCalibration.LogicalLedWidth : _currentCalibration.ViewportWidth;
            double srcH = _currentCalibration.LogicalLedHeight > 0 ? _currentCalibration.LogicalLedHeight : _currentCalibration.ViewportHeight;

            var rect = LayoutTransformHelper.CalculateLayoutRect(
                srcW,
                srcH,
                _currentCalibration.ViewportWidth,
                _currentCalibration.ViewportHeight,
                _currentLayout);

            MediaWeb.Width = rect.Width;
            MediaWeb.Height = rect.Height;
            Canvas.SetLeft(MediaWeb, rect.Left);
            Canvas.SetTop(MediaWeb, rect.Top);
        }

        private void UpdateVideoLayout()
        {
            if (_currentCalibration == null || _currentLayout == null) return;

            double vidW = MediaVideo.NaturalVideoWidth > 0 ? MediaVideo.NaturalVideoWidth : 1920;
            double vidH = MediaVideo.NaturalVideoHeight > 0 ? MediaVideo.NaturalVideoHeight : 1080;

            var rect = LayoutTransformHelper.CalculateLayoutRect(
                vidW,
                vidH,
                _currentCalibration.ViewportWidth,
                _currentCalibration.ViewportHeight,
                _currentLayout);

            MediaVideo.Width = rect.Width;
            MediaVideo.Height = rect.Height;
            Canvas.SetLeft(MediaVideo, rect.Left);
            Canvas.SetTop(MediaVideo, rect.Top);
        }

        private void MediaVideo_MediaOpened(object sender, RoutedEventArgs e)
        {
            UpdateVideoLayout();
            if (_currentLayout?.VideoState.Status == PlaybackStatus.Playing)
            {
                MediaVideo.Play();
            }
        }

        private void MediaVideo_MediaEnded(object sender, RoutedEventArgs e)
        {
            if (_currentLayout != null && _currentLayout.VideoState.IsLooping)
            {
                MediaVideo.Position = TimeSpan.Zero;
                MediaVideo.Play();
            }
        }

        private void MediaVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            MediaVideo.Visibility = Visibility.Collapsed;
        }

        public void SetBlackout(bool isBlackout)
        {
            BlackoutOverlay.Visibility = isBlackout ? Visibility.Visible : Visibility.Collapsed;

            if (_currentLayout?.MediaSource.Type == MediaSourceType.Website)
            {
                // Airspace protection: WebView2 is an HWND host. Hide it during Blackout so it does not paint over BlackoutOverlay!
                MediaWeb.Visibility = (isBlackout || _currentLayout.MediaSource.Type != MediaSourceType.Website)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }

        public void CleanUp()
        {
            try
            {
                MediaVideo.Stop();
                MediaVideo.Source = null;
                MediaImage.Source = null;

                if (MediaWeb != null)
                {
                    MediaWeb.Dispose();
                }
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            CleanUp();
            base.OnClosed(e);
        }
    }
}
