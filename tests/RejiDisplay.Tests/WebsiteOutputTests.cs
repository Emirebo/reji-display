using System;
using RejiDisplay.Helpers;
using RejiDisplay.Models;
using Xunit;

namespace RejiDisplay.Tests
{
    public class WebsiteOutputTests
    {
        [Theory]
        [InlineData("https://subtitles.live.com", MediaSourceType.Website)]
        [InlineData("http://translation.event.org/stream", MediaSourceType.Website)]
        [InlineData("www.google.com", MediaSourceType.Website)]
        [InlineData("C:\\media\\test.mp4", MediaSourceType.Video)]
        [InlineData("C:\\media\\test.png", MediaSourceType.Image)]
        public void MediaSource_FromUrlAndFile_DetectsWebsiteTypeCorrectly(string pathOrUrl, MediaSourceType expectedType)
        {
            var media = MediaSource.FromFile(pathOrUrl);
            Assert.Equal(expectedType, media.Type);
        }

        [Fact]
        public void MediaSource_FromUrl_FormatsUrlAndExtractsDisplayName()
        {
            var media = MediaSource.FromUrl("www.example.com/live");
            Assert.Equal(MediaSourceType.Website, media.Type);
            Assert.Equal("https://www.example.com/live", media.FilePath);
            Assert.Equal("www.example.com", media.DisplayName);
        }

        [Theory]
        [InlineData("https://subtitles.event.org", true, "https://subtitles.event.org/")]
        [InlineData("http://localhost:8080/stream", true, "http://localhost:8080/stream")]
        [InlineData("www.live-caption.com", true, "https://www.live-caption.com/")]
        [InlineData("", false, "")]
        [InlineData("   ", false, "")]
        public void MediaValidationHelper_ValidateUrl_ValidatesAndFormatsUrls(string inputUrl, bool expectedValid, string expectedFormattedUrlPrefix)
        {
            bool isValid = MediaValidationHelper.ValidateUrl(inputUrl, out string formattedUrl, out string errorMessage);
            Assert.Equal(expectedValid, isValid);

            if (expectedValid)
            {
                Assert.StartsWith(expectedFormattedUrlPrefix, formattedUrl, StringComparison.OrdinalIgnoreCase);
                Assert.True(string.IsNullOrEmpty(errorMessage));
            }
            else
            {
                Assert.False(string.IsNullOrEmpty(errorMessage));
            }
        }

        [Fact]
        public void WebsiteState_DefaultsAreMutedForLedSafety()
        {
            var state = new WebsiteState();

            Assert.True(state.IsMuted, "Website audio must be MUTED BY DEFAULT for venue PA safety");
            Assert.Equal(1.0, state.ZoomFactor);
            Assert.Equal("Ready", state.StatusText);
        }

        [Fact]
        public void DraftVsAppliedUrl_SelectionDoesNotAlterLiveOutput()
        {
            var card = new OutputCardState
            {
                CardId = "LEFT",
                DraftLayout = new ImageLayoutState { MediaSource = MediaSource.FromUrl("https://live.event.com/ch1") },
                LiveAppliedLayout = new ImageLayoutState { MediaSource = MediaSource.FromUrl("https://live.event.com/ch1") }
            };

            // Operator types a new draft URL
            card.DraftLayout.MediaSource = MediaSource.FromUrl("https://live.event.com/ch2_draft");

            // Live applied output remains unchanged
            Assert.Equal("https://live.event.com/ch1", card.LiveAppliedLayout.MediaSource.FilePath);

            // Draft reflects new URL
            Assert.Equal("https://live.event.com/ch2_draft", card.DraftLayout.MediaSource.FilePath);
            Assert.Equal(MediaSourceType.Website, card.DraftLayout.MediaSource.Type);
        }

        [Fact]
        public void IndependentOutputs_LeftAndRightWebsitesAreIsolated()
        {
            var leftCard = new OutputCardState { CardId = "LEFT" };
            var rightCard = new OutputCardState { CardId = "RIGHT" };

            leftCard.DraftLayout.MediaSource = MediaSource.FromUrl("https://subtitles.org/english");
            leftCard.DraftLayout.WebsiteState.IsMuted = true;

            rightCard.DraftLayout.MediaSource = MediaSource.FromUrl("https://subtitles.org/turkish");
            rightCard.DraftLayout.WebsiteState.IsMuted = true;

            Assert.Equal("https://subtitles.org/english", leftCard.DraftLayout.MediaSource.FilePath);
            Assert.Equal("https://subtitles.org/turkish", rightCard.DraftLayout.MediaSource.FilePath);

            Assert.NotEqual(leftCard.DraftLayout.MediaSource.FilePath, rightCard.DraftLayout.MediaSource.FilePath);
        }

        [Fact]
        public void ImageLayoutState_Clone_ClonesWebsiteState()
        {
            var original = new ImageLayoutState
            {
                MediaSource = MediaSource.FromUrl("https://interpreter.com/stream"),
                WebsiteState = new WebsiteState { IsMuted = true, ZoomFactor = 1.25, StatusText = "Connected" }
            };

            var cloned = original.Clone();

            Assert.Equal(original.MediaSource.FilePath, cloned.MediaSource.FilePath);
            Assert.Equal(original.MediaSource.Type, cloned.MediaSource.Type);
            Assert.Equal(original.WebsiteState.IsMuted, cloned.WebsiteState.IsMuted);
            Assert.Equal(original.WebsiteState.ZoomFactor, cloned.WebsiteState.ZoomFactor);

            // Modify clone
            cloned.WebsiteState.Url = "https://different.com";
            Assert.NotEqual(original.WebsiteState.Url, cloned.WebsiteState.Url);
        }
    }
}
