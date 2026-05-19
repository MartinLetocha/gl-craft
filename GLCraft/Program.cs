using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Text;
using FontStash.NET;
using GLCraft.Fonts;
using GLCraft.Fundamental;
using GLCraft.GameObjects;
using GLCraft.UI;
using GLCraft.WorldGen;
using Silk.NET.Core;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using SilkyNvg;
using StbImageSharp;
using Button = GLCraft.UI.Button;
using Shader = GLCraft.Fundamental.Shader;
using Texture = GLCraft.Fundamental.Texture;

namespace GLCraft;

static class Program
{
    private const int NearbyBlockSearchRadius = 3;
    private const int MaxNearbyBlocks = 2 + 6 * ((NearbyBlockSearchRadius * 2 + 1) * (NearbyBlockSearchRadius * 2 + 1));
    private const float CommandBlockActiveRadiusSquared = 25f;
    private const float PlayerFootOffset = 1.69f;
    private const float PlayerHeadOffset = 0.2f;
    private const float PlayerRadius = 0.35f;
    private const float MidBodyProbeOffset = 1.0f;
    private const float LowerBodyProbeOffset = 1.55f;
    private const float GravityAcceleration = 18f;
    private const float TerminalFallSpeed = 30f;
    private const float JumpForce = 180f;
    private const float JumpDecay = 1;
    private const float JumpLength = 0.5f;
    private const float JumpDelay = 0.3f;
    private static float JumpDelayCurrent = 0;
    private static bool JumpDelayStarted = false;
    private static float JumpCurrent = 0;
    private static bool Jumping = false;
    private const float GroundSnapEpsilon = 0.02f;
    private const float GroundedStickDistance = 0.08f;
    private const float GroundProbeDepth = 0.05f;

    //Base
    private static IWindow _window;
    private static GL Gl;
    private static IKeyboard primaryKeyboard;
    private static IMouse primaryMouse;

    private static int Width = 800;
    private static int Height = 700;
    private static Matrix4x4 UiProjection;
    private static Matrix4x4 FontProjection;
    
    //Camera
    private static Vector3 CameraPosition = new Vector3(0.5f, 0.0f, 0.5f);
    private static Vector3 CameraTarget = Vector3.Zero;
    private static Vector3 CameraDirection = Vector3.Normalize(CameraPosition - CameraTarget);
    private static Vector3 CameraRight = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, CameraDirection));
    private static Vector3 CameraFront = new Vector3(0.0f, 0.0f, -1.0f);
    private static Vector3 CameraUp = Vector3.Cross(CameraDirection, CameraRight);
    private static float CameraYaw = -90f;
    private static float CameraPitch = 0f;
    private static float CameraZoom = 45f;

    private static Vector2 LastMousePosition;
    
    //Important stuff
    private static List<GameObject> gameObjects = new List<GameObject>();
    private static Dictionary<BlockType, Cube> blockRenderers = new();
    private static List<Chunk> chunks = new();
    private static List<Vector2> chunkPositions = new();
    
    private static Skybox Skybox;

    //UI
    private static GLFons FontRenderer;
    private static Fontstash FontStash;
    private static int FontNormal;
    private static int fpsResetLimit = 20;
    private static int fpsResetCounter = 0;
    private static double fpsLast = 0f;

    //Optimization
    private static int RenderDistance = 9;
    private static Vector2 LastActiveChunk = new Vector2(float.NaN, float.NaN);

    //paths
    private const string TEXTURE_PATH = @"Textures\";
    private const string SHADER_LOCATION = @"Shaders\";
    private const string SPECIAL_RESOURCE_PATH = @"App\";
    private const string APP_LOGO = @"GLCraftLogo.png";
    
    //actual game stuff
    private static int seed;
    
    private static float SprintSpeed = 2f;

    private static Vector3 commandBlockLocation = Vector3.Zero;
    public static int chunkAmount = 1;
    public static bool biggerOreGrowth = false;
    public static int moreOreGrowth = 0;
    public static int oreChance = 0;
    public static int carbonizer = 1;
    
    //collision detection
    private static List<Vector3> NearbyBlocks = new List<Vector3>(MaxNearbyBlocks);
    private static List<int> NearbyBlockChunks = new List<int>(MaxNearbyBlocks);
    private static HashSet<Vector3> NearbyBlockSet = new HashSet<Vector3>();
    //private static List<Vector3> Ray = new List<Vector3>(64);
    //private static Ray RayObject;
    private static readonly StringBuilder DebugTextBuilder = new(64);
    private static float VerticalVelocity;
    private static readonly Vector2[] HorizontalProbeOffsets =
    [
        new Vector2(PlayerRadius, 0f),
        new Vector2(-PlayerRadius, 0f),
        new Vector2(0f, PlayerRadius),
        new Vector2(0f, -PlayerRadius)
    ];
    private static readonly Vector2[] FootProbeOffsets =
    [
        Vector2.Zero,
        new Vector2(PlayerRadius, 0f),
        new Vector2(-PlayerRadius, 0f),
        new Vector2(0f, PlayerRadius),
        new Vector2(0f, -PlayerRadius)
    ];
    
    
    //BUGS:
    //something on resize probably
    //command block Y level is not correct
    //jumping keeps you in air
    //TODO maybes:
    //Auto miner
    //Tree gen
    static void Main(string[] args)
    {
        WindowOptions options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(800, 600),
            Title = "GLCraft",
            WindowState = WindowState.Maximized,
            WindowBorder = WindowBorder.Resizable,
            Position = new Vector2D<int>(50, 50)
        };
        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Update += OnUpdate;
        _window.Render += OnRender;
        _window.FramebufferResize += OnResize;
        _window.Closing += OnClose;

        _window.Run();

        _window.Dispose();
    }

    private static void OnClose()
    {
        foreach (var chunk in chunks)
        {
            chunk.Dispose();
        }

        foreach (var gameObject in gameObjects)
        {
            gameObject.Dispose();
        }

        Skybox?.Dispose();
        //RayObject?.Dispose();
        FontRenderer?.Dispose();
        Loader.Background.Dispose();
        Loader.Slider.Dispose();
        Loader.SliderBackground.Dispose();
    }

    private static unsafe void OnLoad()
    {
        seed = new Random().Next();
        Loader.Message = "Initializing GLCraft...";
        Loader.ChangeChunkSettings(seed, biggerOreGrowth, moreOreGrowth, oreChance, carbonizer);
        
        ImageResult image = ImageResult.FromMemory(File.ReadAllBytes(Path.Combine("Resources", SPECIAL_RESOURCE_PATH, APP_LOGO)),
            ColorComponents.RedGreenBlueAlpha);
        RawImage icon = new RawImage(image.Width, image.Height, image.Data);
        _window.SetWindowIcon(ref icon);

        IInputContext input = _window.CreateInput();
        primaryKeyboard = input.Keyboards.FirstOrDefault();
        if (primaryKeyboard != null)
        {
            primaryKeyboard.KeyDown += KeyDown;
        }

        primaryMouse = input.Mice.FirstOrDefault();
        primaryMouse.Cursor.CursorMode = CursorMode.Raw;
        primaryMouse.MouseMove += OnMouseMove;
        primaryMouse.Scroll += OnMouseWheel;
        primaryMouse.MouseUp += OnMouseClick;

        Gl = GL.GetApi(_window);

        UIHandler.Gl = Gl;
        FontRenderer = new GLFons(Gl);
        FontStash = FontRenderer.Create(512, 512, (int)FonsFlags.ZeroTopleft);
        FontNormal = FontStash.AddFont("testFont", Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Fonts\\Verdana.ttf")
            , 0);

        uint fontColourRed = GetColor(255, 0, 0, 255);

        FontStash.SetFont(FontNormal);
        FontStash.SetSize(72.0f);
        FontStash.SetColour(fontColourRed);

        Gl.ClearColor(Color.CornflowerBlue);
        Gl.Enable(EnableCap.Blend);
        Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var skyboxTexture = new CubeTexture(Gl, Path.Combine(Directory.GetCurrentDirectory(), "Resources", "Skybox\\"));
        var skyboxShader = new Shader(Gl, GetShaderLocation("skybox", GLEnum.VertexShader), GetShaderLocation("skybox", GLEnum.FragmentShader));
        Skybox = new Skybox(Gl, skyboxShader, skyboxTexture);
        
        AddBlocks();

        FontStash.SetFont(FontNormal);
        FontStash.SetSize(36.0f);
        FontStash.SetColour(GLFons.Rgba(255, 255, 255, 255));
        
        foreach (BlockType block in (BlockType[]) Enum.GetValues(typeof(BlockType)))
        {
            UIHandler.Resources.Add(block, 0);
        }


        Width = _window.Size.X;
        Height = _window.Size.Y;
        UpdateProjectionMatrices();
        Gl.Viewport(0, 0, (uint)_window.Size.X, (uint)_window.Size.Y);
        
        var uishader = new Shader(Gl, GetShaderLocation("uishader", GLEnum.VertexShader),
            GetShaderLocation("shader", GLEnum.FragmentShader));

        var keyTexture = new Texture(Gl, Path.Combine("Resources", SPECIAL_RESOURCE_PATH, "EKey.png"));
        var cursor = new Texture(Gl, Path.Combine("Resources",SPECIAL_RESOURCE_PATH,"Middle.png"));
        var commandBg = new Texture(Gl, Path.Combine("Resources",SPECIAL_RESOURCE_PATH, "commandbg.png"));
        var btnBg = new Texture(Gl, Path.Combine("Resources",SPECIAL_RESOURCE_PATH, "btnbg.png"));
        UIHandler.Key = new Image(Gl, keyTexture, uishader, new Transform() {Position = new Vector3(Width / 2.1f, 0,0), ScaleX = Height / 15f, ScaleY = Height / 15f, Rotation = Quaternion.CreateFromAxisAngle(new Vector3(0,0,1), -90 * (float)Math.PI / 180)});
        UIHandler.Background = new Image(Gl, commandBg, uishader, new Transform(){Position = Vector3.Zero, ScaleX = Width / 1.2f, ScaleY = Height / 1.2f});
        UIHandler.Cursor = new Image(Gl, cursor, uishader, new Transform(){Position = Vector3.Zero, ScaleX = Height / 20f, ScaleY = Height / 20f});
        float btnX = Width / 8f;
        float btnY = Height / 13f;
        for (int i = 0; i < 5; i++)
        {
            Vector3 btnLocation = new Vector3(-btnX, btnY * (4 - i) - 20 * i, 0);
            Image btn = new Image(Gl, btnBg, uishader,
                new Transform() { Position = btnLocation, ScaleX = btnX, ScaleY = btnY });
            UIHandler.Buttons.Add(new Button(i, btn, "Buy!", new Vector2(Width / 2f - btnX / 2f + btnLocation.X, Height / 2f - btnY / 2f - btnLocation.Y), new Vector2(btnX, btnY)));
        }
        Vector3 btnRLocation = new Vector3(0, btnY * -4.5f, 0);
        Image btnR = new Image(Gl, btnBg, uishader,
            new Transform() { Position = btnRLocation, ScaleX = btnX, ScaleY = btnY });
        UIHandler.Buttons.Add(new Button(100, btnR, "Regenerate Map", new Vector2(Width / 2f - btnX / 2f + btnRLocation.X, Height / 2f - btnY / 2f - btnRLocation.Y), new Vector2(btnX, btnY)));
        var bgTexture = new Texture(Gl, Path.Combine("Resources", SPECIAL_RESOURCE_PATH, "bg.png"));
        var sliderbgTexture = new Texture(Gl, Path.Combine("Resources", SPECIAL_RESOURCE_PATH, "sliderbg.png"));
        var sliderTexture = new Texture(Gl, Path.Combine("Resources", SPECIAL_RESOURCE_PATH, "slider.png"));
        Loader.Background = new Image(Gl, bgTexture, uishader, new Transform() {Position = new Vector3(0,0,0), ScaleX = Width, ScaleY = Height});
        Loader.Slider = new Image(Gl, sliderTexture, uishader, new Transform() {Position = new Vector3(0,0,0), ScaleX = Width / 3f, ScaleY = Height / 10f});
        Loader.SliderBackground = new Image(Gl, sliderbgTexture, uishader, new Transform() {Position = new Vector3(0,0,0), ScaleX = Width / 3f, ScaleY = Height / 10f});
        Loader.CreateChunks(Gl, chunkAmount, ref chunks, ref chunkPositions, ref commandBlockLocation);
    }

    public static void ResetChunks()
    {
        LastMousePosition = UIHandler.HandleCommandBlockUI(primaryMouse, Width, Height, LastMousePosition);
        seed = new Random().Next();
        Loader.ChangeChunkSettings(seed, biggerOreGrowth, moreOreGrowth, oreChance, carbonizer);
        Loader.CreateChunks(Gl, chunkAmount, ref chunks, ref chunkPositions, ref commandBlockLocation);
    }
    private static void OnMouseClick(IMouse arg1, MouseButton arg2)
    {
        if (!Loader.FinishedLoading)
            return;

        if (UIHandler.BlockCameraAndMovement)
        {
            if (arg2 == MouseButton.Left)
            {
                UIHandler.CheckClick(arg1.Position);
            }
            return;
        }

        //Ray.Clear();
        Vector3? hitBlock = null;
        int hitChunkIndex = -1;
        const float rayDistance = 5f;
        const float rayStep = 0.1f;

        for (float i = 0; i <= rayDistance; i += rayStep)
        {
            var raycastPoint = CameraPosition + i * CameraFront;
            //Ray.Add(raycastPoint);

            for (var index = 0; index < NearbyBlocks.Count; index++)
            {
                var nearbyBlock = NearbyBlocks[index];
                if (PointInsideBlock(raycastPoint, nearbyBlock))
                {
                    hitBlock = nearbyBlock;
                    hitChunkIndex = NearbyBlockChunks[index];
                    if(hitChunkIndex != -1 && chunks[hitChunkIndex].HasBlock(hitBlock.Value))
                        goto RaycastFinished;
                }
            }
        }

        RaycastFinished:
        //UpdateDebugRay();
        
        if (arg2 == MouseButton.Left && hitBlock.HasValue && hitChunkIndex >= 0)
        {
            if (chunks[hitChunkIndex].HasBlock(hitBlock.Value))
            {
                UIHandler.Resources[chunks[hitChunkIndex].RemoveBlock(hitBlock.Value)]++;
                chunks[hitChunkIndex].Rebuild();
            }
        }
    }

    private static bool PointInsideBlock(Vector3 point, Vector3 blockCenter)
    {
        const float halfBlockSize = 0.5f;
        return blockCenter.X - halfBlockSize < point.X && point.X < blockCenter.X + halfBlockSize &&
               blockCenter.Y - halfBlockSize < point.Y && point.Y < blockCenter.Y + halfBlockSize &&
               blockCenter.Z - halfBlockSize < point.Z && point.Z < blockCenter.Z + halfBlockSize;
    }

    // private static void UpdateDebugRay()
    // {
    //     float[] vertexBuffer = new float[Ray.Count * 7];
    //     uint[] indexBuffer = new uint[Ray.Count];
    //
    //     for (var index = 0; index < Ray.Count; index++)
    //     {
    //         var rayPoint = Ray[index];
    //         int vertexOffset = index * 7;
    //
    //         vertexBuffer[vertexOffset] = rayPoint.X;
    //         vertexBuffer[vertexOffset + 1] = rayPoint.Y;
    //         vertexBuffer[vertexOffset + 2] = rayPoint.Z;
    //         vertexBuffer[vertexOffset + 3] = 1f;
    //         vertexBuffer[vertexOffset + 4] = 0f;
    //         vertexBuffer[vertexOffset + 5] = 0f;
    //         vertexBuffer[vertexOffset + 6] = 1f;
    //         indexBuffer[index] = (uint)index;
    //     }
    //
    //     RayObject.FillBuffers(vertexBuffer, indexBuffer);
    //     RayObject.CreateBuffers();
    // }

    private static void AddBlocks()
    {
        var solidShader = new Shader(Gl, GetShaderLocation("solid", GLEnum.VertexShader),
            GetShaderLocation("solid", GLEnum.FragmentShader));
        var texture = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "grassBlock.png"));
        var cobble = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "cobblestone.png"));
        var stone = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "stoneBlock.png"));
        var dirt = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "dirt.png"));
        var deepslate = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "deepslate.png"));
        var coal = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "coalOre.png"));
        var iron = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "ironOre.png"));
        var gold = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "goldOre.png"));
        var diamond = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "diamondOre.png"));
        var granite = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "granite.png"));
        var andesite = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "andesite.png"));
        var command = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "commandBlock.png"));
        var log = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "woodLog.png"));
        var wood = new Texture(Gl, Path.Combine("Resources", TEXTURE_PATH, "woodBlock.png"));
        
        //var axes = new Axes(Gl, solidShader, new Transform() { Position = new Vector3(0, 0f, 0) });
        //gameObjects.Add(axes);
        
        var cubeShader = new Shader(Gl, GetShaderLocation("instanced", GLEnum.VertexShader),
            GetShaderLocation("shader", GLEnum.FragmentShader));
        
        var grassCube = new Cube(Gl, cubeShader, texture, new Transform(), false);
        var stoneCube = new Cube(Gl, cubeShader, stone, new Transform(), true);
        var cobbleCube = new Cube(Gl, cubeShader, cobble, new Transform(), true);
        var dirtCube = new Cube(Gl, cubeShader, dirt, new Transform(), true);
        var deepslateCube = new Cube(Gl, cubeShader, deepslate, new Transform(), true);
        var coalCube = new Cube(Gl, cubeShader, coal, new Transform(), true);
        var ironCube = new Cube(Gl, cubeShader, iron, new Transform(), true);
        var goldCube = new Cube(Gl, cubeShader, gold, new Transform(), true);
        var diamondCube = new Cube(Gl, cubeShader, diamond, new Transform(), true);
        var graniteCube = new Cube(Gl, cubeShader, granite, new Transform(), true);
        var andesiteCube = new Cube(Gl, cubeShader, andesite, new Transform(), true);
        var commandCube = new Cube(Gl, cubeShader, command, new Transform(), true);
        var logCube = new Cube(Gl, cubeShader, log, new Transform(), true);
        var woodLog = new Cube(Gl, cubeShader, wood, new Transform(), true);
        //RayObject = new(Gl, solidShader, new Transform());
        
        gameObjects.Add(grassCube);
        gameObjects.Add(stoneCube);
        gameObjects.Add(cobbleCube);
        gameObjects.Add(dirtCube);
        gameObjects.Add(deepslateCube);
        gameObjects.Add(coalCube);
        gameObjects.Add(ironCube);
        gameObjects.Add(goldCube);
        gameObjects.Add(diamondCube);
        gameObjects.Add(graniteCube);
        gameObjects.Add(andesiteCube);
        gameObjects.Add(commandCube);
        gameObjects.Add(logCube);
        gameObjects.Add(woodLog);
        
        blockRenderers.Add(BlockType.GrassBlock, grassCube);
        blockRenderers.Add(BlockType.StoneBlock, stoneCube);
        blockRenderers.Add(BlockType.CobblestoneBlock, cobbleCube);
        blockRenderers.Add(BlockType.DirtBlock, dirtCube);
        blockRenderers.Add(BlockType.Deepslate, deepslateCube);
        blockRenderers.Add(BlockType.CoalOre, coalCube);
        blockRenderers.Add(BlockType.IronOre, ironCube);
        blockRenderers.Add(BlockType.GoldOre, goldCube);
        blockRenderers.Add(BlockType.DiamondOre, diamondCube);
        blockRenderers.Add(BlockType.Granite, graniteCube);
        blockRenderers.Add(BlockType.Andesite, andesiteCube);
        blockRenderers.Add(BlockType.CommandBlock, commandCube);
        blockRenderers.Add(BlockType.Log, logCube);
        blockRenderers.Add(BlockType.WoodBlock, woodLog);
    }
    

    private static uint GetColor(byte r, byte g, byte b, byte a)
    {
        return (uint)((r) | (g << 8) | (b << 16) | (a << 24));
    }

    private static void OnUpdate(double deltaTime)
    {
        if (!Loader.FinishedLoading || UIHandler.BlockCameraAndMovement)
            return;
        
        var moveSpeed = 2.5f * (float)deltaTime;
        if (primaryKeyboard.IsKeyPressed(Key.ShiftLeft))
        {
            moveSpeed /= SprintSpeed;
        }
        else if (primaryKeyboard.IsKeyPressed(Key.ControlLeft))
        {
            moveSpeed *= SprintSpeed;
        }

        CameraZoom = primaryKeyboard.IsKeyPressed(Key.C) ? 0.1f : 45f;
        Vector3 movement = Vector3.Zero;
        Vector3 flatFront = Vector3.Normalize(new Vector3(CameraFront.X, 0f, CameraFront.Z));
        if (float.IsNaN(flatFront.X))
        {
            flatFront = -Vector3.UnitZ;
        }

        Vector3 strafeDirection = Vector3.Normalize(Vector3.Cross(flatFront, Vector3.UnitY));
        if (primaryKeyboard.IsKeyPressed(Key.W))
        {
            movement += flatFront * moveSpeed;
        }

        if (primaryKeyboard.IsKeyPressed(Key.S))
        {
            movement -= flatFront * moveSpeed;
        }

        if (primaryKeyboard.IsKeyPressed(Key.A))
        {
            movement -= strafeDirection * moveSpeed;
        }

        if (primaryKeyboard.IsKeyPressed(Key.D))
        {
            movement += strafeDirection * moveSpeed;
        }

        RefreshNearbyBlocks(CameraPosition);
        TryMoveHorizontal(movement);
        Jump((float)deltaTime);
        ApplyGravity((float)deltaTime);
        RefreshNearbyBlocks(CameraPosition);
    }

    private static void Jump(float deltaTime)
    {
        if (JumpDelayStarted)
        {
            JumpDelayCurrent += deltaTime;
            if (JumpDelayCurrent >= JumpDelay)
            {
                JumpDelayCurrent = 0;
                JumpDelayStarted = false;
            }
            else
                return;
        }
        if (Jumping)
        {
            JumpCurrent += deltaTime;
            if (JumpCurrent < JumpLength)
            {
                VerticalVelocity = VerticalVelocity + (JumpForce - JumpDecay * JumpCurrent * 1500) * deltaTime;
                VerticalVelocity = Math.Clamp(VerticalVelocity, 0, 1000f);
                if(VerticalVelocity == 0)
                    JumpCurrent += deltaTime * 3;
            }
            else
            {
                Jumping = false;
                JumpCurrent = 0;
                JumpDelayStarted = true;
            }
        }
    }

    private static string GetShaderLocation(string name, GLEnum type)
    {
        if (type == GLEnum.VertexShader)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Resources",  SHADER_LOCATION + name + ".vert");
        }

        if (type == GLEnum.FragmentShader)
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Resources",  SHADER_LOCATION + name + ".frag");
        }

        return "";
    }

    private static unsafe void OnRender(double deltaTime)
    {
        Gl.Enable(EnableCap.DepthTest);
        Gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        if (!Loader.FinishedLoading && Loader.StartedLoading)
        {
            float onePercent = Loader.PercentageMaximum / 100f;
            float currentPercent = (Loader.PercentageCurrent / onePercent + 1) / 100f;
            float larped = (1 - currentPercent) * Width / 300 + currentPercent * Width / 3;
            Loader.LoadChunk(ref chunks, ref chunkPositions);
            Loader.Slider.Render(UiProjection);
            Loader.Slider.EditTransform(new Vector3((-larped / 2 + larped) - Width / 6f, 0,0), larped, Height / 10f);
            Loader.SliderBackground.Render(UiProjection);
            Loader.Background.Render(UiProjection);
            Gl.Disable(EnableCap.DepthTest);
            DrawLoadingScreenText();
            return;
        }

        if (Loader.DoOnceAfterLoad)
        {
            CameraPosition = commandBlockLocation + new Vector3(0, 5, 0);
            VerticalVelocity = 0f;
            Loader.DoOnceAfterLoad = false;
        }

        if (CameraPosition.Y < -80)
        {
            CameraPosition = commandBlockLocation + new Vector3(0, 2, 0);
            VerticalVelocity = 0f;
        }

        var view = Matrix4x4.CreateLookAt(CameraPosition, CameraPosition + CameraFront, CameraUp);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathHelper.DegreesToRadians(CameraZoom),
            (float)Width / Height, 0.1f, 200.0f);

        var activeChunk = GetActiveChunkLocation(CameraPosition);
        if (activeChunk != LastActiveChunk)
        {
            PickChunksForRendering(activeChunk);
            LastActiveChunk = activeChunk;
        }

        foreach (var chunk in chunks)
        {
            if (!chunk.Render)
                continue;
            foreach (var batch in chunk.RenderBatches)
            {
                blockRenderers[batch.BlockType].RenderInstanced(batch.FaceMask, batch.InstanceBuffer, batch.InstanceCount, view, projection);
            }
        }
        //RayObject.Render(view, projection);

        Gl.DepthFunc(DepthFunction.Lequal);
        Skybox.Render(view, projection);
        Gl.DepthFunc(DepthFunction.Less);

        Gl.Disable(EnableCap.DepthTest);
                
        if (UIHandler.BlockCameraAndMovement)
        {
            UIHandler.DrawCommandBlockUI(UiProjection, ref FontRenderer, ref FontStash, Width, Height);
        }
        DrawPersistentUI(UiProjection);
        
        DrawDebug(activeChunk, deltaTime);
    }

    private static void TryMoveHorizontal(Vector3 movement)
    {
        if (movement.X != 0f)
        {
            Vector3 candidate = CameraPosition + new Vector3(movement.X, 0f, 0f);
            RefreshNearbyBlocks(candidate);
            if (!CollidesWithBody(candidate))
            {
                CameraPosition = candidate;
            }
        }

        if (movement.Z != 0f)
        {
            Vector3 candidate = CameraPosition + new Vector3(0f, 0f, movement.Z);
            RefreshNearbyBlocks(candidate);
            if (!CollidesWithBody(candidate))
            {
                CameraPosition = candidate;
            }
        }
    }

    private static void ApplyGravity(float deltaTime)
    {
        RefreshNearbyBlocks(CameraPosition);
        if (TryGetGroundedCameraY(CameraPosition, out float currentGroundedY) &&
            CameraPosition.Y - currentGroundedY <= GroundedStickDistance &&
            VerticalVelocity <= 0f)
        {
            CameraPosition = new Vector3(CameraPosition.X, currentGroundedY, CameraPosition.Z);
            VerticalVelocity = 0f;
            return;
        }

        VerticalVelocity = MathF.Max(VerticalVelocity - GravityAcceleration * deltaTime, -TerminalFallSpeed);
        float verticalDelta = VerticalVelocity * deltaTime;
        if (verticalDelta == 0f)
        {
            return;
        }

        Vector3 candidate = CameraPosition + new Vector3(0f, verticalDelta, 0f);
        RefreshNearbyBlocks(candidate);

        if (verticalDelta < 0f && TryGetGroundedCameraY(candidate, out float groundedCameraY))
        {
            CameraPosition = new Vector3(candidate.X, groundedCameraY, candidate.Z);
            VerticalVelocity = 0f;
            return;
        }

        if (verticalDelta > 0f && CollidesWithCeiling(candidate, out float ceilingCameraY))
        {
            CameraPosition = new Vector3(candidate.X, ceilingCameraY, candidate.Z);
            VerticalVelocity = 0f;
            return;
        }

        CameraPosition = candidate;
    }

    private static bool CollidesWithBody(Vector3 position)
    {
        foreach (var offset in HorizontalProbeOffsets)
        {
            Vector3 upperBodyProbe = position + new Vector3(offset.X, -PlayerHeadOffset, offset.Y);
            if (TryGetSolidBlockContainingPoint(upperBodyProbe, out _))
                return true;

            Vector3 middleBodyProbe = position + new Vector3(offset.X, -MidBodyProbeOffset, offset.Y);
            if (TryGetSolidBlockContainingPoint(middleBodyProbe, out _))
                return true;

            Vector3 lowerBodyProbe = position + new Vector3(offset.X, -LowerBodyProbeOffset, offset.Y);
            if (TryGetSolidBlockContainingPoint(lowerBodyProbe, out _))
                return true;
        }

        return false;
    }

    private static bool TryGetGroundedCameraY(Vector3 position, out float groundedCameraY)
    {
        groundedCameraY = float.MinValue;
        bool hitGround = false;

        foreach (var offset in FootProbeOffsets)
        {
            Vector3 footProbe = position + new Vector3(offset.X, -(PlayerFootOffset + GroundProbeDepth), offset.Y);
            if (!TryGetSolidBlockContainingPoint(footProbe, out Vector3 blockCenter))
                continue;

            float candidateGroundedY = blockCenter.Y + 0.5f + PlayerFootOffset + GroundSnapEpsilon;
            if (candidateGroundedY > groundedCameraY)
            {
                groundedCameraY = candidateGroundedY;
            }

            hitGround = true;
        }

        return hitGround;
    }

    private static bool CollidesWithCeiling(Vector3 position, out float ceilingCameraY)
    {
        ceilingCameraY = position.Y;

        foreach (var offset in FootProbeOffsets)
        {
            Vector3 headProbe = position + new Vector3(offset.X, PlayerHeadOffset, offset.Y);
            if (!TryGetSolidBlockContainingPoint(headProbe, out Vector3 blockCenter))
                continue;

            ceilingCameraY = blockCenter.Y - 0.5f - PlayerHeadOffset - GroundSnapEpsilon;
            return true;
        }

        return false;
    }

    private static bool TryGetSolidBlockContainingPoint(Vector3 point, out Vector3 blockCenter)
    {
        for (int index = 0; index < NearbyBlocks.Count; index++)
        {
            int chunkIndex = NearbyBlockChunks[index];
            if (chunkIndex == -1)
                continue;

            Vector3 nearbyBlock = NearbyBlocks[index];
            if (!chunks[chunkIndex].HasBlockCollision(nearbyBlock))
                continue;

            if (PointInsideBlock(point, nearbyBlock))
            {
                blockCenter = nearbyBlock;
                return true;
            }
        }

        blockCenter = Vector3.Zero;
        return false;
    }

    private static void RefreshNearbyBlocks(Vector3 position)
    {
        NearbyBlocks.Clear();
        NearbyBlockChunks.Clear();
        NearbyBlockSet.Clear();

        Vector3 playerPosition = new Vector3(
            (float)Math.Floor(position.X) + 0.5f,
            (float)Math.Floor(position.Y),
            (float)Math.Floor(position.Z) + 0.5f);

        AddNearbyBlock(playerPosition);
        AddNearbyBlock(playerPosition - Vector3.UnitY);

        for (int y = -3; y <= 2; y++)
        {
            GetNearNine(playerPosition + new Vector3(0, y, 0), NearbyBlockSearchRadius, position);
        }
    }

    private static void GetNearNine(Vector3 position, int radius, Vector3 referencePosition)
    {
        float radiusSquared = radius * radius;
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                var newPos = position + new Vector3(x, 0, z);
                if (Vector3.DistanceSquared(referencePosition, newPos) < radiusSquared)
                {
                    AddNearbyBlock(newPos);
                }
            }
        }
    }

    private static void AddNearbyBlock(Vector3 position)
    {
        if (!NearbyBlockSet.Add(position))
            return;

        NearbyBlocks.Add(position);
        NearbyBlockChunks.Add(GetChunkFromPosition(position));
    }

    private static int GetChunkFromPosition(Vector3 position)
    {
        for (var index = 0; index < chunkPositions.Count; index++)
        {
            var chunk = chunkPositions[index];
            if (position.X > chunk.X && position.X < chunk.X + 16 && position.Z > chunk.Y && position.Z < chunk.Y + 16)
                return index;
        }
        return -1;
    }

    private static void DrawPersistentUI(Matrix4x4 projectionUI)
    {
        if (!UIHandler.BlockCameraAndMovement)
        {
            UIHandler.Cursor.Render(projectionUI);
        }

        if (Vector3.DistanceSquared(CameraPosition, commandBlockLocation) < CommandBlockActiveRadiusSquared)
        {
            UIHandler.Key.Render(projectionUI);
        }
    }

    private static void DrawLoadingScreenText()
    {
        FontRenderer.SetProjection(FontProjection);
        FontStash.DrawText(Width / 3f, Height / 1.7f, Loader.Message);
        FontStash.DrawText(Width / 2.09f, Height / 1.97f, $"{Loader.PercentageCurrent}/{Loader.PercentageMaximum}");
    }

    private static void DrawDebug(Vector2 activeChunk, double deltaTime)
    {
        FontRenderer.SetProjection(FontProjection);
        DebugTextBuilder.Clear();
        DebugTextBuilder.Append("X: ");
        DebugTextBuilder.Append(CameraPosition.X.ToString("0.00"));
        DebugTextBuilder.Append(", Y: ");
        DebugTextBuilder.Append(CameraPosition.Y.ToString("0.00"));
        DebugTextBuilder.Append(", Z: ");
        DebugTextBuilder.Append(CameraPosition.Z.ToString("0.00"));
        FontStash.DrawText(20, 56, DebugTextBuilder.ToString());
        if (fpsResetCounter >= fpsResetLimit)
        {
            fpsLast = 1 / deltaTime;
            fpsResetCounter = 0;
        }
        else
        {
            fpsResetCounter++;
        }

        FontStash.DrawText(Width - 220, 56, fpsLast.ToString("0.00") + " fps");
        //FontStash.DrawText(20, 144, $"Active chunk - X: {activeChunk.X}; Y: {activeChunk.Y}");
        FontStash.DrawText(20, 100, $"Seed: {seed}");
        FontStash.DrawText(20, Height - 44, "letocma1 - pgrf2 - 4.5.2026");
        FontStash.DrawText(20, Height - 88, "WASD - Movement, Mouse - Look, Left click - Break block");
        string msg = "Press 'R' to respawn.";
        FontStash.DrawText(Width / 2f - msg.Length * 9, Height - 44, msg);
        
        //FontStash.DrawText(20, 256, $"Bigger ore chunks: {biggerOreGrowth}");
        //FontStash.DrawText(20, 300, $"Better ore chance: {oreChance}");
        //FontStash.DrawText(20, 344, $"Extra ore: {moreOreGrowth}");
        //FontStash.DrawText(20, 388, "Left to set bigger ore chunks, right to add extra ore");
        //FontStash.DrawText(20, 432, "Down to keep seed, Up to not");
    }

    private static Vector2 GetActiveChunkLocation(Vector3 position)
    {
        var chunkX = MathF.Floor((position.X + 8f) / 16f) * 16f - 8f;
        var chunkZ = MathF.Floor((position.Z + 8f) / 16f) * 16f - 8f;
        return new Vector2(chunkX, chunkZ);
    }

    private static void PickChunksForRendering(Vector2 activeChunkLocation)
    {
        Vector2 dividedCenter = (activeChunkLocation + new Vector2(0.5f, 0.5f)) / 16;
        foreach (var chunk in chunks)
        {
            chunk.Render = true;
            Vector2 leftBottom = chunk.LeftBottom;
            Vector2 divided = (leftBottom + new Vector2(0.5f, 0.5f)) / 16;
            Vector2 offsetCenter = divided - dividedCenter;
            if (Math.Abs(offsetCenter.X) + Math.Abs(offsetCenter.Y) > RenderDistance)
                chunk.Render = false;
        }
    }

    private static void OnResize(Vector2D<int> size)
    {
        Width = size.X;
        Height = size.Y;
        UpdateProjectionMatrices();
        Loader.Background.EditTransform(new Vector3(0,0,0), Width, Height);
        Loader.SliderBackground.EditTransform(new Vector3(0,0,0), Width / 2f, Height / 2f);
        Loader.Slider.EditTransform(new Vector3(0,0,0), Width / 5f, Height /6f); //TODO: change to real values
        UIHandler.Key.EditTransform(new Vector3(Width / 2.1f, 0,0), Height / 15f, Height / 15f);
        UIHandler.Background.EditTransform(Vector3.Zero, Width / 1.2f, Height / 1.2f);
        Gl.Viewport(size);
    }

    private static void KeyDown(IKeyboard keyboard, Key key, int keyCode)
    {
        switch (key)
        {
            case Key.F11 when _window.WindowState == WindowState.Maximized:
                _window.WindowState = WindowState.Normal;
                break;
            case Key.F11:
                _window.WindowState = WindowState.Maximized;
                break;
            case Key.R:
                CameraPosition = commandBlockLocation + new Vector3(0, 10, 0);
                VerticalVelocity = 0f;
                break;
            case Key.E:
                if (Vector3.DistanceSquared(CameraPosition, commandBlockLocation) < CommandBlockActiveRadiusSquared)
                {
                    LastMousePosition = UIHandler.HandleCommandBlockUI(primaryMouse, Width, Height, LastMousePosition);
                }
                break;
            case Key.Space:
                Jumping = true;
                break;
            case Key.Escape:
                _window.Close();
                break;
        }
    }

    private static unsafe void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (!Loader.FinishedLoading || UIHandler.BlockCameraAndMovement)
            return;
        var lookSensitivity = 0.08f;
        if (LastMousePosition == default)
        {
            LastMousePosition = position;
        }
        else
        {
            var xOffset = (position.X - LastMousePosition.X) * lookSensitivity;
            var yOffset = (position.Y - LastMousePosition.Y) * lookSensitivity;
            LastMousePosition = position;

            CameraYaw += xOffset;
            CameraPitch -= yOffset;

            //We don't want to be able to look behind us by going over our head or under our feet so make sure it stays within these bounds
            CameraPitch = Math.Clamp(CameraPitch, -89.0f, 89.0f);

            CameraDirection.X = MathF.Cos(MathHelper.DegreesToRadians(CameraYaw)) *
                                MathF.Cos(MathHelper.DegreesToRadians(CameraPitch));
            CameraDirection.Y = MathF.Sin(MathHelper.DegreesToRadians(CameraPitch));
            CameraDirection.Z = MathF.Sin(MathHelper.DegreesToRadians(CameraYaw)) *
                                MathF.Cos(MathHelper.DegreesToRadians(CameraPitch));
            CameraFront = Vector3.Normalize(CameraDirection);
        }
    }

    private static unsafe void OnMouseWheel(IMouse mouse, ScrollWheel scrollWheel)
    {
    }

    private static void UpdateProjectionMatrices()
    {
        UiProjection = Matrix4x4.CreateOrthographic(Width, Height, 0.1f, 100f);
        FontProjection = Matrix4x4.CreateOrthographicOffCenter(0, Width, Height, 0, -1f, 1f);
    }
}
