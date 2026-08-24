using Avalonia.OpenGL;
using Avalonia.Headless.XUnit;
using RAWtoJXL.Avalonia.Controls;

namespace RAWtoJXL.Tests.GUITests;

[Trait("category", "gui")]
public class GpuPrototypeControlTests
{
    [Fact]
    public void DesktopOpenGl43_ReportsComputeSupport()
    {
        var report = OpenGlCapabilityReport.From(
            new GlVersion(GlProfileType.OpenGL, 4, 3),
            "4.6.0",
            "Test Vendor",
            "Test Renderer");

        Assert.True(report.HasContext);
        Assert.True(report.IsDesktopOpenGl);
        Assert.True(report.SupportsRenderPrototype);
        Assert.True(report.SupportsComputeShaders);
        Assert.Equal("OpenGL 4.6.0 | compute ready", report.StatusText);
    }

    [Fact]
    public void OlderDesktopOpenGl_ReportsRenderOnly()
    {
        var report = OpenGlCapabilityReport.From(
            new GlVersion(GlProfileType.OpenGL, 3, 3),
            "3.3.0",
            "Test Vendor",
            "Test Renderer");

        Assert.True(report.HasContext);
        Assert.True(report.IsDesktopOpenGl);
        Assert.True(report.SupportsRenderPrototype);
        Assert.False(report.SupportsComputeShaders);
        Assert.Equal("OpenGL 3.3.0 | render only", report.StatusText);
    }

    [Fact]
    public void OpenGles30Profile_ReportsRenderOnly()
    {
        var report = OpenGlCapabilityReport.From(
            new GlVersion(GlProfileType.OpenGLES, 3, 0),
            "OpenGL ES 3.0",
            "Test Vendor",
            "Test Renderer");

        Assert.True(report.HasContext);
        Assert.False(report.IsDesktopOpenGl);
        Assert.True(report.SupportsRenderPrototype);
        Assert.False(report.SupportsComputeShaders);
        Assert.Equal("OpenGL ES 3.0 | render only", report.StatusText);
    }

    [Fact]
    public void OpenGles20Profile_IsRejected()
    {
        var report = OpenGlCapabilityReport.From(
            new GlVersion(GlProfileType.OpenGLES, 2, 0),
            "OpenGL ES 2.0",
            "Test Vendor",
            "Test Renderer");

        Assert.True(report.HasContext);
        Assert.False(report.IsDesktopOpenGl);
        Assert.False(report.SupportsRenderPrototype);
        Assert.False(report.SupportsComputeShaders);
        Assert.Equal("OpenGL profile unsupported (OpenGL ES 2.0)", report.StatusText);
    }

    [Fact]
    public void FailedReport_UsesSafeStatus()
    {
        var report = OpenGlCapabilityReport.From(
            new GlVersion(GlProfileType.OpenGL, 4, 6),
            "4.6.0",
            "Test Vendor",
            "Test Renderer").WithFailure("shader error");

        Assert.False(report.HasContext);
        Assert.False(report.SupportsRenderPrototype);
        Assert.False(report.SupportsComputeShaders);
        Assert.Equal("GPU unavailable: shader error", report.StatusText);
    }

    [AvaloniaFact]
    public void CompareWindow_EnablesGpuPrototypeByDefault()
    {
        var service = new Moq.Mock<RAWtoJXL.Core.Interfaces.ICompareConversionService>();
        var dispatcher = new Moq.Mock<RAWtoJXL.Avalonia.Services.IDispatcherService>();
        dispatcher.Setup(item => item.InvokeAsync(Moq.It.IsAny<Action>()))
            .Returns<Action>(action =>
            {
                action();
                return Task.CompletedTask;
            });
        var viewModel = new RAWtoJXL.Avalonia.ViewModels.CompareViewModel(
            Path.Combine(Path.GetTempPath(), "gpu-prototype.dng"),
            service.Object,
            dispatcher.Object);
        Assert.True(viewModel.IsGpuPrototypeVisible);
        Assert.True(viewModel.IsGpuPrototypeAvailable);
        var window = new RAWtoJXL.Avalonia.CompareWindow
        {
            DataContext = viewModel
        };

        window.Show();
        window.UpdateLayout();

        Assert.Single(GUITestHelpers.GetAllControls<GpuPrototypeControl>(window));
        Assert.Contains(
            GUITestHelpers.GetAllControls<global::Avalonia.Controls.CheckBox>(window),
            checkBox => checkBox.Content?.ToString() == "Show GPU prototype");
        Assert.True(viewModel.IsGpuPrototypeVisible);

        window.Close();
        viewModel.Dispose();
    }

    [Fact]
    public void CompareViewModel_DisablesGpuPrototypeWhenUnavailable()
    {
        var service = new Moq.Mock<RAWtoJXL.Core.Interfaces.ICompareConversionService>();
        var dispatcher = new Moq.Mock<RAWtoJXL.Avalonia.Services.IDispatcherService>();
        var viewModel = new RAWtoJXL.Avalonia.ViewModels.CompareViewModel(
            Path.Combine(Path.GetTempPath(), "gpu-prototype.dng"),
            service.Object,
            dispatcher.Object);

        viewModel.SetGpuPrototypeAvailability(false);

        Assert.False(viewModel.IsGpuPrototypeAvailable);
        Assert.False(viewModel.IsGpuPrototypeVisible);
        viewModel.Dispose();
    }
}
