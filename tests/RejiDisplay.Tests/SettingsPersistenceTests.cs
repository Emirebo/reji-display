using System;
using System.IO;
using Xunit;
using RejiDisplay.Models;
using RejiDisplay.Services;

namespace RejiDisplay.Tests
{
    public class SettingsPersistenceTests
    {
        [Fact]
        public void FreshInstallation_DefaultSettings_HaveCleanLayoutDefaults()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "test_settings_fresh_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var service = new SettingsService(tempFile);
                var settings = service.LoadSettings();

                Assert.NotNull(settings.LeftOutput.DraftLayout);
                Assert.NotNull(settings.RightOutput.DraftLayout);

                // Verify clean-install defaults for LEFT
                Assert.Equal(1.0, settings.LeftOutput.DraftLayout.Zoom);
                Assert.Equal(0, settings.LeftOutput.DraftLayout.OffsetX);
                Assert.Equal(0, settings.LeftOutput.DraftLayout.OffsetY);
                Assert.Equal(ScaleMode.Fit, settings.LeftOutput.DraftLayout.ScaleMode);

                // Verify clean-install defaults for RIGHT
                Assert.Equal(1.0, settings.RightOutput.DraftLayout.Zoom);
                Assert.Equal(0, settings.RightOutput.DraftLayout.OffsetX);
                Assert.Equal(0, settings.RightOutput.DraftLayout.OffsetY);
                Assert.Equal(ScaleMode.Fit, settings.RightOutput.DraftLayout.ScaleMode);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void ResetPosition_SavesAndRestores_CorrectedValuesAfterRestart()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "test_settings_reset_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var service = new SettingsService(tempFile);
                var settings = service.LoadSettings();

                // Operator performs layout reset ("Konumu Sıfırla")
                settings.LeftOutput.DraftLayout.Zoom = 1.0;
                settings.LeftOutput.DraftLayout.OffsetX = 0;
                settings.LeftOutput.DraftLayout.OffsetY = 0;
                settings.LeftOutput.DraftLayout.ScaleMode = ScaleMode.Fit;

                service.SaveSettings(settings);

                // Re-instantiate service and simulate app restart
                var service2 = new SettingsService(tempFile);
                var restored = service2.LoadSettings();

                Assert.Equal(1.0, restored.LeftOutput.DraftLayout.Zoom);
                Assert.Equal(0, restored.LeftOutput.DraftLayout.OffsetX);
                Assert.Equal(0, restored.LeftOutput.DraftLayout.OffsetY);
                Assert.Equal(ScaleMode.Fit, restored.LeftOutput.DraftLayout.ScaleMode);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void LeftAndRightOutputs_InitializeAndPersist_Independently()
        {
            string tempFile = Path.Combine(Path.GetTempPath(), "test_settings_independent_" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var service = new SettingsService(tempFile);
                var settings = service.LoadSettings();

                // Modify LEFT output draft
                settings.LeftOutput.DraftLayout.Zoom = 1.4;
                settings.LeftOutput.DraftLayout.OffsetX = -15;

                // Modify RIGHT output draft with different values
                settings.RightOutput.DraftLayout.Zoom = 0.8;
                settings.RightOutput.DraftLayout.OffsetX = 35;

                service.SaveSettings(settings);

                var service2 = new SettingsService(tempFile);
                var restored = service2.LoadSettings();

                // Verify independence
                Assert.Equal(1.4, restored.LeftOutput.DraftLayout.Zoom);
                Assert.Equal(-15, restored.LeftOutput.DraftLayout.OffsetX);

                Assert.Equal(0.8, restored.RightOutput.DraftLayout.Zoom);
                Assert.Equal(35, restored.RightOutput.DraftLayout.OffsetX);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }
    }
}
