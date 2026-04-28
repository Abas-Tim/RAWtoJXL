using ARWtoJXL.Core.Models;
using Xunit;

namespace ARWtoJXL.Tests
{
    public class QualityCalculatorTests
    {
        [Theory]
        [InlineData(0, 5)]
        [InlineData(50, 6)]
        [InlineData(70, 7)]
        [InlineData(85, 8)]
        [InlineData(95, 9)]
        [InlineData(100, 9)]
        public void CalculateEffort_ReturnsCorrectEffort(int quality, int expectedEffort)
        {
            var effort = QualityCalculator.CalculateEffort(quality);
            Assert.Equal(expectedEffort, effort);
        }

        [Theory]
        [InlineData(99, false)]
        [InlineData(100, true)]
        [InlineData(101, true)]
        public void IsLossless_ReturnsCorrectValue(int quality, bool expected)
        {
            var isLossless = QualityCalculator.IsLossless(quality);
            Assert.Equal(expected, isLossless);
        }

        [Fact]
        public void CalculateDistance_Quality90_ReturnsApprox1()
        {
            var distance = QualityCalculator.CalculateDistance(90);
            Assert.InRange(distance, 0.9f, 1.1f);
        }

        [Fact]
        public void CalculateDistance_Quality0_ReturnsMaxDistance()
        {
            var distance = QualityCalculator.CalculateDistance(0);
            Assert.InRange(distance, 20.0f, 30.0f);
        }
    }
}
