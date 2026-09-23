using Xunit;
using RejiDisplay.Models;
using RejiDisplay.Helpers;

namespace RejiDisplay.Tests
{
    public class PreviewGeometryTests
    {
        [Fact]
        public void CalculateLayoutRect_ScalesOffsetsProportionally_ForSmallControlPanelPreviewBox()
        {
            var layout = new ImageLayoutState
            {
                ScaleMode = ScaleMode.Fit,
                Zoom = 1.0,
                OffsetX = 215, // 215 pixels on physical 3840px LED display
                OffsetY = 44   // 44 pixels on physical 2160px LED display
            };

            double physicalWidth = 3840;
            double physicalHeight = 2160;

            double previewViewportWidth = 140;
            double previewViewportHeight = 170;

            // 1. Physical Output Window Rect (3840x2160 canvas, no ref scale needed)
            var physicalRect = LayoutTransformHelper.CalculateLayoutRect(1920, 1080, physicalWidth, physicalHeight, layout);

            // Verify full 215px / 44px offset is applied on 3840x2160 screen
            Assert.Equal((physicalWidth - physicalRect.Width) / 2.0 + 215, physicalRect.Left);
            Assert.Equal((physicalHeight - physicalRect.Height) / 2.0 + 44, physicalRect.Top);

            // 2. Control Panel Preview Box Rect (140x170 box, proportionally scaled to 3840x2160)
            var previewRect = LayoutTransformHelper.CalculateLayoutRect(1920, 1080, previewViewportWidth, previewViewportHeight, layout, physicalWidth, physicalHeight);

            // Expected preview scaled offset: 215 * (140 / 3840) = +7.8385 px
            double expectedPreviewOffsetX = 215.0 * (previewViewportWidth / physicalWidth);
            double expectedPreviewOffsetY = 44.0 * (previewViewportHeight / physicalHeight);

            double expectedLeft = (previewViewportWidth - previewRect.Width) / 2.0 + expectedPreviewOffsetX;
            double expectedTop = (previewViewportHeight - previewRect.Height) / 2.0 + expectedPreviewOffsetY;

            Assert.Equal(expectedLeft, previewRect.Left, precision: 3);
            Assert.Equal(expectedTop, previewRect.Top, precision: 3);

            // Verify the image stays well inside preview bounds and is not clipped off-screen
            Assert.True(previewRect.Left > -previewViewportWidth);
            Assert.True(previewRect.Left < previewViewportWidth);
            Assert.True(previewRect.Top > -previewViewportHeight);
            Assert.True(previewRect.Top < previewViewportHeight);
        }
    }
}
