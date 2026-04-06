using System.Numerics;
using GLCraft.Fundamental;
using Silk.NET.OpenGL;
using Shader = GLCraft.Fundamental.Shader;
using Texture = GLCraft.Fundamental.Texture;

namespace GLCraft.GameObjects;

public class Axes : GameObject
{
    private float[] Vertices =
    {
        0,0,0.02f, 0,0,1,1,
        0,0,1, 0,0,1,1,
        0.02f,0,1, 0,0,1,1,
        0.02f,0,0.02f, 0,0,1,1,
        
        0,0,0, 0,1,0,1,
        0,1,0, 0,1,0,1,
        0.02f,1,0, 0,1,0,1,
        0.02f,0,0, 0,1,0,1,
        
        0,0,0, 1,0,0,1,
        1,0,0, 1,0,0,1,
        1,0,0.02f, 1,0,0,1,
        0,0,0.02f, 1,0,0,1
    };

    private uint[] Indices =
    {
        0,1,2,
        0,2,3,
        
        4,5,6,
        4,6,7,
        
        8,9,10,
        8,10,11,
    };

    public Axes(GL gl, Shader shader, Transform transform)
    {
        Gl = gl;

        Texture = new Texture();
        Shader = shader;
        Transform = transform;
        
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
        Vao.Bind();
        Shader.Use();
        
        Shader.SetUniform("uModel", Transform.ViewMatrix);
        Shader.SetUniform("uView", view);
        Shader.SetUniform("uProjection", projection);
        
        Gl.DrawElements(GLEnum.Triangles,18, DrawElementsType.UnsignedInt, (void*) 0);
    }

    public override unsafe void Render(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection)
    {
        Vao.Bind();
        Shader.Use();
        
        Shader.SetUniform("uModel", model);
        Shader.SetUniform("uView", view);
        Shader.SetUniform("uProjection", projection);
        
        Gl.DrawElements(GLEnum.Triangles,18, DrawElementsType.UnsignedInt, (void*) 0);
    }

    public override void RegenerateFaces(int faceMask)
    {
        throw new NotImplementedException();
    }
}
