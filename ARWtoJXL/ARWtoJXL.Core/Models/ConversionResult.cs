namespace ARWtoJXL.Core.Models
{
    public class ConversionResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? OutputPath { get; set; }
        public double Progress { get; set; }
    }
}
