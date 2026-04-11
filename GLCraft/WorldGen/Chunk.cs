using System.Numerics;
using GLCraft.Fundamental;
using Silk.NET.OpenGL;

namespace GLCraft.WorldGen;

public class Chunk : IDisposable
{
    public sealed class RenderBatch : IDisposable
    {
        public required BlockType BlockType { get; init; }
        public required int FaceMask { get; init; }
        public required BufferObject<Vector3> InstanceBuffer { get; init; }
        public required uint InstanceCount { get; init; }

        public void Dispose()
        {
            InstanceBuffer.Dispose();
        }
    }

    private int width = 16;
    private int height = 16;
    private int depth = -64;
    private readonly GL _gl;
    public bool Render = true;
    public Vector2 LeftBottom;
    private Dictionary<Vector3, BlockType> transforms = new();
    private Dictionary<Vector3, int> visibleFaces = new();
    public List<RenderBatch> RenderBatches { get; } = new();
    private Random random;

    private bool BiggerOreGrowth;
    private int MoreOreGrowth;
    private int OreChance;
    private int Carbonizer;

    public Chunk(GL gl, Vector2 offset, int seed, bool oreGrowth, int moreOre, int _oreChance, int carbonizer)
    {
        _gl = gl;
        BiggerOreGrowth = oreGrowth;
        MoreOreGrowth = moreOre;
        OreChance = _oreChance;
        Carbonizer = carbonizer;
        
        random = new Random(seed);
        offset *= new Vector2(16, 16);
        float[] heightMap = GetHeightMap(offset, seed);
        
        LeftBottom = new Vector2(-width / 2 + (int)offset.X, -height / 2 + (int)offset.Y);
        int x = 0;
        int y = 0;
        Dictionary<Vector3, BlockType> ores = new();
        Dictionary<Vector3, BlockType> stones = new();
        for (float i = -width / 2 + (int)offset.X + 0.5f; i < width / 2 + (int)offset.X +0.5f; i++)
        {
            for (float j = -height / 2 + (int)offset.Y +0.5f; j < height / 2 + (int)offset.Y +0.5f; j++)
            {
                int bheight = (int)(heightMap[y * width + x] * 10);
                int dirtAmount = WeightedRandom(1, 4, [50d,30,15,5]);
                int deepslateAmount = WeightedRandom(1, 5, [5d,20,50,20,5]);
                for (int k = depth; k <= bheight; k++)
                {
                    float upgrade = OreChance / 4f;
                    int oreChance = WeightedRandom(1, 5, [96 - OreChance, 1 + upgrade, 1 + upgrade, 1 + upgrade, 1 + upgrade]);
                    if (i == 0.5f && j == 0.5f && k == bheight)
                    {
                        transforms.Add(new Vector3(i, k + 1, j), BlockType.CommandBlock);
                    }
                    if (k == bheight)
                    {
                        transforms.Add(new Vector3(i, k, j), BlockType.GrassBlock);
                    }
                    else if (k >= bheight - dirtAmount)
                    {
                        transforms.Add(new Vector3(i, k, j), BlockType.DirtBlock);
                    }
                    else if (k < depth + deepslateAmount)
                    {
                        transforms.Add(new Vector3(i, k, j), BlockType.Deepslate);
                    }
                    else if (oreChance != 1)
                    {
                        AddOre(oreChance, i, k, j, ref ores);
                    }
                    else
                    {
                        AddStone(i,k,j,ref stones);
                    }
                }

                y++;
            }

            x++;
            y = 0;
        }

        foreach (var ore in ores)
        {
            AttemptOreGrowth(ore.Key, ore.Value);
        }

        foreach (var stone in stones)
        {
            AttemptStoneGrowth(stone.Key, stone.Value);
        }
        
        Rebuild();
    }

    private void AttemptStoneGrowth(Vector3 position, BlockType type, int recursionDepth = 0)
    {
        if (!transforms.ContainsKey(position) || (transforms[position] != BlockType.StoneBlock && transforms[position] != type))
            return;
        int chance = (int)(40 * recursionDepth * 0.5f);
        int generated = random.Next(1,101);
        if (generated > chance)
        {
            transforms[position] = type;
            AttemptStoneGrowth(position + new Vector3(1,0,0), type, recursionDepth + 1);
            AttemptStoneGrowth(position + new Vector3(-1,0,0), type, recursionDepth + 1);
            AttemptStoneGrowth(position + new Vector3(0,0,1), type, recursionDepth + 1);
            AttemptStoneGrowth(position + new Vector3(0,0,-1), type, recursionDepth + 1);
            AttemptStoneGrowth(position + new Vector3(0,1,0), type, recursionDepth + 1);
            AttemptStoneGrowth(position + new Vector3(0,-1,0), type, recursionDepth + 1);
        }
    }
    private void AttemptOreGrowth(Vector3 position, BlockType type, int recursionDepth = 0)
    {
        if (!transforms.ContainsKey(position) || (transforms[position] != BlockType.StoneBlock && transforms[position] != type))
            return;
        if (type == BlockType.CoalOre && recursionDepth == 0)
        {
            int turnDiamond = random.Next(0, 101);
            if (turnDiamond > 100 - 25 * (Carbonizer - 1))
            {
                type = BlockType.DiamondOre;
            }
        }
        
        int chance = 0;
        if (BiggerOreGrowth)
        {
            chance = 60 - MoreOreGrowth + (int)type * recursionDepth;
        }
        else
        {
            chance = (60 - MoreOreGrowth + (int)type) * recursionDepth;
        }
        int generated = random.Next(1,101);
        if (generated > chance)
        {
            transforms[position] = type;
            AttemptOreGrowth(position + new Vector3(1,0,0), type, recursionDepth + 1);
            AttemptOreGrowth(position + new Vector3(-1,0,0), type, recursionDepth + 1);
            AttemptOreGrowth(position + new Vector3(0,0,1), type, recursionDepth + 1);
            AttemptOreGrowth(position + new Vector3(0,0,-1), type, recursionDepth + 1);
            AttemptOreGrowth(position + new Vector3(0,1,0), type, recursionDepth + 1);
            AttemptOreGrowth(position + new Vector3(0,-1,0), type, recursionDepth + 1);
        }
    }

    private void AddStone(float x, int y, float z, ref Dictionary<Vector3, BlockType> stones)
    {
        var location = new Vector3(x, y, z);
        int stone = random.Next(1,501);
        if (stone == 1)
        {
            int variant = random.Next(1,3);
            if (variant == 1)
            {
                transforms.Add(location, BlockType.Granite);
                stones.Add(location, BlockType.Granite);
            }
            else
            {
                transforms.Add(location, BlockType.Andesite);
                stones.Add(location, BlockType.Andesite);
            }
        }
        else
        {
            transforms.Add(location, BlockType.StoneBlock);
        }
    }
    private void AddOre(int oreChance, float x, int y, float z, ref Dictionary<Vector3, BlockType> ores)
    {
        var location = new Vector3(x, y, z);
        if (oreChance == 2)
        {
            transforms.Add(location, BlockType.CoalOre);
            ores.Add(location, BlockType.CoalOre);
        }
        else if (oreChance == 3 && y < depth * 0.5f / 3)
        {
            transforms.Add(location, BlockType.IronOre);
            ores.Add(location, BlockType.IronOre);
        }
        else if (oreChance == 4 && y < depth * 1f / 3)
        {
            transforms.Add(location, BlockType.GoldOre);
            ores.Add(location, BlockType.GoldOre);
        }
        else if (oreChance == 5 && y < depth * 2f / 3)
        {
            transforms.Add(location, BlockType.DiamondOre);
            ores.Add(location, BlockType.DiamondOre);
        }
        else
        {
            transforms.Add(location, BlockType.StoneBlock);
        }
    }

    private int WeightedRandom(int min, int max, double[] values)
    {
        if (max + 1 - min != values.Length)
            return min;
        double generated = random.Next(0, 100) + random.NextDouble();
        double lastValue = 0;
        for (var index = 0; index < values.Length; index++)
        {
            var value = values[index];
            lastValue += value;
            if (generated < lastValue)
                return min + index;
        }
        return min;
    }

    private float[] GetHeightMap(Vector2 offset, int seed)
    {
        int sampleCount = width * height;
        float[] xCoords = new float[sampleCount];
        float[] yCoords = new float[sampleCount];
        int index = 0;
        for (int y = 0; y < height; ++y)
        for (int x = 0; x < width; ++x)
        {
            xCoords[index] = x + offset.X;
            yCoords[index] = y + offset.Y;
            index++;
        }
        float[] output = new float[sampleCount];
        NoiseDotNet.NoiseSettings settings = new(xFreq: 0.1f, yFreq: 0.1f, seed: seed);

        NoiseDotNet.Noise.GradientNoise2D(
            xCoords: xCoords,
            yCoords: yCoords,
            output: output,
            settings);
        
        return output;
    }

    public bool HasBlock(Vector3 position)
    {
        bool found = transforms.TryGetValue(position, out var block);
        if (!found)
            return false;
        return true;
    }
    public BlockType RemoveBlock(Vector3 position)
    {
        BlockType block = transforms[position];
        transforms.Remove(position);
        return block;
    }

    public void Rebuild()
    {
        RebuildVisibleFaceCache();
        RebuildRenderBatches();
    }
        
    private void RebuildVisibleFaceCache()
    {
        visibleFaces.Clear();
        foreach (var position in transforms.Keys)
        {
            visibleFaces[position] = GetVisibleFaceMask(position);
        }
    }
    
    private void RebuildRenderBatches()
    {
        foreach (var batch in RenderBatches)
        {
            batch.Dispose();
        }

        RenderBatches.Clear();
        Dictionary<(BlockType BlockType, int FaceMask), List<Vector3>> positionsByBatch = new();

        foreach (var block in transforms)
        {
            if (!visibleFaces.TryGetValue(block.Key, out var faceMask))
            {
                faceMask = GetVisibleFaceMask(block.Key);
                visibleFaces[block.Key] = faceMask;
            }

            if (faceMask == 0)
            {
                continue;
            }

            var key = (block.Value, faceMask);
            if (!positionsByBatch.TryGetValue(key, out var positions))
            {
                positions = new List<Vector3>();
                positionsByBatch.Add(key, positions);
            }

            positions.Add(block.Key);
        }

        foreach (var batch in positionsByBatch)
        {
            RenderBatches.Add(new RenderBatch
            {
                BlockType = batch.Key.BlockType,
                FaceMask = batch.Key.FaceMask,
                InstanceBuffer = new BufferObject<Vector3>(_gl, batch.Value.ToArray(), BufferTargetARB.ArrayBuffer),
                InstanceCount = (uint)batch.Value.Count
            });
        }
    }
    private int GetVisibleFaceMask(Vector3 center)
    {
        float x = center.X;
        float y = center.Y;
        float z = center.Z;

        const int negativeZFaceBit = 1 << 0;
        const int positiveZFaceBit = 1 << 1;
        const int negativeXFaceBit = 1 << 2;
        const int positiveXFaceBit = 1 << 3;
        const int negativeYFaceBit = 1 << 4;
        const int positiveYFaceBit = 1 << 5;

        var result = 0;

        if (!transforms.ContainsKey(new Vector3(x, y, z - 1)))
        {
            result |= negativeZFaceBit;
        }

        if (!transforms.ContainsKey(new Vector3(x, y, z + 1)))
        {
            result |= positiveZFaceBit;
        }

        if (!transforms.ContainsKey(new Vector3(x - 1, y, z)))
        {
            result |= negativeXFaceBit;
        }

        if (!transforms.ContainsKey(new Vector3(x + 1, y, z)))
        {
            result |= positiveXFaceBit;
        }

        if (!transforms.ContainsKey(new Vector3(x, y - 1, z)))
        {
            result |= negativeYFaceBit;
        }

        if (!transforms.ContainsKey(new Vector3(x, y + 1, z)))
        {
            result |= positiveYFaceBit;
        }

        return result;
    }

    public void Dispose()
    {
        foreach (var batch in RenderBatches)
        {
            batch.Dispose();
        }

        RenderBatches.Clear();
    }
}
