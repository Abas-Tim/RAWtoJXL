using RAWtoJXL.Avalonia;
using RAWtoJXL.Core.Interfaces;

namespace RAWtoJXL.Tests;

public class CrashReporterTests
{
    private sealed class FakeLogger : ILogger
    {
        public List<string> Messages { get; } = new();
        public void Write(string message) => Messages.Add(message);
        public void Clear() => Messages.Clear();
    }

    [Fact]
    public void Record_Exception_LogsContextAndDetails()
    {
        var logger = new FakeLogger();

        CrashReporter.Record(logger, "crash", new InvalidOperationException("boom"));

        Assert.Contains(logger.Messages, m => m.Contains("[CrashReporter:crash]"));
        Assert.Contains(logger.Messages, m => m.Contains("boom"));
    }

    [Fact]
    public void Record_Message_LogsContextAndMessage()
    {
        var logger = new FakeLogger();

        CrashReporter.Record(logger, "ui", "layout failure");

        Assert.Contains(logger.Messages, m => m.Contains("[CrashReporter:ui]"));
        Assert.Contains(logger.Messages, m => m.Contains("layout failure"));
    }

    [Fact]
    public void Record_NullLogger_DoesNotThrow()
    {
        CrashReporter.Record(null, "task", new Exception("boom"));
    }

    [Fact]
    public void Record_NullException_LogsNullPlaceholder()
    {
        var logger = new FakeLogger();

        CrashReporter.Record(logger, "crash", (Exception?)null);

        Assert.Contains(logger.Messages, m => m.Contains("(null exception)"));
    }
}
