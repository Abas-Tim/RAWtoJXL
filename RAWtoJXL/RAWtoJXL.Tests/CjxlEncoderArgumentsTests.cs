using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;
using Moq;

namespace RAWtoJXL.Tests;

public class CjxlEncoderArgumentsTests
{
    [Fact]
    public void BuildStreamEncodingArguments_Quality50_ReturnsExpectedArgs()
    {
        var service = CreateTestEncoder();
        var args = service.BuildStreamEncodingArguments(50, @"C:\output.jxl");

        float expectedDistance = QualityCalculator.CalculateDistance(50);
        Assert.Contains($"--distance={expectedDistance:F2}", args);
        Assert.Contains("--effort=5", args);
        Assert.Contains($"--num_threads={Environment.ProcessorCount}", args);
        Assert.Contains("--container=1", args);
        Assert.Contains("--progressive_dc=1", args);
        Assert.DoesNotContain("--modular=1", args);
        Assert.Equal("-", args[^2]);
        Assert.Equal(@"C:\output.jxl", args[^1]);
    }

    [Fact]
    public void BuildStreamEncodingArguments_Quality0_ReturnsMaxCompressionArgs()
    {
        var service = CreateTestEncoder();
        var args = service.BuildStreamEncodingArguments(0, @"C:\output.jxl");

        Assert.Contains("--effort=3", args);
        Assert.DoesNotContain("--modular=1", args);
        Assert.Contains("--progressive_dc=1", args);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(30, 4)]
    [InlineData(50, 5)]
    [InlineData(70, 6)]
    [InlineData(85, 7)]
    [InlineData(95, 8)]
    [InlineData(100, 8)]
    public void BuildStreamEncodingArguments_EffortMatchesCalculator(int quality, int expectedEffort)
    {
        var service = CreateTestEncoder();
        var args = service.BuildStreamEncodingArguments(quality, @"C:\output.jxl");

        Assert.Contains($"--effort={expectedEffort}", args);
    }

    [Fact]
    public void BuildStreamEncodingArguments_DistanceMatchesCalculator()
    {
        var service = CreateTestEncoder();
        var args = service.BuildStreamEncodingArguments(85, @"C:\output.jxl");

        float expectedDistance = QualityCalculator.CalculateDistance(85);
        string expectedArg = $"--distance={expectedDistance:F2}";
        Assert.Contains(expectedArg, args);
    }

    [Fact]
    public void BuildStreamEncodingArguments_StdinAndOutputAreLastTwoArguments()
    {
        var service = CreateTestEncoder();
        var output = @"D:\output\converted.jxl";
        var args = service.BuildStreamEncodingArguments(85, output);

        Assert.Equal("-", args[^2]);
        Assert.Equal(output, args[^1]);
    }

    [Fact]
    public void BuildStreamEncodingArguments_MinimalArgs_CountIsAtLeastSix()
    {
        var service = CreateTestEncoder();
        var args = service.BuildStreamEncodingArguments(85, @"C:\output.jxl");

        Assert.InRange(args.Count, 6, 7);
    }

    [Fact]
    public void BuildStreamEncodingArguments_EffortOverride_UsesCustomEffort()
    {
        var service = CreateTestEncoder();
        var args = service.BuildStreamEncodingArguments(50, @"C:\output.jxl", effortOverride: 3);

        Assert.Contains("--effort=3", args);
        Assert.DoesNotContain("--effort=5", args);
    }

    [Fact]
    public void BuildStreamEncodingArguments_EffortOverrideNull_UsesAutoEffort()
    {
        var service = CreateTestEncoder();
        var args = service.BuildStreamEncodingArguments(50, @"C:\output.jxl", effortOverride: null);

        Assert.Contains("--effort=5", args);
    }

    [Fact]
    public void BuildStreamEncodingArguments_EffortOverride9_StillAllowed()
    {
        var service = CreateTestEncoder();
        var args = service.BuildStreamEncodingArguments(100, @"C:\output.jxl", effortOverride: 9);

        Assert.Contains("--effort=9", args);
        Assert.DoesNotContain("--effort=8", args);
    }

    private static TestEncoder CreateTestEncoder()
    {
        var logger = new Mock<ILogger>();
        logger.Setup(l => l.Write(It.IsAny<string>()));
        return new TestEncoder(logger.Object);
    }

 private class TestEncoder : CjxlEncoderService
    {
        public TestEncoder(ILogger logger)
            : base(Mock.Of<IPathResolver>(), logger, Mock.Of<IProcessRunner>())
        {
        }
    }
}
