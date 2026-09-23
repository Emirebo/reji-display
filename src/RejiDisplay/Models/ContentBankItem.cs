using System;
using System.IO;

namespace RejiDisplay.Models
{
    public class ContentBankItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public MediaSource MediaSource { get; set; } = new();
        public WebsiteState WebsiteState { get; set; } = new();
        public VideoPlaybackState VideoState { get; set; } = new();
        public ScaleMode ScaleMode { get; set; } = ScaleMode.Fit;
        public double Zoom { get; set; } = 1.0;
        public double OffsetX { get; set; } = 0;
        public double OffsetY { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool CheckFileExists(out string? missingPath)
        {
            missingPath = null;
            if (MediaSource.Type == MediaSourceType.Image || MediaSource.Type == MediaSourceType.Video)
            {
                if (!string.IsNullOrEmpty(MediaSource.FilePath) && !File.Exists(MediaSource.FilePath))
                {
                    missingPath = MediaSource.FilePath;
                    return false;
                }
            }
            return true;
        }

        public string GetIcon()
        {
            return MediaSource.Type switch
            {
                MediaSourceType.Video => "🎬",
                MediaSourceType.Website => "🌐",
                _ => (MediaSource.FilePath != null && MediaSource.FilePath.Contains("TestPattern_")) ? "📐" : "🖼️"
            };
        }

        public ImageLayoutState ToLayoutState()
        {
            return new ImageLayoutState
            {
                MediaPath = MediaSource.FilePath,
                MediaSource = MediaSource.Clone(),
                WebsiteState = WebsiteState.Clone(),
                VideoState = VideoState.Clone(),
                ScaleMode = ScaleMode,
                Zoom = Zoom,
                OffsetX = OffsetX,
                OffsetY = OffsetY
            };
        }

        public static ContentBankItem FromLayoutState(string title, ImageLayoutState layout)
        {
            return new ContentBankItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = title,
                MediaSource = layout.MediaSource.Clone(),
                WebsiteState = layout.WebsiteState.Clone(),
                VideoState = layout.VideoState.Clone(),
                ScaleMode = layout.ScaleMode,
                Zoom = layout.Zoom,
                OffsetX = layout.OffsetX,
                OffsetY = layout.OffsetY,
                CreatedAt = DateTime.Now
            };
        }
    }
}
