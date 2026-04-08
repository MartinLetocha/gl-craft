using System.Numerics;
using GLCraft.Fundamental;
using Silk.NET.OpenGL;
using Shader = GLCraft.Fundamental.Shader;
using Texture = GLCraft.Fundamental.Texture;

namespace GLCraft.GameObjects;

public class Image : GameObject
{
    private new float[] Vertices =
    {
        0.5f,  0.5f, 0f,  1f, 1f,
        0.5f,  -0.5f, 0f,  0f, 1f,
        -0.5f,  0.5f,  0f,  1f, 0.0f,
        -0.5f,  0.5f,  0f,  1f, 0.0f,
        -0.5f,  -0.5f,  0f,  0.0f, 0.0f,
        0.5f,  -0.5f, 0f,  0.0f, 1f
    };

    private new uint[] Indices =
    {
        0,1,2,
        3,4,5
    };

    public Image(GL gl, Texture texture, Shader shader, Transform transform)
    {
        Gl = gl;
        Texture = texture;
        Shader = shader;
        Transform = transform;
        
        Ebo = new BufferObject<uint>(Gl, Indices, BufferTargetARB.ElementArrayBuffer);
        Vbo = new BufferObject<float>(Gl, Vertices, BufferTargetARB.ArrayBuffer);
        Vao = new VertexArrayObject<float, uint>(Gl, Vbo, Ebo);
        
        Vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 5, 0);
        Vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, 5, 3);
        
        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        Gl.BindVertexArray(0);
    }
    public override void Render(Matrix4x4 view, Matrix4x4 projection)
    {
        throw new NotImplementedException();
    }

    public override void Render(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection)
    {
        throw new NotImplementedException();
    }

    public Transform GetTransform()
    {
        return Transform;
    }
    public override unsafe void Render(Matrix4x4 projection)
    {
        Vao.Bind();
        Shader.Use();
        Texture.Bind();
        
        Shader.SetUniform("uTexture0", 0);
        Shader.SetUniform("uModel", Transform.ViewMatrix);
        Shader.SetUniform("uProjection", projection);

        Gl.DrawElements(GLEnum.Triangles,6, DrawElementsType.UnsignedInt, (void*) 0);
    }

    public override void RegenerateFaces(int faceMask)
    {
        throw new NotImplementedException();
    }
}