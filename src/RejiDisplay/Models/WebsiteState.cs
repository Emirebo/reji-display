namespace RejiDisplay.Models
{
    public class WebsiteState
    {
        public string Url { get; set; } = string.Empty;
        public bool IsMuted { get; set; } = true;
        public double ZoomFactor { get; set; } = 1.0;
        public string StatusText { get; set; } = "Ready";

        public WebsiteState Clone()
        {
            return new WebsiteState
            {
                Url = this.Url,
                IsMuted = this.IsMuted,
                ZoomFactor = this.ZoomFactor,
                StatusText = this.StatusText
            };
        }
    }
}
