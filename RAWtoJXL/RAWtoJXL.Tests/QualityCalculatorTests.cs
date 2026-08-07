using RAWtoJXL.Core.Models;
using Xunit;

namespace RAWtoJXL.Tests
{
    public class QualityCalculatorTests
    {
        [Theory]
        [InlineData(0, 3)]
        [InlineData(29, 3)]
        [InlineData(30, 4)]
        [InlineData(44, 4)]
        [InlineData(45, 5)]
        [InlineData(59, 5)]
        [InlineData(60, 6)]
        [InlineData(74, 6)]
        [InlineData(75, 7)]
        [InlineData(89, 7)]
        [InlineData(90, 8)]
        [InlineData(99, 8)]
        [InlineData(100, 8)]
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
