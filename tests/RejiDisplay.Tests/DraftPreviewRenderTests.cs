using Xunit;
using RejiDisplay.Models;
using RejiDisplay.Helpers;

namespace RejiDisplay.Tests
{
    public class DraftPreviewRenderTests
    {
        [Fact]
        public void CalculateLayoutRect_CalculatesValidViewportBounds_ForImages()
        {
            var layout = new ImageLayoutState
            {
                ScaleMode = ScaleMode.Fit,
                Zoom = 1.0,
                OffsetX = 0,
                OffsetY = 0
            };

            // Image size 1920x1080, viewport size 140x170
            var rect = LayoutTransformHelper.CalculateLayoutRect(1920, 1080, 140, 170, layout);

            Assert.True(rect.Width > 0);
            Assert.True(rect.Height > 0);
            Assert.True(rect.Width <= 140.1);
        }

        [Fact]
        public void CalculateLayoutRect_ReflectsZoomAndOffset_InDraftPreview()
        {
            var layoutOriginal = new ImageLayoutState
            {
                ScaleMode = ScaleMode.Fit,
                Zoom = 1.0,
                OffsetX = 0,
                OffsetY = 0
            };

            var layoutZoomed = new ImageLayoutState
            {
                ScaleMode = ScaleMode.Fit,
                Zoom = 1.5,
                OffsetX = 25,
                OffsetY = -10
            };

            var rectOriginal = LayoutTransformHelper.CalculateLayoutRect(1920, 1080, 140, 170, layoutOriginal);
            var rectZoomed = LayoutTransformHelper.CalculateLayoutRect(1920, 1080, 140, 170, layoutZoomed);

            // Verify zoomed rect has larger dimensions and shifted coordinates
            Assert.True(rectZoomed.Width > rectOriginal.Width);
            Assert.True(rectZoomed.Height > rectOriginal.Height);
            Assert.NotEqual(rectOriginal.Left, rectZoomed.Left);
            Assert.NotEqual(rectOriginal.Top, rectZoomed.Top);
        }

        [Fact]
        public void IndependentCardLayouts_DoNotMutateEachOther()
        {
            var leftCard = new OutputCardState
            {
                CardId = "LEFT",
                DraftLayout = new ImageLayoutState { Zoom = 1.0, OffsetX = 0 }
            };

            var rightCard = new OutputCardState
            {
                CardId = "RIGHT",
                DraftLayout = new ImageLayoutState { Zoom = 2.0, OffsetX = 50 }
            };

            // Adjust LEFT layout
            leftCard.DraftLayout.Zoom = 1.2;
            leftCard.DraftLayout.OffsetX = 10;

            // Verify RIGHT layout is completely isolated
            Assert.Equal(2.0, rightCard.DraftLayout.Zoom);
            Assert.Equal(50, rightCard.DraftLayout.OffsetX);
        }
    }
}
