using System;
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
            calibration.ValidateAndClamp(TargetDisplay.Width, TargetDisplay.Height);

            Canvas.SetLeft(ViewportCanvas, calibration.ViewportX);
            Canvas.SetTop(ViewportCanvas, calibration.ViewportY);
            ViewportCanvas.Width = calibration.ViewportWidth;
            ViewportCanvas.Height = calibration.ViewportHeight;

            MediaImage.Source = bitmap;

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

            SetBlackout(isBlackout);
        }

        public void SetBlackout(bool isBlackout)
        {
            BlackoutOverlay.Visibility = isBlackout ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
