using System;
using System.Collections.Generic;
using System.Windows.Media.Imaging;
using RejiDisplay.Models;

namespace RejiDisplay.Services
{
    public class OutputManager
    {
        private readonly Dictionary<string, OutputWindow> _activeWindows = new(StringComparer.OrdinalIgnoreCase);

        public bool IsOutputActive(string cardId)
        {
            return _activeWindows.ContainsKey(cardId) && _activeWindows[cardId].IsLoaded;
        }

        public DisplayInfo? GetActiveDisplay(string cardId)
        {
            if (_activeWindows.TryGetValue(cardId, out var win))
            {
                return win.TargetDisplay;
            }
            return null;
        }

        public void StartOutput(
            string cardId,
            DisplayInfo display,
            OutputCalibration calibration,
            ImageLayoutState liveAppliedLayout,
            BitmapImage? imageBitmap,
            bool isBlackout)
        {
            StopOutput(cardId);

            var window = new OutputWindow(display);
            _activeWindows[cardId] = window;

            window.Closed += (s, e) =>
            {
                if (_activeWindows.TryGetValue(cardId, out var existing) && existing == window)
                {
                    _activeWindows.Remove(cardId);
                }
            };

            window.Show();
            window.RenderLiveAppliedState(calibration, liveAppliedLayout, imageBitmap, isBlackout);
        }

        public void UpdateLiveOutput(
            string cardId,
            OutputCalibration calibration,
            ImageLayoutState liveAppliedLayout,
            BitmapImage? imageBitmap,
            bool isBlackout)
        {
            if (_activeWindows.TryGetValue(cardId, out var window))
            {
                window.RenderLiveAppliedState(calibration, liveAppliedLayout, imageBitmap, isBlackout);
            }
        }

        public void StopOutput(string cardId)
        {
            if (_activeWindows.TryGetValue(cardId, out var window))
            {
                _activeWindows.Remove(cardId);
                try
                {
                    window.Close();
                }
                catch
                {
                    // Ignore window close exceptions
                }
            }
        }

        public void StopAllOutputs()
        {
            var cardIds = new List<string>(_activeWindows.Keys);
            foreach (var id in cardIds)
            {
                StopOutput(id);
            }
        }

        public void UpdateBlackout(string cardId, bool isBlackout)
        {
            if (_activeWindows.TryGetValue(cardId, out var window))
            {
                window.SetBlackout(isBlackout);
            }
        }
    }
}
