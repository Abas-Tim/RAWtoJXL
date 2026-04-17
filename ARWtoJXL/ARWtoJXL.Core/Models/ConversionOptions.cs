using ARWtoJXL.Core.Services;

namespace ARWtoJXL.Core.Models
{
    public class ConversionOptions
    {
        public int Quality { get; set; } = 90;
        public OutputFormat OutputFormat { get; set; } = OutputFormat.Jxl;
        public string OutputPath { get; set; } = string.Empty;
    }
}
