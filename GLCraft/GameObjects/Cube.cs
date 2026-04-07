using System.Numerics;
using GLCraft.Fundamental;
using Silk.NET.OpenGL;
using Shader = GLCraft.Fundamental.Shader;
using Texture = GLCraft.Fundamental.Texture;

namespace GLCraft.GameObjects;

public class Cube : GameObject
{
    private const int NegativeZFaceBit = 1 << 0;
    private const int PositiveZFaceBit = 1 << 1;
    private const int NegativeXFaceBit = 1 << 2;
    private const int PositiveXFaceBit = 1 << 3;
    private const int NegativeYFaceBit = 1 << 4;
    private const int PositiveYFaceBit = 1 << 5;
    private const int AllFacesMask =
        NegativeZFaceBit |
        PositiveZFaceBit |
        NegativeXFaceBit |
        PositiveXFaceBit |
        NegativeYFaceBit |
        PositiveYFaceBit;

    private const int FloatsPerVertex = 5;
    private const int VerticesPerFace = 6;
    private const int FloatsPerFace = FloatsPerVertex * VerticesPerFace;

    private readonly Dictionary<int, MeshVariant> _meshCache = new();
    private int _currentMeshKey = -1;
    private uint _vertexCount;

    protected new float[] Vertices =
    {
        //X    Y      Z     U   V
        -0.5f, -0.5f, -0.5f,  0.5f, 0.5f, //-z
        0.5f, -0.5f, -0.5f,  1f, 0.5f,
        0.5f,  0.5f, -0.5f,  1f, 0.0f,
        0.5f,  0.5f, -0.5f,  1f, 0.0f,
        -0.5f,  0.5f, -0.5f,  0.5f, 0.0f,
        -0.5f, -0.5f, -0.5f,  0.5f, 0.5f,

        -0.5f, -0.5f,  0.5f,  0.5f, 0.5f, //+z
        0.5f, -0.5f,  0.5f,  1.0f, 0.5f,
        0.5f,  0.5f,  0.5f,  1.0f, 0.0f,
        0.5f,  0.5f,  0.5f,  1.0f, 0.0f,
        -0.5f,  0.5f,  0.5f,  0.5f, 0.0f,
        -0.5f, -0.5f,  0.5f,  0.5f, 0.5f,

        -0.5f,  0.5f,  0.5f,  0.5f, 0.0f, //-x
        -0.5f,  0.5f, -0.5f,  1.0f, 0.0f,
        -0.5f, -0.5f, -0.5f,  1.0f, 0.5f,
        -0.5f, -0.5f, -0.5f,  1.0f, 0.5f,
        -0.5f, -0.5f,  0.5f,  0.5f, 0.5f,
        -0.5f,  0.5f,  0.5f,  0.5f, 0.0f,

        0.5f,  0.5f,  0.5f,  0.5f, 0.0f, //+x
        0.5f,  0.5f, -0.5f,  1.0f, 0.0f,
        0.5f, -0.5f, -0.5f,  1.0f, 0.5f,
        0.5f, -0.5f, -0.5f,  1.0f, 0.5f,
        0.5f, -0.5f,  0.5f,  0.5f, 0.5f,
        0.5f,  0.5f,  0.5f,  0.5f, 0.0f,

        -0.5f, -0.5f, -0.5f,  0.0f, 0.5f, //bottom face (-y)
        0.5f, -0.5f, -0.5f,  0.5f, 0.5f,
        0.5f, -0.5f,  0.5f,  0.5f, 0.0f,
        0.5f, -0.5f,  0.5f,  0.5f, 0.0f,
        -0.5f, -0.5f,  0.5f,  0.0f, 0.0f,
        -0.5f, -0.5f, -0.5f,  0.0f, 0.5f,

        -0.5f,  0.5f, -0.5f,  0.0f, 1.0f, //top face (y)
        0.5f,  0.5f, -0.5f,  0.5f, 1.0f,
        0.5f,  0.5f,  0.5f,  0.5f, 0.5f,
        0.5f,  0.5f,  0.5f,  0.5f, 0.5f,
        -0.5f,  0.5f,  0.5f,  0.0f, 0.5f,
        -0.5f,  0.5f, -0.5f,  0.0f, 1.0f
    };
    
    private float[] AltVertices =
    {
        //X    Y      Z     U   V
        -0.5f, -0.5f, -0.5f,  0f, 1f, //-z
        0.5f, -0.5f, -0.5f,  1f, 1f,
        0.5f,  0.5f, -0.5f,  1f, 0f,
        0.5f,  0.5f, -0.5f,  1f, 0f,
        -0.5f,  0.5f, -0.5f,  0f, 0f,
        -0.5f, -0.5f, -0.5f,  0f, 1f,

        -0.5f, -0.5f,  0.5f,  0f, 1f, //+z
        0.5f, -0.5f,  0.5f,  1f, 1f,
        0.5f,  0.5f,  0.5f,  1f, 0f,
        0.5f,  0.5f,  0.5f,  1f, 0f,
        -0.5f,  0.5f,  0.5f,  0f, 0f,
        -0.5f, -0.5f,  0.5f,  0f, 1f,

        -0.5f,  0.5f,  0.5f,  0f, 1f, //-x
        -0.5f,  0.5f, -0.5f,  1f, 1f,
        -0.5f, -0.5f, -0.5f,  1f, 0f,
        -0.5f, -0.5f, -0.5f,  1f, 0f,
        -0.5f, -0.5f,  0.5f,  0f, 0f,
        -0.5f,  0.5f,  0.5f,  0f, 1f,

        0.5f,  0.5f,  0.5f,  0.0f, 1f, //+x
        0.5f,  0.5f, -0.5f,  1f, 1f,
        0.5f, -0.5f, -0.5f,  1f, 0.0f,
        0.5f, -0.5f, -0.5f,  1f, 0.0f,
        0.5f, -0.5f,  0.5f,  0.0f, 0.0f,
        0.5f,  0.5f,  0.5f,  0.0f, 1f,

        -0.5f, -0.5f, -0.5f,  0.0f, 1f, //bottom face (-y)
        0.5f, -0.5f, -0.5f,  1f, 1f,
        0.5f, -0.5f,  0.5f,  1f, 0.0f,
        0.5f, -0.5f,  0.5f,  1f, 0.0f,
        -0.5f, -0.5f,  0.5f,  0.0f, 0.0f,
        -0.5f, -0.5f, -0.5f,  0.0f, 1f,

        -0.5f,  0.5f, -0.5f,  0.0f, 1f, //top face (y)
        0.5f,  0.5f, -0.5f,  1f, 1f,
        0.5f,  0.5f,  0.5f,  1f, 0.0f,
        0.5f,  0.5f,  0.5f,  1f, 0.0f,
        -0.5f,  0.5f,  0.5f,  0.0f, 0.0f,
        -0.5f,  0.5f, -0.5f,  0.0f, 1f
    };

    private sealed class MeshVariant
    {
        public required BufferObject<float> Vbo { get; init; }
        public required BufferObject<uint> Ebo { get; init; }
        public required VertexArrayObject<float, uint> Vao { get; init; }
        public required uint VertexCount { get; init; }

        public void Dispose()
        {
            Vao.Dispose();
            Ebo.Dispose();
            Vbo.Dispose();
        }
    }

    public Cube(GL gl, Shader shader, Texture texture, Transform transform, bool oneSprite)
    {
        Gl = gl;

        Shader = shader;
        Texture = texture;
        Transform = transform;

        if (oneSprite)
            Vertices = AltVertices;

        var fullMeshKey = AllFacesMask;
        var fullMesh = CreateMeshVariant(BuildMesh(fullMeshKey));
        _meshCache.Add(fullMeshKey, fullMesh);
        SetMesh(fullMesh, fullMeshKey);
    }

    public Cube(GL gl, float[] vertices, uint[] indices, Shader shader, Texture texture, Transform transform)
    {
        Gl = gl;

        Vertices = vertices;
        Shader = shader;
        Texture = texture;
        Transform = transform;

        SetMesh(CreateMeshVariant(vertices, indices), -1);
    }

    public override void RegenerateFaces(int faceMask)
    {
        if (faceMask == _currentMeshKey)
        {
            return;
        }

        if (!_meshCache.TryGetValue(faceMask, out var mesh))
        {
            var (vertices, indices) = BuildMesh(faceMask);
            mesh = CreateMeshVariant(vertices, indices);
            _meshCache.Add(faceMask, mesh);
        }

        SetMesh(mesh, faceMask);
    }

    private (float[] Vertices, uint[] Indices) BuildMesh(int faceMask)
    {
        List<float> vertices = new();
        List<uint> indices = new();

        if ((faceMask & NegativeZFaceBit) != 0)
        {
            AppendFace(vertices, indices, 0);
        }

        if ((faceMask & PositiveZFaceBit) != 0)
        {
            AppendFace(vertices, indices, 1);
        }

        if ((faceMask & NegativeXFaceBit) != 0)
        {
            AppendFace(vertices, indices, 2);
        }

        if ((faceMask & PositiveXFaceBit) != 0)
        {
            AppendFace(vertices, indices, 3);
        }

        if ((faceMask & NegativeYFaceBit) != 0)
        {
            AppendFace(vertices, indices, 4);
        }

        if ((faceMask & PositiveYFaceBit) != 0)
        {
            AppendFace(vertices, indices, 5);
        }

        return (vertices.ToArray(), indices.ToArray());
    }

    private void AppendFace(List<float> vertices, List<uint> indices, int faceIndex)
    {
        var vertexStart = (uint)(vertices.Count / FloatsPerVertex);
        var sourceOffset = faceIndex * FloatsPerFace;

        for (var i = 0; i < FloatsPerFace; i++)
        {
            vertices.Add(Vertices[sourceOffset + i]);
        }

        for (uint i = 0; i < VerticesPerFace; i++)
        {
            indices.Add(vertexStart + i);
        }
    }

    private MeshVariant CreateMeshVariant((float[] Vertices, uint[] Indices) mesh)
    {
        return CreateMeshVariant(mesh.Vertices, mesh.Indices);
    }

    private unsafe MeshVariant CreateMeshVariant(float[] vertices, uint[] indices)
    {
        var ebo = new BufferObject<uint>(Gl, indices, BufferTargetARB.ElementArrayBuffer);
        var vbo = new BufferObject<float>(Gl, vertices, BufferTargetARB.ArrayBuffer);
        var vao = new VertexArrayObject<float, uint>(Gl, vbo, ebo);

        vao.VertexAttributePointer(0, 3, VertexAttribPointerType.Float, FloatsPerVertex, 0);
        vao.VertexAttributePointer(1, 2, VertexAttribPointerType.Float, FloatsPerVertex, 3);

        Gl.BindBuffer(BufferTargetARB.ArrayBuffer, 0);
        Gl.BindVertexArray(0);

        return new MeshVariant
        {
            Vbo = vbo,
            Ebo = ebo,
            Vao = vao,
            VertexCount = (uint)(vertices.Length / FloatsPerVertex)
        };
    }

    private void SetMesh(MeshVariant mesh, int key)
    {
        Vbo = mesh.Vbo;
        Ebo = mesh.Ebo;
        Vao = mesh.Vao;
        _vertexCount = mesh.VertexCount;
        _currentMeshKey = key;
    }

    public override unsafe void Render(Matrix4x4 view, Matrix4x4 projection)
    {
        Vao.Bind();
        Shader.Use();
        Texture.Bind();
            
        //Setting a uniform.
        Shader.SetUniform("uTexture0", 0);
        
        Shader.SetUniform("uModel", Transform.ViewMatrix);
        Shader.SetUniform("uView", view);
        Shader.SetUniform("uProjection", projection);
        Gl.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
    }

    public unsafe void RenderInstanced(int faceMask, BufferObject<Vector3> instanceBuffer, uint instanceCount, Matrix4x4 view, Matrix4x4 projection)
    {
        if (instanceCount == 0)
        {
            return;
        }

        if (!_meshCache.TryGetValue(faceMask, out var mesh))
        {
            var (vertices, indices) = BuildMesh(faceMask);
            mesh = CreateMeshVariant(vertices, indices);
            _meshCache.Add(faceMask, mesh);
        }

        mesh.Vao.Bind();
        instanceBuffer.Bind();
        Gl.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, 3u * sizeof(float), (void*)0);
        Gl.EnableVertexAttribArray(2);
        Gl.VertexAttribDivisor(2, 1);

        Shader.Use();
        Texture.Bind();

        Shader.SetUniform("uTexture0", 0);
        Shader.SetUniform("uView", view);
        Shader.SetUniform("uProjection", projection);

        Gl.DrawArraysInstanced(PrimitiveType.Triangles, 0, mesh.VertexCount, instanceCount);
    }

    public override unsafe void Render(Matrix4x4 model, Matrix4x4 view, Matrix4x4 projection)
    {
        Vao.Bind();
        Shader.Use();
        Texture.Bind();
            
        //Setting a uniform.
        Shader.SetUniform("uTexture0", 0);
        
        Shader.SetUniform("uModel", model);
        Shader.SetUniform("uView", view);
        Shader.SetUniform("uProjection", projection);

        Gl.DrawArrays(PrimitiveType.Triangles, 0, _vertexCount);
    }

    public override void Dispose()
    {
        foreach (var mesh in _meshCache.Values)
        {
            mesh.Dispose();
        }

        if (_meshCache.Count == 0)
        {
            Vao.Dispose();
            Ebo.Dispose();
            Vbo.Dispose();
        }

        Shader.Dispose();
        Texture.Dispose();
    }
}
