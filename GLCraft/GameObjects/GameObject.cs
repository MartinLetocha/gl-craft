using System.Numerics;
using GLCraft.Fundamental;
using Silk.NET.OpenGL;
using Shader = GLCraft.Fundamental.Shader;
using Texture = GLCraft.Fundamental.Texture;

namespace GLCraft.GameObjects;

public abstract class GameObject : IDisposable
{
    protected BufferObject<float> Vbo;
    protected BufferObject<uint> Ebo;
    protected VertexArrayObject<float, uint> Vao;
    protected float[] Vertices;
    protected uint[] Indices;
    protected GL Gl;
    protected Shader Shader;
    protected Texture Texture;
    protected Transform Transform;

    public abstract void Render(Matrix4x4 view, Matrix4x4 projection);
    public abstract void Render(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection);
    public abstract void RegenerateFaces(int faceMask);

    public void EditTransform(Vector3 position)
    {
        Transform.Position = position;
    }
    public virtual void Dispose()
    {
        Vbo.Dispose();
        Ebo.Dispose();
        Vao.Dispose();
        Shader.Dispose();
        Texture.Dispose();
    }
}
