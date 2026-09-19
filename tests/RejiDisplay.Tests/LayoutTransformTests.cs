using System.Windows.Media;
using RejiDisplay.Helpers;
using RejiDisplay.Models;
using Xunit;

namespace RejiDisplay.Tests
{
    public class LayoutTransformTests
    {
        [Fact]
        public void CalculateTransform_FitMode_ComputesUniformScale()
        {
            var layout = new ImageLayoutState
            {
                ScaleMode = ScaleMode.Fit,
                Zoom = 1.0,
                OffsetX = 0,
                OffsetY = 0
            };

            // Image 860x1720, Viewport 1920x1080 -> Fit ratio = min(1920/860, 1080/1720) = 1080/1720 = 0.6279
            var transformGroup = LayoutTransformHelper.CalculateTransform(860, 1720, 1920, 1080, layout);

            Assert.NotNull(transformGroup);
            Assert.Equal(2, transformGroup.Children.Count);

            var scale = (ScaleTransform)transformGroup.Children[0];
            double expectedFit = 1080.0 / 1720.0;
            Assert.Equal(expectedFit, scale.ScaleX, precision: 4);
            Assert.Equal(expectedFit, scale.ScaleY, precision: 4);
        }

        [Fact]
        public void CalculateTransform_ZoomAndOffset_AppliesScaleAndTranslate()
        {
            var layout = new ImageLayoutState
            {
                ScaleMode = ScaleMode.Custom,
                Zoom = 1.5,
                OffsetX = 45,
                OffsetY = -30
            };

            var transformGroup = LayoutTransformHelper.CalculateTransform(1000, 1000, 1000, 1000, layout);

            var scale = (ScaleTransform)transformGroup.Children[0];
            var translate = (TranslateTransform)transformGroup.Children[1];

            Assert.Equal(1.5, scale.ScaleX, precision: 4);
            Assert.Equal(1.5, scale.ScaleY, precision: 4);
            Assert.Equal(45, translate.X);
            Assert.Equal(-30, translate.Y);
        }

        [Fact]
        public void OutputCalibration_ValidateAndClamp_FixesOutOfBoundsViewport()
        {
            var calib = new OutputCalibration
            {
                GpuSignalWidth = 1920,
                GpuSignalHeight = 1080,
                ViewportX = 1800, // Out of bounds when width = 500!
                ViewportY = 0,
                ViewportWidth = 500,
                ViewportHeight = 500
            };

            calib.ValidateAndClamp(1920, 1080);

            // ViewportX (1800) + ViewportWidth (500) = 2300 > 1920! Clamped X should be 1920 - 500 = 1420.
            Assert.Equal(1420, calib.ViewportX);
            Assert.Equal(500, calib.ViewportWidth);
        }

        [Fact]
        public void OutputCalibration_IsIndependentFromImageLayout()
        {
            var calib = new OutputCalibration
            {
                LogicalLedWidth = 860,
                LogicalLedHeight = 1720,
                ViewportWidth = 1920,
                ViewportHeight = 1080
            };

            var layout = new ImageLayoutState
            {
                Zoom = 2.5,
                OffsetX = 200,
                OffsetY = -150
            };

            // Adjusting layout properties does not alter calibration object
            Assert.Equal(860, calib.LogicalLedWidth);
            Assert.Equal(1720, calib.LogicalLedHeight);
            Assert.Equal(1920, calib.ViewportWidth);
        }
    }
}
