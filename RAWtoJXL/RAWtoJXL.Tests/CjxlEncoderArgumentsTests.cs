using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;
using Moq;

namespace RAWtoJXL.Tests;

public class CjxlEncoderArgumentsTests
{
    [Fact]
    public void BuildEncodingArguments_Quality50_ReturnsExpectedArgs()
    {
        var service = CreateTestEncoder();
        var args = service.BuildEncodingArguments(50, @"C:\input.png", @"C:\output.jxl");

        float expectedDistance = QualityCalculator.CalculateDistance(50);
        Assert.Contains($"--distance={expectedDistance:F2}", args);
        Assert.Contains("--effort=7", args);
        Assert.Contains($"--num_threads={Environment.ProcessorCount}", args);
        Assert.Contains("--container=1", args);
        Assert.Contains("--progressive_dc=1", args);
        Assert.DoesNotContain("--modular=1", args);
        Assert.Equal(@"C:\input.png", args[^2]);
        Assert.Equal(@"C:\output.jxl", args[^1]);
    }

    [Fact]
    public void BuildEncodingArguments_Quality0_ReturnsMaxCompressionArgs()
    {
        var service = CreateTestEncoder();
        var args = service.BuildEncodingArguments(0, @"C:\input.png", @"C:\output.jxl");

        Assert.Contains("--effort=7", args);
        Assert.DoesNotContain("--modular=1", args);
        Assert.Contains("--progressive_dc=1", args);
    }

    [Theory]
    [InlineData(0, 7)]
    [InlineData(50, 7)]
    [InlineData(70, 7)]
    [InlineData(85, 7)]
    [InlineData(95, 7)]
    [InlineData(100, 7)]
    public void BuildEncodingArguments_EffortMatchesCalculator(int quality, int expectedEffort)
    {
        var service = CreateTestEncoder();
        var args = service.BuildEncodingArguments(quality, @"C:\input.png", @"C:\output.jxl");

        Assert.Contains($"--effort={expectedEffort}", args);
    }

    [Fact]
    public void BuildEncodingArguments_DistanceMatchesCalculator()
    {
        var service = CreateTestEncoder();
        var args = service.BuildEncodingArguments(85, @"C:\input.png", @"C:\output.jxl");

        float expectedDistance = QualityCalculator.CalculateDistance(85);
        string expectedArg = $"--distance={expectedDistance:F2}";
        Assert.Contains(expectedArg, args);
    }

    [Fact]
    public void BuildEncodingArguments_InputAndOutputAreLastTwoArguments()
    {
        var service = CreateTestEncoder();
        var input = @"C:\Users\test\photo.png";
        var output = @"D:\output\converted.jxl";
        var args = service.BuildEncodingArguments(85, input, output);

        Assert.Equal(input, args[^2]);
        Assert.Equal(output, args[^1]);
    }

    [Fact]
    public void BuildEncodingArguments_MinimalArgs_CountIsAtLeastSix()
    {
        var service = CreateTestEncoder();
        var args = service.BuildEncodingArguments(85, @"C:\input.png", @"C:\output.jxl");

        Assert.InRange(args.Count, 6, 7);
    }

    [Fact]
    public void BuildEncodingArguments_EffortOverride_UsesCustomEffort()
    {
        var service = CreateTestEncoder();
        var args = service.BuildEncodingArguments(50, @"C:\input.png", @"C:\output.jxl", effortOverride: 3);

        Assert.Contains("--effort=3", args);
        Assert.DoesNotContain("--effort=7", args);
    }

    [Fact]
    public void BuildEncodingArguments_EffortOverrideNull_UsesAutoEffort()
    {
        var service = CreateTestEncoder();
        var args = service.BuildEncodingArguments(50, @"C:\input.png", @"C:\output.jxl", effortOverride: null);

        Assert.Contains("--effort=7", args);
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
            : base(Mock.Of<IPathResolver>(), Mock.Of<IExiftoolService>(), logger, Mock.Of<IProcessRunner>())
        {
        }
    }
}
