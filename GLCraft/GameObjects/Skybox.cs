using System.Numerics;
using GLCraft.Fundamental;
using Silk.NET.OpenGL;
using Shader = GLCraft.Fundamental.Shader;
using Texture = GLCraft.Fundamental.Texture;

namespace GLCraft.GameObjects;

public class Skybox : GameObject
{
    protected new float[] Vertices =
    {
        //X    Y      Z     U   V
        -0.5f, -0.5f, -0.5f, //-z
        0.5f, -0.5f, -0.5f,
        0.5f, 0.5f, -0.5f,
        0.5f, 0.5f, -0.5f,
        -0.5f, 0.5f, -0.5f,
        -0.5f, -0.5f, -0.5f,

        -0.5f, -0.5f, 0.5f, //+z
        0.5f, -0.5f, 0.5f,
        0.5f, 0.5f, 0.5f,
        0.5f, 0.5f, 0.5f,
        -0.5f, 0.5f, 0.5f,
        -0.5f, -0.5f, 0.5f,

        -0.5f, 0.5f, 0.5f, //-x
        -0.5f, 0.5f, -0.5f,
        -0.5f, -0.5f, -0.5f,
        -0.5f, -0.5f, -0.5f,
        -0.5f, -0.5f, 0.5f,
        -0.5f, 0.5f, 0.5f,

        0.5f, 0.5f, 0.5f, //+x
        0.5f, 0.5f, -0.5f,
        0.5f, -0.5f, -0.5f,
        0.5f, -0.5f, -0.5f,
        0.5f, -0.5f, 0.5f,
        0.5f, 0.5f, 0.5f,

        -0.5f, -0.5f, -0.5f, //bottom face (-y)
        0.5f, -0.5f, -0.5f,
        0.5f, -0.5f, 0.5f,
        0.5f, -0.5f, 0.5f,
        -0.5f, -0.5f, 0.5f,
        -0.5f, -0.5f, -0.5f,

        -0.5f, 0.5f, -0.5f, //top face (y)
        0.5f, 0.5f, -0.5f,
        0.5f, 0.5f, 0.5f,
        0.5f, 0.5f, 0.5f,
        -0.5f, 0.5f, 0.5f,
        -0.5f, 0.5f, -0.5f,
    };
    private CubeTexture texture;

    public Skybox(GL gl, Shader shader, CubeTexture texture)
    {
        Gl = gl;

        this.texture = texture;
        Shader = shader;

        Ebo = new BufferObject<uint>(Gl, new uint[1], BufferTargetARB.ElementArrayBuffer);
        Vbo = new BufferObject<float>(Gl, Vertices, BufferTargetARB.ArrayBuffer);
        Vao = new VertexArrayObject<float, uint>(Gl, Vbo, Ebo);

        Vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, 3, 0);

        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        Gl.BindVertexArray(0);
    }

    public override void Render(Matrix4x4 view, Matrix4x4 projection)
    {
        Vao.Bind();
        Shader.Use();
        texture.Bind(TextureUnit.Texture0);

        view.M41 = 0f;
        view.M42 = 0f;
        view.M43 = 0f;

        Shader.SetUniform("skybox", 0);
        Shader.SetUniform("uView", view);
        Shader.SetUniform("uProjection", projection);

        Gl.DrawArrays(PrimitiveType.Triangles, 0, 36);
    }

    public override void Render(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection)
    {
        throw new NotImplementedException();
    }

    public override void RegenerateFaces(int faceMask)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        Vao.Dispose();
        Ebo.Dispose();
        Vbo.Dispose();
        Shader.Dispose();
        texture.Dispose();
    }
}
