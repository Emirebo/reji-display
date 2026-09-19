using System;

namespace RejiDisplay.Models
{
    public class OutputCalibration
    {
        public int GpuSignalWidth { get; set; } = 1920;
        public int GpuSignalHeight { get; set; } = 1080;

        public int LogicalLedWidth { get; set; } = 860;
        public int LogicalLedHeight { get; set; } = 1720;

        public int ViewportX { get; set; } = 0;
        public int ViewportY { get; set; } = 0;
        public int ViewportWidth { get; set; } = 1920;
        public int ViewportHeight { get; set; } = 1080;

        public bool IsVerifiedPhysicalMapping { get; set; } = false;

        public void ValidateAndClamp(int currentSignalWidth, int currentSignalHeight)
        {
            if (currentSignalWidth > 0) GpuSignalWidth = currentSignalWidth;
            if (currentSignalHeight > 0) GpuSignalHeight = currentSignalHeight;

            if (ViewportWidth <= 0 || ViewportWidth > GpuSignalWidth)
                ViewportWidth = GpuSignalWidth;

            if (ViewportHeight <= 0 || ViewportHeight > GpuSignalHeight)
                ViewportHeight = GpuSignalHeight;

            if (ViewportX < 0) ViewportX = 0;
            if (ViewportX + ViewportWidth > GpuSignalWidth)
                ViewportX = Math.Max(0, GpuSignalWidth - ViewportWidth);

            if (ViewportY < 0) ViewportY = 0;
            if (ViewportY + ViewportHeight > GpuSignalHeight)
                ViewportY = Math.Max(0, GpuSignalHeight - ViewportHeight);
        }

        public OutputCalibration Clone()
        {
            return new OutputCalibration
            {
                GpuSignalWidth = this.GpuSignalWidth,
                GpuSignalHeight = this.GpuSignalHeight,
                LogicalLedWidth = this.LogicalLedWidth,
                LogicalLedHeight = this.LogicalLedHeight,
                ViewportX = this.ViewportX,
                ViewportY = this.ViewportY,
                ViewportWidth = this.ViewportWidth,
                ViewportHeight = this.ViewportHeight,
                IsVerifiedPhysicalMapping = this.IsVerifiedPhysicalMapping
            };
        }
    }
}
