using System;
using System.IO;

namespace RejiDisplay.Models
{
    public class MediaSource
    {
        public MediaSourceType Type { get; set; } = MediaSourceType.None;
        public string? FilePath { get; set; }
        public string DisplayName { get; set; } = string.Empty;

        public static MediaSource FromFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new MediaSource { Type = MediaSourceType.None };
            }

            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                return FromUrl(path);
            }

            string fileName = Path.GetFileName(path);
            if (fileName.StartsWith("TestPattern_", StringComparison.OrdinalIgnoreCase))
            {
                return new MediaSource
                {
                    Type = MediaSourceType.TestPattern,
                    FilePath = path,
                    DisplayName = fileName
                };
            }

            string ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".mp4" || ext == ".mov" || ext == ".mkv" || ext == ".webm" || ext == ".avi" || ext == ".wmv")
            {
                return new MediaSource
                {
                    Type = MediaSourceType.Video,
                    FilePath = path,
                    DisplayName = fileName
                };
            }

            return new MediaSource
            {
                Type = MediaSourceType.Image,
                FilePath = path,
                DisplayName = fileName
            };
        }

        public static MediaSource FromUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return new MediaSource { Type = MediaSourceType.None };
            }

            string formattedUrl = url.Trim();
            if (formattedUrl.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
            {
                formattedUrl = "https://" + formattedUrl;
            }

            string displayName = formattedUrl;
            try
            {
                if (Uri.TryCreate(formattedUrl, UriKind.Absolute, out Uri? uri))
                {
                    displayName = uri.Host;
                }
            }
            catch { }

            return new MediaSource
            {
                Type = MediaSourceType.Website,
                FilePath = formattedUrl,
                DisplayName = displayName
            };
        }

        public MediaSource Clone()
        {
            return new MediaSource
            {
                Type = this.Type,
                FilePath = this.FilePath,
                DisplayName = this.DisplayName
            };
        }
    }
}
