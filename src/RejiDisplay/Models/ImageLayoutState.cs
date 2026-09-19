namespace RejiDisplay.Models
{
    public class ImageLayoutState
    {
        private MediaSource _mediaSource = new();

        public MediaSource MediaSource
        {
            get => _mediaSource;
            set => _mediaSource = value ?? new MediaSource();
        }

        public string? MediaPath
        {
            get => _mediaSource.FilePath;
            set
            {
                if (_mediaSource.FilePath != value)
                {
                    _mediaSource = MediaSource.FromFile(value);
                }
            }
        }

        public VideoPlaybackState VideoState { get; set; } = new();

        public ScaleMode ScaleMode { get; set; } = ScaleMode.Fit;
        public double Zoom { get; set; } = 1.0; // 1.0 = 100% (range 0.1 to 4.0)
        public double OffsetX { get; set; } = 0; // in pixels
        public double OffsetY { get; set; } = 0; // in pixels

        public ImageLayoutState Clone()
        {
            return new ImageLayoutState
            {
                MediaSource = this.MediaSource.Clone(),
                VideoState = this.VideoState.Clone(),
                ScaleMode = this.ScaleMode,
                Zoom = this.Zoom,
                OffsetX = this.OffsetX,
                OffsetY = this.OffsetY
            };
        }
    }
}
