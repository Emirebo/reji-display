using System;
using System.IO;
using Xunit;
using RejiDisplay.Models;

namespace RejiDisplay.Tests
{
    public class ContentBankTests
    {
        [Fact]
        public void ContentBankItem_MissingFile_ReturnsFalseAndOutputsMissingPath()
        {
            var item = new ContentBankItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Non Existent Video",
                MediaSource = new MediaSource
                {
                    Type = MediaSourceType.Video,
                    FilePath = "C:\\non_existent_folder_12345\\missing_video.mp4",
                    DisplayName = "missing_video.mp4"
                }
            };

            bool exists = item.CheckFileExists(out string? missingPath);

            Assert.False(exists);
            Assert.Equal("C:\\non_existent_folder_12345\\missing_video.mp4", missingPath);
        }

        [Fact]
        public void ContentBankItem_ExistingFile_ReturnsTrueAndNullMissingPath()
        {
            string tempFile = Path.GetTempFileName();
            try
            {
                var item = new ContentBankItem
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Title = "Temp Image",
                    MediaSource = new MediaSource
                    {
                        Type = MediaSourceType.Image,
                        FilePath = tempFile,
                        DisplayName = Path.GetFileName(tempFile)
                    }
                };

                bool exists = item.CheckFileExists(out string? missingPath);

                Assert.True(exists);
                Assert.Null(missingPath);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void ContentBankItem_WebUrl_AlwaysReturnsTrue()
        {
            var item = new ContentBankItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = "Web Page",
                MediaSource = MediaSource.FromUrl("https://example.com")
            };

            bool exists = item.CheckFileExists(out string? missingPath);

            Assert.True(exists);
            Assert.Null(missingPath);
        }

        [Fact]
        public void StagedDraft_DoesNotChangeLiveState_UntilApplied()
        {
            var cardState = new OutputCardState
            {
                CardId = "LEFT",
                DraftLayout = new ImageLayoutState
                {
                    MediaSource = new MediaSource { Type = MediaSourceType.TestPattern, DisplayName = "Original Live Pattern" },
                    ScaleMode = ScaleMode.Stretch
                },
                LiveAppliedLayout = new ImageLayoutState
                {
                    MediaSource = new MediaSource { Type = MediaSourceType.TestPattern, DisplayName = "Original Live Pattern" },
                    ScaleMode = ScaleMode.Stretch
                }
            };

            // Stage a new media item into DraftLayout
            var stagedSource = new MediaSource { Type = MediaSourceType.Image, FilePath = "C:\\images\\bg.png", DisplayName = "Staged Image" };
            cardState.DraftLayout.MediaSource = stagedSource;

            // Verify live content is preserved and distinct from staged content
            Assert.Equal("Original Live Pattern", cardState.LiveAppliedLayout.MediaSource.DisplayName);
            Assert.Equal("Staged Image", cardState.DraftLayout.MediaSource.DisplayName);
            Assert.NotEqual(cardState.LiveAppliedLayout.MediaSource.DisplayName, cardState.DraftLayout.MediaSource.DisplayName);

            // Simulate explicit UYGULA / YAYINA AL action
            cardState.LiveAppliedLayout = cardState.DraftLayout.Clone();

            // Now live output is updated to staged content
            Assert.Equal("Staged Image", cardState.LiveAppliedLayout.MediaSource.DisplayName);
        }
    }
}
