using Silk.NET.OpenGL;
using StbImageSharp;

namespace GLCraft.Fundamental;

public class CubeTexture : IDisposable
{
    private GL _gl;
    public uint _handle;

    public unsafe CubeTexture(GL gl, string path)
    {
        _gl = gl;
        _gl.GenTextures(1, out uint textureID);
        _handle = textureID;
        Bind(TextureUnit.Texture0);

        for (int i = 0; i < 6; i++)
        {
            ImageResult result = ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(path, $"{i + 1}.png")),
                ColorComponents.RedGreenBlueAlpha);

            fixed (byte* ptr = result.Data)
            {
                _gl.TexImage2D(TextureTarget.TextureCubeMapPositiveX + i, 0, InternalFormat.Rgba, (uint)result.Width,
                    (uint)result.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, ptr);
            }
        }

        SetParameters();
    }

    public void Bind(TextureUnit textureSlot = TextureUnit.Texture0)
    {
        //When we bind a texture we can choose which textureslot we can bind it to.
        _gl.ActiveTexture(textureSlot);
        _gl.BindTexture(TextureTarget.TextureCubeMap, _handle);
    }

    private void SetParameters()
    {
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapS, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapT, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureWrapR, (int)GLEnum.ClampToEdge);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMinFilter, (int)GLEnum.Linear);
        _gl.TexParameter(TextureTarget.TextureCubeMap, TextureParameterName.TextureMagFilter, (int)GLEnum.Linear);
    }

    public void Dispose()
    {
        _gl.DeleteTexture(_handle);
    }
}
