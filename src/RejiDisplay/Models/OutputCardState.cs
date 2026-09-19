namespace RejiDisplay.Models
{
    public class OutputCardState
    {
        public string CardId { get; set; } = string.Empty; // "LEFT" or "RIGHT"
        public string Title { get; set; } = string.Empty;  // "LEFT LED", "RIGHT LED"
        public string? AssignedDeviceName { get; set; }
        public string? AssignedDeviceId { get; set; }

        public OutputCalibration Calibration { get; set; } = new();

        public ImageLayoutState DraftLayout { get; set; } = new();
        public ImageLayoutState LiveAppliedLayout { get; set; } = new();

        public bool IsLiveUpdateEnabled { get; set; } = false;
        public bool IsBlackout { get; set; }
        public bool IsActive { get; set; }
        public string StatusText { get; set; } = "INACTIVE";
        public string ErrorText { get; set; } = string.Empty;
    }
}
