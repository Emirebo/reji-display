using System;
using System.Windows;
using System.Windows.Media;
using RejiDisplay.Models;

namespace RejiDisplay.Helpers
{
    public static class LayoutTransformHelper
    {
        public static TransformGroup CalculateTransform(
            double imgWidth,
            double imgHeight,
            double viewportWidth,
            double viewportHeight,
            ImageLayoutState layout)
        {
            var transformGroup = new TransformGroup();

            if (imgWidth <= 0 || imgHeight <= 0 || viewportWidth <= 0 || viewportHeight <= 0)
            {
                return transformGroup;
            }

            double baseScaleX = 1.0;
            double baseScaleY = 1.0;

            switch (layout.ScaleMode)
            {
                case ScaleMode.Fit:
                    double fitRatio = Math.Min(viewportWidth / imgWidth, viewportHeight / imgHeight);
                    baseScaleX = fitRatio;
                    baseScaleY = fitRatio;
                    break;

                case ScaleMode.Fill:
                    double fillRatio = Math.Max(viewportWidth / imgWidth, viewportHeight / imgHeight);
                    baseScaleX = fillRatio;
                    baseScaleY = fillRatio;
                    break;

                case ScaleMode.Stretch:
                    baseScaleX = viewportWidth / imgWidth;
                    baseScaleY = viewportHeight / imgHeight;
                    break;

                case ScaleMode.Custom:
                    double customFit = Math.Min(viewportWidth / imgWidth, viewportHeight / imgHeight);
                    baseScaleX = customFit;
                    baseScaleY = customFit;
                    break;
            }

            double finalZoom = Math.Max(0.1, Math.Min(4.0, layout.Zoom));
            double scaleX = baseScaleX * finalZoom;
            double scaleY = baseScaleY * finalZoom;

            var scaleTransform = new ScaleTransform(scaleX, scaleY);
            var translateTransform = new TranslateTransform(layout.OffsetX, layout.OffsetY);

            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);

            return transformGroup;
        }
    }
}
