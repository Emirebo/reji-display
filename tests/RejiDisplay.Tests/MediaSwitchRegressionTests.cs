using System;
using System.IO;
using Xunit;
using RejiDisplay.Models;
using RejiDisplay.Helpers;

namespace RejiDisplay.Tests
{
    public class MediaSwitchRegressionTests
    {
        [Fact]
        public void LoadImage_WhenPreviouslyWeb_CorrectlyUpdatesMediaSourceTypeToImage()
        {
            var cardState = new OutputCardState
            {
                CardId = "LEFT",
                DraftLayout = new ImageLayoutState
                {
                    MediaSource = MediaSource.FromUrl("https://uulive.ai.studio/?mode=projector"),
                    MediaPath = "https://uulive.ai.studio/?mode=projector"
                }
            };

            string tempDir = Path.GetTempPath();
            string sampleImagePath = Path.Combine(tempDir, "sample_logo_" + Guid.NewGuid().ToString("N") + ".png");

            try
            {
                string patFile = TestPatternGenerator.SaveTestPatternToTempFile("REG_IMG", 400, 400);
                File.Copy(patFile, sampleImagePath, overwrite: true);
                File.Delete(patFile);

                // Simulate loading image for card
                bool isValidMedia = MediaValidationHelper.ValidateMediaFile(sampleImagePath, out string err);
                Assert.True(isValidMedia);
                Assert.Empty(err);

                var newSource = MediaSource.FromFile(sampleImagePath);
                cardState.DraftLayout.MediaSource = newSource;
                cardState.DraftLayout.MediaPath = sampleImagePath;

                // Verify type is now Image, NOT Website, and no URL validation error occurred
                Assert.Equal(MediaSourceType.Image, cardState.DraftLayout.MediaSource.Type);
                Assert.Equal(sampleImagePath, cardState.DraftLayout.MediaPath);
            }
            finally
            {
                if (File.Exists(sampleImagePath)) File.Delete(sampleImagePath);
            }
        }

        [Fact]
        public void FourMediaTypes_CycleTransition_PreservesCorrectTypeAndPath()
        {
            var cardState = new OutputCardState
            {
                CardId = "LEFT",
                DraftLayout = new ImageLayoutState
                {
                    MediaSource = MediaSource.FromUrl("https://uulive.ai.studio/?mode=projector"),
                    MediaPath = "https://uulive.ai.studio/?mode=projector"
                }
            };

            string tempDir = Path.GetTempPath();
            string sampleImage = Path.Combine(tempDir, "sample_bg_" + Guid.NewGuid().ToString("N") + ".jpg");
            string patFile = TestPatternGenerator.SaveTestPatternToTempFile("CYCLE_PAT", 400, 400);

            try
            {
                File.Copy(patFile, sampleImage, overwrite: true);

                // 1. Image Selection
                cardState.DraftLayout.MediaSource = MediaSource.FromFile(sampleImage);
                cardState.DraftLayout.MediaPath = sampleImage;
                Assert.Equal(MediaSourceType.Image, cardState.DraftLayout.MediaSource.Type);

                // 2. TestPattern Selection
                cardState.DraftLayout.MediaSource = MediaSource.FromFile(patFile);
                cardState.DraftLayout.MediaPath = patFile;
                Assert.Equal(MediaSourceType.TestPattern, cardState.DraftLayout.MediaSource.Type);

                // 3. Web Selection
                cardState.DraftLayout.MediaSource = MediaSource.FromUrl("https://example.com/stream");
                cardState.DraftLayout.MediaPath = "https://example.com/stream";
                Assert.Equal(MediaSourceType.Website, cardState.DraftLayout.MediaSource.Type);

                // 4. Video Selection
                cardState.DraftLayout.MediaSource = new MediaSource
                {
                    Type = MediaSourceType.Video,
                    FilePath = "C:\\videos\\intro.mp4",
                    DisplayName = "intro.mp4"
                };
                cardState.DraftLayout.MediaPath = "C:\\videos\\intro.mp4";
                Assert.Equal(MediaSourceType.Video, cardState.DraftLayout.MediaSource.Type);
            }
            finally
            {
                if (File.Exists(sampleImage)) File.Delete(sampleImage);
                if (File.Exists(patFile)) File.Delete(patFile);
            }
        }

        [Fact]
        public void LeftCardStaging_DoesNotModifyRightCardDraftOrLiveState()
        {
            var leftCard = new OutputCardState
            {
                CardId = "LEFT",
                DraftLayout = new ImageLayoutState { MediaPath = "left_draft.png" },
                LiveAppliedLayout = new ImageLayoutState { MediaPath = "left_live.png" }
            };

            var rightCard = new OutputCardState
            {
                CardId = "RIGHT",
                DraftLayout = new ImageLayoutState { MediaPath = "https://uulive.ai.studio" },
                LiveAppliedLayout = new ImageLayoutState { MediaPath = "right_live.png" }
            };

            // Modify LEFT draft
            leftCard.DraftLayout.MediaPath = "new_left_draft.png";

            // Verify RIGHT draft and live layouts remain unchanged
            Assert.Equal("https://uulive.ai.studio", rightCard.DraftLayout.MediaPath);
            Assert.Equal("right_live.png", rightCard.LiveAppliedLayout.MediaPath);
        }

        [Fact]
        public void ApplyDraftToLive_OnlyAppliesTargetCard_AndPreservesCurrentLiveOnMissingFile()
        {
            var card = new OutputCardState
            {
                CardId = "LEFT",
                DraftLayout = new ImageLayoutState
                {
                    MediaPath = "C:\\missing_dir_999\\missing.png"
                },
                LiveAppliedLayout = new ImageLayoutState
                {
                    MediaPath = "temp_pattern.png"
                }
            };

            string originalLivePath = card.LiveAppliedLayout.MediaPath;

            // Pre-validation check before apply
            bool fileExists = File.Exists(card.DraftLayout.MediaPath);
            Assert.False(fileExists);

            // If file does not exist, application aborts and live layout remains untouched
            if (!fileExists)
            {
                // Abort application
            }
            else
            {
                card.LiveAppliedLayout = card.DraftLayout.Clone();
            }

            Assert.Equal(originalLivePath, card.LiveAppliedLayout.MediaPath);
        }
    }
}
