using System.Numerics;
using FontStash.NET;
using Silk.NET.OpenGL;

namespace GLCraft.Fonts;

public sealed class GLFons : IDisposable
{
    private const string VertexShaderSource = """
                                              #version 330 core
                                              layout (location = 0) in vec2 vPos;
                                              layout (location = 1) in vec2 vUv;
                                              layout (location = 2) in vec4 vColor;

                                              uniform mat4 uProjection;

                                              out vec2 fUv;
                                              out vec4 fColor;

                                              void main()
                                              {
                                                  gl_Position = uProjection * vec4(vPos, 0.0, 1.0);
                                                  fUv = vUv;
                                                  fColor = vColor;
                                              }
                                              """;

    private const string FragmentShaderSource = """
                                                #version 330 core
                                                in vec2 fUv;
                                                in vec4 fColor;

                                                uniform sampler2D uTexture0;

                                                out vec4 FragColor;

                                                void main()
                                                {
                                                    FragColor = texture(uTexture0, fUv) * fColor;
                                                }
                                                """;

    private const int VertexAttrib = 0;
    private const int TcoordAttrib = 1;
    private const int ColourAttrib = 2;

    private readonly GL _gl;

    private uint _tex;
    private int _width;
    private int _height;
    private uint _vertexArray;
    private uint _vertexBuffer;
    private uint _tcoordBuffer;
    private uint _colourBuffer;
    private uint _shaderProgram;
    private Fontstash? _fontstash;
    private Matrix4x4 _projection = Matrix4x4.Identity;

    public GLFons(GL gl)
    {
        _gl = gl;
    }

    public Fontstash Create(int width, int height, int flags)
    {
        FonsParams prams = default;
        prams.width = width;
        prams.height = height;
        prams.flags = (byte)flags;
        prams.renderCreate = RenderCreate;
        prams.renderResize = RenderResize;
        prams.renderUpdate = RenderUpdate;
        prams.renderDraw = RenderDraw;
        prams.renderDelete = RenderDelete;

        _fontstash = new Fontstash(prams);
        return _fontstash;
    }

    public void SetProjection(Matrix4x4 projection)
    {
        _projection = projection;
    }

    public void Dispose()
    {
        _fontstash?.Dispose();
        _fontstash = null;
        RenderDelete();
    }

    public static uint Rgba(byte r, byte g, byte b, byte a)
    {
        return (uint)(r | (g << 8) | (b << 16) | (a << 24));
    }

    private unsafe bool RenderCreate(int width, int height)
    {
        if (_tex != 0)
        {
            _gl.DeleteTexture(_tex);
            _tex = 0;
        }

        _tex = _gl.GenTexture();
        if (_tex == 0)
        {
            return false;
        }

        if (_vertexArray == 0)
        {
            _vertexArray = _gl.GenVertexArray();
        }

        if (_vertexArray == 0)
        {
            return false;
        }

        _gl.BindVertexArray(_vertexArray);

        if (_vertexBuffer == 0)
        {
            _vertexBuffer = _gl.GenBuffer();
        }

        if (_vertexBuffer == 0)
        {
            return false;
        }

        if (_tcoordBuffer == 0)
        {
            _tcoordBuffer = _gl.GenBuffer();
        }

        if (_tcoordBuffer == 0)
        {
            return false;
        }

        if (_colourBuffer == 0)
        {
            _colourBuffer = _gl.GenBuffer();
        }

        if (_colourBuffer == 0)
        {
            return false;
        }

        _width = width;
        _height = height;

        _gl.BindTexture(TextureTarget.Texture2D, _tex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, (int)InternalFormat.Red, (uint)_width, (uint)_height, 0, GLEnum.Red, GLEnum.UnsignedByte, null);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        int[] swizzleRgbaParams = [(int)GLEnum.One, (int)GLEnum.One, (int)GLEnum.One, (int)GLEnum.Red];
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleRgba, swizzleRgbaParams);

        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.BindVertexArray(0);

        if (_shaderProgram == 0)
        {
            _shaderProgram = CreateShaderProgram(VertexShaderSource, FragmentShaderSource);
        }

        return true;
    }

    private bool RenderResize(int width, int height)
    {
        return RenderCreate(width, height);
    }

    private unsafe void RenderUpdate(int[] rect, byte[] data)
    {
        int w = rect[2] - rect[0];
        int h = rect[3] - rect[1];

        if (_tex == 0)
        {
            return;
        }

        int alignment = _gl.GetInteger(GLEnum.UnpackAlignment);
        int rowLength = _gl.GetInteger(GLEnum.UnpackRowLength);
        int skipPixels = _gl.GetInteger(GLEnum.UnpackSkipPixels);
        int skipRows = _gl.GetInteger(GLEnum.UnpackSkipRows);

        _gl.BindTexture(TextureTarget.Texture2D, _tex);
        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
        _gl.PixelStore(PixelStoreParameter.UnpackRowLength, _width);
        _gl.PixelStore(PixelStoreParameter.UnpackSkipPixels, rect[0]);
        _gl.PixelStore(PixelStoreParameter.UnpackSkipRows, rect[1]);

        fixed (byte* d = data)
        {
            _gl.TexSubImage2D(TextureTarget.Texture2D, 0, rect[0], rect[1], (uint)w, (uint)h, GLEnum.Red, GLEnum.UnsignedByte, d);
        }

        _gl.PixelStore(PixelStoreParameter.UnpackAlignment, alignment);
        _gl.PixelStore(PixelStoreParameter.UnpackRowLength, rowLength);
        _gl.PixelStore(PixelStoreParameter.UnpackSkipPixels, skipPixels);
        _gl.PixelStore(PixelStoreParameter.UnpackSkipRows, skipRows);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
    }

    private unsafe void RenderDraw(float[] verts, float[] tcoords, uint[] colours, int nverts)
    {
        if (_tex == 0 || _vertexArray == 0 || _shaderProgram == 0)
        {
            return;
        }

        _gl.UseProgram(_shaderProgram);
        _gl.ActiveTexture(TextureUnit.Texture0);
        _gl.BindTexture(TextureTarget.Texture2D, _tex);
        _gl.Uniform1(_gl.GetUniformLocation(_shaderProgram, "uTexture0"), 0);

        int projectionLocation = _gl.GetUniformLocation(_shaderProgram, "uProjection");
        Matrix4x4 projection = _projection;
        _gl.UniformMatrix4(projectionLocation, 1, false, (float*)&projection);

        _gl.BindVertexArray(_vertexArray);

        _gl.EnableVertexAttribArray(VertexAttrib);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _vertexBuffer);
        fixed (float* d = verts)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(nverts * 2 * sizeof(float)), d, BufferUsageARB.DynamicDraw);
        }
        _gl.VertexAttribPointer(VertexAttrib, 2, GLEnum.Float, false, 0, null);

        _gl.EnableVertexAttribArray(TcoordAttrib);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _tcoordBuffer);
        fixed (float* d = tcoords)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(nverts * 2 * sizeof(float)), d, BufferUsageARB.DynamicDraw);
        }
        _gl.VertexAttribPointer(TcoordAttrib, 2, GLEnum.Float, false, 0, null);

        _gl.EnableVertexAttribArray(ColourAttrib);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, _colourBuffer);
        fixed (uint* d = colours)
        {
            _gl.BufferData(BufferTargetARB.ArrayBuffer, (nuint)(nverts * sizeof(uint)), d, BufferUsageARB.DynamicDraw);
        }
        _gl.VertexAttribPointer(ColourAttrib, 4, GLEnum.UnsignedByte, true, 0, null);

        _gl.DrawArrays(PrimitiveType.Triangles, 0, (uint)nverts);

        _gl.DisableVertexAttribArray(VertexAttrib);
        _gl.DisableVertexAttribArray(TcoordAttrib);
        _gl.DisableVertexAttribArray(ColourAttrib);
        _gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        _gl.BindVertexArray(0);
        _gl.BindTexture(TextureTarget.Texture2D, 0);
        _gl.UseProgram(0);
    }

    private void RenderDelete()
    {
        if (_tex != 0)
        {
            _gl.DeleteTexture(_tex);
            _tex = 0;
        }

        if (_vertexBuffer != 0)
        {
            _gl.DeleteBuffer(_vertexBuffer);
            _vertexBuffer = 0;
        }

        if (_tcoordBuffer != 0)
        {
            _gl.DeleteBuffer(_tcoordBuffer);
            _tcoordBuffer = 0;
        }

        if (_colourBuffer != 0)
        {
            _gl.DeleteBuffer(_colourBuffer);
            _colourBuffer = 0;
        }

        if (_vertexArray != 0)
        {
            _gl.DeleteVertexArray(_vertexArray);
            _vertexArray = 0;
        }

        if (_shaderProgram != 0)
        {
            _gl.DeleteProgram(_shaderProgram);
            _shaderProgram = 0;
        }
    }

    private uint CreateShaderProgram(string vertexShaderSource, string fragmentShaderSource)
    {
        uint vertexShader = CompileShader(ShaderType.VertexShader, vertexShaderSource);
        uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentShaderSource);

        uint program = _gl.CreateProgram();
        _gl.AttachShader(program, vertexShader);
        _gl.AttachShader(program, fragmentShader);
        _gl.LinkProgram(program);
        _gl.GetProgram(program, GLEnum.LinkStatus, out int linkStatus);

        if (linkStatus == 0)
        {
            string infoLog = _gl.GetProgramInfoLog(program);
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);
            _gl.DeleteProgram(program);
            throw new InvalidOperationException($"Text shader failed to link: {infoLog}");
        }

        _gl.DetachShader(program, vertexShader);
        _gl.DetachShader(program, fragmentShader);
        _gl.DeleteShader(vertexShader);
        _gl.DeleteShader(fragmentShader);

        return program;
    }

    private uint CompileShader(ShaderType shaderType, string source)
    {
        uint shader = _gl.CreateShader(shaderType);
        _gl.ShaderSource(shader, source);
        _gl.CompileShader(shader);
        _gl.GetShader(shader, ShaderParameterName.CompileStatus, out int compileStatus);

        if (compileStatus == 0)
        {
            string infoLog = _gl.GetShaderInfoLog(shader);
            _gl.DeleteShader(shader);
            throw new InvalidOperationException($"Text shader failed to compile: {infoLog}");
        }

        return shader;
    }
}
