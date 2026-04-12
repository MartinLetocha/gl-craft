using System.Numerics;
using GLCraft.GameObjects;
using GLCraft.WorldGen;
using Silk.NET.OpenGL;

namespace GLCraft.UI;

public static class Loader
{
    public static int PercentageMaximum;
    public static int PercentageCurrent;
    public static string Message = "Loading...";
    public static bool StartedLoading = false;
    public static bool FinishedLoading = false;
    public static bool DoOnceAfterLoad = false;
    public static Image Background;
    public static Image SliderBackground;
    public static Image Slider;

    private static GL Gl;

    private static int seed;
    private static bool biggerOreGrowth;
    private static int moreOreGrowth;
    private static int oreChance;
    private static int carbonizer;
    private static int chunkAmount;
    private static int cX;
    private static int cY;
    
    public static void ChangeChunkSettings(int s, bool b, int m, int o, int c)
    {
        seed = s;
        biggerOreGrowth = b;
        moreOreGrowth = m;
        oreChance = o;
        carbonizer = c;
    }
    
    public static void CreateChunks(GL GlP, int amount, ref List<Chunk> chunks, ref List<Vector2> locations, ref Vector3 commandBlockLocation)
    {
        DoOnceAfterLoad = false;
        Gl = GlP;
        StartedLoading = true;
        FinishedLoading = false;
        int dimension = amount * 2 - 1;
        int fullChunks = dimension * dimension;
        int triangle = (int)((Math.Pow(amount - 1d, 2) + amount - 1d) / 2d);
        PercentageMaximum = fullChunks - triangle * 4;
        PercentageCurrent = 0;
        Message = "Loading spawn chunk...";

        foreach (var chunk in chunks)
        {
            chunk.Dispose();
        }

        chunks.Clear();
        
        amount--;
        cX = -amount;
        cY = -amount;
        chunkAmount = amount;
        Chunk spawn = new Chunk(Gl, Vector2.Zero, seed, biggerOreGrowth, moreOreGrowth, oreChance, carbonizer);
        chunks.Add(spawn);
        locations.Add(spawn.LeftBottom);
        commandBlockLocation = new Vector3(0.5f, GetHeightCommand(new Vector2(0.5f,0.5f),seed) + 1f, 0.5f);
        PercentageCurrent++;
    }

    public static void LoadChunk(ref List<Chunk> chunks, ref List<Vector2> locations)
    {
        Message = "Loading chunks...";
        if (PercentageCurrent >= 600)
        {
            Message = "Has to be done soon...";
        }
        else if (PercentageCurrent >= 300)
        {
            Message = "This is taking a while...";
        }
        
        LoadChunkLoop(ref chunks, ref locations);
        
        if (chunks.Count >= PercentageMaximum)
        {
            FinishedLoading = true;
            StartedLoading = false;
            Message = "Finished loading!";
            DoOnceAfterLoad = true;
        }
    }

    private static void LoadChunkLoop(ref List<Chunk> chunks, ref List<Vector2> locations)
    {
        for (; cX <= chunkAmount; cX++)
        {
            for (; cY <= chunkAmount; cY++)
            {
                int x = cX;
                int y = cY;
                if (Math.Abs(x) + Math.Abs(y) > chunkAmount || (x == 0 && y == 0))
                {
                    continue;
                }

                Chunk chunk = new Chunk(Gl, new Vector2(x, y), seed, biggerOreGrowth, moreOreGrowth, oreChance, carbonizer);
                chunks.Add(chunk);
                locations.Add(chunk.LeftBottom);
                PercentageCurrent++;
                cY++;
                return;
            }

            cY = -chunkAmount;
        }
    }
    
    private static float GetHeightCommand(Vector2 location, int seed)
    {
        int sampleCount = 1;
        float[] xCoords = new float[sampleCount];
        float[] yCoords = new float[sampleCount];
        xCoords[0] = location.X;
        yCoords[0] = location.Y;
        int index = 0;
        float[] output = new float[sampleCount];
        NoiseDotNet.NoiseSettings settings = new(xFreq: 0.1f, yFreq: 0.1f, seed: seed);

        NoiseDotNet.Noise.GradientNoise2D(
            xCoords: xCoords,
            yCoords: yCoords,
            output: output,
            settings);
        
        return output[0];
    }
}
