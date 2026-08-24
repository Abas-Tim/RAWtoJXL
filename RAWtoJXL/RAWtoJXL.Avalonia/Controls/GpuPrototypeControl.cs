using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Platform;

namespace RAWtoJXL.Avalonia.Controls;

public sealed record OpenGlCapabilityReport(
    bool HasContext,
    bool IsDesktopOpenGl,
    bool SupportsRenderPrototype,
    bool SupportsComputeShaders,
    string Version,
    string Vendor,
    string Renderer,
    string? FailureReason)
{
    public static OpenGlCapabilityReport From(
        GlVersion version,
        string? versionText,
        string? vendor,
        string? renderer)
    {
        bool hasContext = version.Major > 0;
        bool isDesktopOpenGl = version.Type == GlProfileType.OpenGL;
        bool isOpenGles = version.Type == GlProfileType.OpenGLES;
        bool supportsRenderPrototype = (isDesktopOpenGl && IsAtLeast(version, 3, 3)) ||
                                       (isOpenGles && IsAtLeast(version, 3, 0));
        bool supportsComputeShaders = (isDesktopOpenGl && IsAtLeast(version, 4, 3)) ||
                                      (isOpenGles && IsAtLeast(version, 3, 1));
        return new OpenGlCapabilityReport(
            hasContext,
            isDesktopOpenGl,
            supportsRenderPrototype,
            supportsComputeShaders,
            versionText ?? $"{version.Major}.{version.Minor}",
            vendor ?? string.Empty,
            renderer ?? string.Empty,
            null);
    }

    public OpenGlCapabilityReport WithFailure(string reason)
    {
        return this with
        {
            HasContext = false,
            SupportsRenderPrototype = false,
            SupportsComputeShaders = false,
            FailureReason = reason
        };
    }

    public string StatusText
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(FailureReason))
            {
                return $"GPU unavailable: {FailureReason}";
            }

            if (!HasContext)
            {
                return "GPU context unavailable";
            }

            if (!SupportsRenderPrototype)
            {
                return $"OpenGL profile unsupported ({Version})";
            }

            string profile = Version.StartsWith("OpenGL", StringComparison.OrdinalIgnoreCase)
                ? Version
                : $"OpenGL {Version}";
            return SupportsComputeShaders
                ? $"{profile} | compute ready"
                : $"{profile} | render only";
        }
    }

    private static bool IsAtLeast(GlVersion version, int major, int minor)
    {
        return version.Major > major || version.Major == major && version.Minor >= minor;
    }
}

public sealed class GpuPrototypeControl : OpenGlControlBase
{
    private const int Framebuffer = 0x8D40;
    private const int ColorBufferBit = 0x00004000;
    private const int Triangles = 0x0004;
    private const int VertexShader = 0x8B31;
    private const int FragmentShader = 0x8B30;
    private const int Texture2D = 0x0DE1;
    private const int Texture0 = 0x84C0;
    private const int TextureMinFilter = 0x2801;
    private const int TextureMagFilter = 0x2800;
    private const int TextureWrapS = 0x2802;
    private const int TextureWrapT = 0x2803;
    private const int Linear = 0x2601;
    private const int ClampToEdge = 0x812F;
    private const int Rgba = 0x1908;
    private const int UnsignedByte = 0x1401;

    private int _program;
    private int _vertexArray;
    private int _texture;
    private int _textureUniformLocation = -1;
    private int _hasTextureUniformLocation = -1;
    private int _imageGeneration;
    private int _uploadedImageGeneration = -1;
    private int _imageWidth;
    private int _imageHeight;
    private int _imageFormat = Rgba;
    private byte[]? _imagePixels;
    private string? _imageFailure;
    private string _statusText = "GPU prototype waiting for OpenGL";

    public static readonly StyledProperty<Bitmap?> ImageSourceProperty =
        AvaloniaProperty.Register<GpuPrototypeControl, Bitmap?>(nameof(ImageSource));

    public static readonly DirectProperty<GpuPrototypeControl, string> StatusTextProperty =
        AvaloniaProperty.RegisterDirect<GpuPrototypeControl, string>(
            nameof(StatusText), control => control.StatusText);

    public OpenGlCapabilityReport? Capability { get; private set; }

    public event EventHandler? CapabilityChanged;

    public Bitmap? ImageSource
    {
        get => GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetAndRaise(StatusTextProperty, ref _statusText, value);
    }

    static GpuPrototypeControl()
    {
        ImageSourceProperty.Changed.AddClassHandler<GpuPrototypeControl>(
            (control, change) => control.OnImageSourceChanged(change.NewValue as Bitmap));
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        try
        {
            Capability = OpenGlCapabilityReport.From(GlVersion, gl.Version, gl.Vendor, gl.Renderer);
            RefreshStatusText();
            if (!Capability.HasContext || !Capability.SupportsRenderPrototype)
            {
                CapabilityChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (gl.IsGenVertexArraysAvailable && gl.IsBindVertexArrayAvailable)
            {
                _vertexArray = gl.GenVertexArray();
                gl.BindVertexArray(_vertexArray);
            }

            _program = CreateProgram(gl, Capability.IsDesktopOpenGl);
            _textureUniformLocation = gl.GetUniformLocationString(_program, "uTexture");
            _hasTextureUniformLocation = gl.GetUniformLocationString(_program, "uHasTexture");
            RefreshStatusText();
            CapabilityChanged?.Invoke(this, EventArgs.Empty);
            RequestNextFrameRendering();
        }
        catch (Exception exception)
        {
            DeleteResources(gl);
            Capability = (Capability ?? OpenGlCapabilityReport.From(GlVersion, gl.Version, gl.Vendor, gl.Renderer))
                .WithFailure(exception.GetType().Name);
            RefreshStatusText();
            CapabilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (_program == 0)
        {
            return;
        }

        gl.BindFramebuffer(Framebuffer, fb);
        UploadTextureIfNeeded(gl);
        gl.Viewport(0, 0, Math.Max(1, (int)Math.Ceiling(Bounds.Width)), Math.Max(1, (int)Math.Ceiling(Bounds.Height)));
        gl.ClearColor(0.04f, 0.07f, 0.10f, 1.0f);
        gl.Clear(ColorBufferBit);
        gl.UseProgram(_program);
        gl.ActiveTexture(Texture0);
        gl.BindTexture(Texture2D, _texture);
        if (_textureUniformLocation >= 0)
        {
            gl.Uniform1i(_textureUniformLocation, 0);
        }

        if (_hasTextureUniformLocation >= 0)
        {
            gl.Uniform1i(_hasTextureUniformLocation, _texture == 0 ? 0 : 1);
        }

        if (_vertexArray != 0)
        {
            gl.BindVertexArray(_vertexArray);
        }

        gl.DrawArrays(Triangles, 0, (IntPtr)3);
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        DeleteResources(gl);
        Capability = null;
        RefreshStatusText();
    }

    protected override void OnOpenGlLost()
    {
        _program = 0;
        _vertexArray = 0;
        _texture = 0;
        _uploadedImageGeneration = -1;
        _textureUniformLocation = -1;
        _hasTextureUniformLocation = -1;
        Capability = new OpenGlCapabilityReport(false, false, false, false, string.Empty, string.Empty, string.Empty, "context lost");
        RefreshStatusText();
        CapabilityChanged?.Invoke(this, EventArgs.Empty);
    }

    private int CreateProgram(GlInterface gl, bool isDesktopOpenGl)
    {
        string shaderVersion = isDesktopOpenGl
            ? "#version 330 core\n"
            : "#version 300 es\nprecision mediump float;\nprecision mediump int;\n";
        string vertexSource = shaderVersion +
                              "out vec2 vUv;\n" +
                              "void main() {\n" +
                              "  vec2 positions[3] = vec2[](vec2(-1.0, -1.0), vec2(3.0, -1.0), vec2(-1.0, 3.0));\n" +
                              "  vec2 position = positions[gl_VertexID];\n" +
                              "  vUv = position * 0.5 + 0.5;\n" +
                              "  gl_Position = vec4(position, 0.0, 1.0);\n" +
                              "}\n";
        string fragmentSource = shaderVersion +
                                "in vec2 vUv;\n" +
                                "out vec4 color;\n" +
                                "uniform sampler2D uTexture;\n" +
                                "uniform int uHasTexture;\n" +
                                "void main() {\n" +
                                "  color = uHasTexture != 0\n" +
                                "    ? texture(uTexture, vec2(vUv.x, 1.0 - vUv.y))\n" +
                                "    : vec4(0.08 + vUv.x * 0.35, 0.12 + vUv.y * 0.48, 0.25, 1.0);\n" +
                                "}\n";

        int vertex = gl.CreateShader(VertexShader);
        int fragment = gl.CreateShader(FragmentShader);
        int program = gl.CreateProgram();
        try
        {
            ThrowIfShaderFailed(gl.CompileShaderAndGetError(vertex, vertexSource));
            ThrowIfShaderFailed(gl.CompileShaderAndGetError(fragment, fragmentSource));
            gl.AttachShader(program, vertex);
            gl.AttachShader(program, fragment);
            ThrowIfShaderFailed(gl.LinkProgramAndGetError(program));
            return program;
        }
        catch
        {
            if (program != 0)
            {
                gl.DeleteProgram(program);
            }

            throw;
        }
        finally
        {
            if (vertex != 0)
            {
                gl.DeleteShader(vertex);
            }

            if (fragment != 0)
            {
                gl.DeleteShader(fragment);
            }
        }
    }

    private void DeleteResources(GlInterface gl)
    {
        if (_texture != 0)
        {
            gl.DeleteTexture(_texture);
            _texture = 0;
        }

        if (_program != 0)
        {
            gl.DeleteProgram(_program);
            _program = 0;
        }

        if (_vertexArray != 0 && gl.IsDeleteVertexArraysAvailable)
        {
            gl.DeleteVertexArray(_vertexArray);
            _vertexArray = 0;
        }

        _uploadedImageGeneration = -1;
        _textureUniformLocation = -1;
        _hasTextureUniformLocation = -1;
    }

    private void OnImageSourceChanged(Bitmap? bitmap)
    {
        _imageGeneration++;
        _imagePixels = null;
        _imageWidth = 0;
        _imageHeight = 0;
        _imageFailure = null;

        if (bitmap != null)
        {
            try
            {
                var format = bitmap.Format.GetValueOrDefault(PixelFormat.Bgra8888);
                if (format != PixelFormat.Rgba8888 && format != PixelFormat.Bgra8888)
                {
                    throw new NotSupportedException($"pixel format {format}");
                }

                int width = bitmap.PixelSize.Width;
                int height = bitmap.PixelSize.Height;
                int stride = checked(width * 4);
                int bufferSize = checked(stride * height);
                var pixels = new byte[bufferSize];
                IntPtr buffer = Marshal.AllocHGlobal(bufferSize);
                try
                {
                    bitmap.CopyPixels(new PixelRect(bitmap.PixelSize), buffer, bufferSize, stride);
                    Marshal.Copy(buffer, pixels, 0, bufferSize);
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }

                if (format == PixelFormat.Bgra8888)
                {
                    for (int index = 0; index < pixels.Length; index += 4)
                    {
                        (pixels[index], pixels[index + 2]) = (pixels[index + 2], pixels[index]);
                    }
                }

                _imagePixels = pixels;
                _imageWidth = width;
                _imageHeight = height;
                _imageFormat = Rgba;
            }
            catch (Exception exception)
            {
                _imageFailure = exception.GetType().Name;
            }
        }

        RefreshStatusText();
        if (_program != 0)
        {
            RequestNextFrameRendering();
        }
    }

    private void UploadTextureIfNeeded(GlInterface gl)
    {
        if (_uploadedImageGeneration == _imageGeneration)
        {
            return;
        }

        if (_texture != 0)
        {
            gl.DeleteTexture(_texture);
            _texture = 0;
        }

        _uploadedImageGeneration = _imageGeneration;
        if (_imagePixels == null || _imageWidth <= 0 || _imageHeight <= 0)
        {
            return;
        }

        _texture = gl.GenTexture();
        if (_texture == 0)
        {
            return;
        }

        gl.ActiveTexture(Texture0);
        gl.BindTexture(Texture2D, _texture);
        gl.TexParameteri(Texture2D, TextureMinFilter, Linear);
        gl.TexParameteri(Texture2D, TextureMagFilter, Linear);
        gl.TexParameteri(Texture2D, TextureWrapS, ClampToEdge);
        gl.TexParameteri(Texture2D, TextureWrapT, ClampToEdge);

        var handle = GCHandle.Alloc(_imagePixels, GCHandleType.Pinned);
        try
        {
            gl.TexImage2D(
                Texture2D,
                0,
                Rgba,
                _imageWidth,
                _imageHeight,
                0,
                _imageFormat,
                UnsignedByte,
                handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    private void RefreshStatusText()
    {
        if (Capability == null)
        {
            StatusText = "GPU prototype waiting for OpenGL";
            return;
        }

        StatusText = Capability.StatusText;
        if (Capability.FailureReason == null && !string.IsNullOrWhiteSpace(_imageFailure))
        {
            StatusText += $" | image unavailable: {_imageFailure}";
        }
    }

    private static void ThrowIfShaderFailed(string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(error);
        }
    }
}
