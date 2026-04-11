using System.Numerics;
using GLCraft.GameObjects;
using Silk.NET.OpenGL;

namespace GLCraft.Fundamental;

public class Ray : GameObject
{
    private uint _indexCount;

    public Ray(GL gl, Shader shader, Transform transform)
    {
        Gl = gl;

        Texture = new Texture();
        Shader = shader;
        Transform = transform;
    }

    public void FillBuffers(float[] vertices, uint[] indices)
    {
        Vertices = vertices;
        Indices = indices;
        _indexCount = (uint)indices.Length;
    }

    public void CreateBuffers()
    {
        Vao?.Dispose();
        Vbo?.Dispose();
        Ebo?.Dispose();

        if (_indexCount == 0)
        {
            return;
        }

        Ebo = new BufferObject<uint>(Gl, Indices, BufferTargetARB.ElementArrayBuffer);
        Vbo = new BufferObject<float>(Gl, Vertices, BufferTargetARB.ArrayBuffer);
        Vao = new VertexArrayObject<float, uint>(Gl, Vbo, Ebo);
        
        Vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 7, 0);
        Vao.VertexAttributePointer(1, 4, VertexAttribPointerType.Float, 7, 3);

        // Leave a clean GL state so later object setup does not mutate this VAO's EBO binding.
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        Gl.BindVertexArray(0);
    }
    
    public override unsafe void Render(Matrix4x4 view, Matrix4x4 projection)
    {
        if (_indexCount == 0)
        {
            return;
        }

        Vao.Bind();
        Shader.Use();
        
        Shader.SetUniform("uModel", Transform.ViewMatrix);
        Shader.SetUniform("uView", view);
        Shader.SetUniform("uProjection", projection);

        Gl.LineWidth(3f);
        Gl.DrawElements(GLEnum.LineStrip, _indexCount, DrawElementsType.UnsignedInt, (void*)0);
    }

    public override unsafe void Render(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection)
    {
        if (_indexCount == 0)
        {
            return;
        }

        Vao.Bind();
        Shader.Use();

        Shader.SetUniform("uModel", model);
        Shader.SetUniform("uView", view);
        Shader.SetUniform("uProjection", projection);

        Gl.LineWidth(3f);
        Gl.DrawElements(GLEnum.LineStrip, _indexCount, DrawElementsType.UnsignedInt, (void*)0);
    }

    public override void RegenerateFaces(int faceMask)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        Shader.Dispose();
    }
}
