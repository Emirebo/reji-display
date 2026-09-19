namespace RejiDisplay.Models
{
    public class ImageLayoutState
    {
        public string? MediaPath { get; set; }
        public ScaleMode ScaleMode { get; set; } = ScaleMode.Fit;
        public double Zoom { get; set; } = 1.0; // 1.0 = 100% (range 0.1 to 4.0)
        public double OffsetX { get; set; } = 0; // in pixels
        public double OffsetY { get; set; } = 0; // in pixels

        public ImageLayoutState Clone()
        {
            return new ImageLayoutState
            {
                MediaPath = this.MediaPath,
                ScaleMode = this.ScaleMode,
                Zoom = this.Zoom,
                OffsetX = this.OffsetX,
                OffsetY = this.OffsetY
            };
        }
    }
}
