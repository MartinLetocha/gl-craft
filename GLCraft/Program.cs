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
using Shader = GLCraft.Fundamental.Shader;
using Texture = GLCraft.Fundamental.Texture;

namespace GLCraft;

class Program
{
    //Base
    private static IWindow _window;
    private static GL Gl;
    private static IKeyboard primaryKeyboard;
    private static IMouse primaryMouse;

    private static int Width = 800;
    private static int Height = 700;
    
    //Camera
    private static Vector3 CameraPosition = new Vector3(0.0f, 0.0f, 3.0f);
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
    private const string TEXTURE_PATH = @"C:\Users\marti\RiderProjects\GLCraft\GLCraft\Textures\";
    private const string SHADER_LOCATION = @"C:\Users\marti\RiderProjects\GLCraft\GLCraft\Shaders\";
    private const string SPECIAL_RESOURCE_PATH = @"C:\Users\marti\RiderProjects\GLCraft\GLCraft\App\";
    private const string APP_LOGO = @"GLCraftLogo.png";
    
    //actual game stuff
    private static int seed;
    
    private static float SprintSpeed = 2f;

    private static Vector3 commandBlockLocation = Vector3.Zero;
    private static float commandBlockActiveRadius = 5f;
    private static int chunkAmount = 1;
    private static bool biggerOreGrowth = false;
    private static int moreOreGrowth = 0;
    private static int oreChance = 0;
    private static int carbonizer = 1;

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
        
        ImageResult image = ImageResult.FromMemory(File.ReadAllBytes(Path.Combine(SPECIAL_RESOURCE_PATH, APP_LOGO)),
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
        FontNormal = FontStash.AddFont("testFont",
            "C:\\Users\\marti\\RiderProjects\\GLCraft\\GLCraft\\Fonts\\Verdana.ttf", 0);

        uint fontColourRed = GetColor(255, 0, 0, 255);

        FontStash.SetFont(FontNormal);
        FontStash.SetSize(72.0f);
        FontStash.SetColour(fontColourRed);

        Gl.ClearColor(Color.CornflowerBlue);
        Gl.Enable(EnableCap.Blend);
        Gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        var skyboxTexture = new CubeTexture(Gl, "C:\\Users\\marti\\RiderProjects\\GLCraft\\GLCraft\\Skybox\\");
        var skyboxShader = new Shader(Gl, GetShaderLocation("skybox", GLEnum.VertexShader), GetShaderLocation("skybox", GLEnum.FragmentShader));
        Skybox = new Skybox(Gl, skyboxShader, skyboxTexture);
        
        AddBlocks();

        FontStash.SetFont(FontNormal);
        FontStash.SetSize(36.0f);
        FontStash.SetColour(GLFons.Rgba(255, 255, 255, 255));

        Width = _window.Size.X;
        Height = _window.Size.Y;
        Gl.Viewport(0, 0, (uint)_window.Size.X, (uint)_window.Size.Y);
        
        var uishader = new Shader(Gl, GetShaderLocation("uishader", GLEnum.VertexShader),
            GetShaderLocation("shader", GLEnum.FragmentShader));

        var keyTexture = new Texture(Gl, Path.Combine(SPECIAL_RESOURCE_PATH, "EKey.png"));
        var commandBg = new Texture(Gl, Path.Combine(SPECIAL_RESOURCE_PATH, "commandbg.png"));
        UIHandler.Key = new Image(Gl, keyTexture, uishader, new Transform() {Position = new Vector3(Width / 2.1f, 0,0), ScaleX = Height / 15f, ScaleY = Height / 15f, Rotation = Quaternion.CreateFromAxisAngle(new Vector3(0,0,1), -90 * (float)Math.PI / 180)});
        UIHandler.Background = new Image(Gl, commandBg, uishader, new Transform(){Position = Vector3.Zero, ScaleX = Width / 1.2f, ScaleY = Height / 1.2f});
        
        var bgTexture = new Texture(Gl, Path.Combine(SPECIAL_RESOURCE_PATH, "bg.png"));
        var sliderbgTexture = new Texture(Gl, Path.Combine(SPECIAL_RESOURCE_PATH, "sliderbg.png"));
        var sliderTexture = new Texture(Gl, Path.Combine(SPECIAL_RESOURCE_PATH, "slider.png"));
        Loader.Background = new Image(Gl, bgTexture, uishader, new Transform() {Position = new Vector3(0,0,0), ScaleX = Width, ScaleY = Height});
        Loader.Slider = new Image(Gl, sliderTexture, uishader, new Transform() {Position = new Vector3(0,0,0), ScaleX = Width / 3f, ScaleY = Height / 10f});
        Loader.SliderBackground = new Image(Gl, sliderbgTexture, uishader, new Transform() {Position = new Vector3(0,0,0), ScaleX = Width / 3f, ScaleY = Height / 10f});
        Loader.CreateChunks(Gl, chunkAmount, ref chunks, ref commandBlockLocation);
    }

    private static void OnMouseClick(IMouse arg1, MouseButton arg2)
    {
        if (UIHandler.BlockCameraAndMovement)
        {
            if (arg2 == MouseButton.Left)
            {
                UIHandler.CheckClick(arg1.Position);
            }
            return;
        }
        //check what block the mouse pointed at and destroy it
    }

    private static void AddBlocks()
    {
        var solidShader = new Shader(Gl, GetShaderLocation("solid", GLEnum.VertexShader),
            GetShaderLocation("solid", GLEnum.FragmentShader));
        var texture = new Texture(Gl, Path.Combine(TEXTURE_PATH, "grassBlock.png"));
        var cobble = new Texture(Gl, Path.Combine(TEXTURE_PATH, "cobblestone.png"));
        var stone = new Texture(Gl, Path.Combine(TEXTURE_PATH, "stoneBlock.png"));
        var dirt = new Texture(Gl, Path.Combine(TEXTURE_PATH, "dirt.png"));
        var deepslate = new Texture(Gl, Path.Combine(TEXTURE_PATH, "deepslate.png"));
        var coal = new Texture(Gl, Path.Combine(TEXTURE_PATH, "coalOre.png"));
        var iron = new Texture(Gl, Path.Combine(TEXTURE_PATH, "ironOre.png"));
        var gold = new Texture(Gl, Path.Combine(TEXTURE_PATH, "goldOre.png"));
        var diamond = new Texture(Gl, Path.Combine(TEXTURE_PATH, "diamondOre.png"));
        var granite = new Texture(Gl, Path.Combine(TEXTURE_PATH, "granite.png"));
        var andesite = new Texture(Gl, Path.Combine(TEXTURE_PATH, "andesite.png"));
        var command = new Texture(Gl, Path.Combine(TEXTURE_PATH, "commandBlock.png"));
        
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
    }
    

    private static uint GetColor(byte r, byte g, byte b, byte a)
    {
        return (uint)((r) | (g << 8) | (b << 16) | (a << 24));
    }

    private static void OnUpdate(double deltaTime)
    {
        if (UIHandler.BlockCameraAndMovement)
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

        CameraZoom = primaryKeyboard.IsKeyPressed(Key.C) ? 1f : 45f;
        if (primaryKeyboard.IsKeyPressed(Key.W))
        {
            //Move forwards
            CameraPosition += moveSpeed * CameraFront;
        }

        if (primaryKeyboard.IsKeyPressed(Key.S))
        {
            //Move backwards
            CameraPosition -= moveSpeed * CameraFront;
        }

        if (primaryKeyboard.IsKeyPressed(Key.A))
        {
            //Move left
            CameraPosition -= Vector3.Normalize(Vector3.Cross(CameraFront, CameraUp)) * moveSpeed;
        }

        if (primaryKeyboard.IsKeyPressed(Key.D))
        {
            //Move right
            CameraPosition += Vector3.Normalize(Vector3.Cross(CameraFront, CameraUp)) * moveSpeed;
        }
    }

    private static string GetShaderLocation(string name, GLEnum type)
    {
        if (type == GLEnum.VertexShader)
        {
            return SHADER_LOCATION + name + ".vert";
        }

        if (type == GLEnum.FragmentShader)
        {
            return SHADER_LOCATION + name + ".frag";
        }

        return "";
    }

    private static unsafe void OnRender(double deltaTime)
    {
        Gl.Enable(EnableCap.DepthTest);
        Gl.Clear((uint)(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit));

        if (!Loader.FinishedLoading && Loader.StartedLoading)
        {
            var projectionUI = Matrix4x4.CreateOrthographic(Width, Height, 0.1f, 100f);
            float onePercent = Loader.PercentageMaximum / 100f;
            float currentPercent = (Loader.PercentageCurrent / onePercent + 1) / 100f;
            float larped = (1 - currentPercent) * Width / 300 + currentPercent * Width / 3;
            Loader.LoadChunk(ref chunks);
            Loader.Slider.Render(projectionUI);
            Loader.Slider.EditTransform(new Vector3((-larped / 2 + larped) - Width / 6f, 0,0), larped, Height / 10f);
            Loader.SliderBackground.Render(projectionUI);
            Loader.Background.Render(projectionUI);
            Gl.Disable(EnableCap.DepthTest);
            DrawLoadingScreenText();
            return;
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
        var projectionUILate = Matrix4x4.CreateOrthographic(Width, Height, 0.1f, 100f);
        if (UIHandler.BlockCameraAndMovement)
        {
            UIHandler.DrawCommandBlockUI(projectionUILate, ref FontRenderer, ref FontStash, Width, Height);
        }
        DrawPersistentUI(projectionUILate);
        //gameObjects[0].Render(view, projection);

        Gl.DepthFunc(DepthFunction.Lequal);
        Skybox.Render(view, projection);
        Gl.DepthFunc(DepthFunction.Less);

        Gl.Disable(EnableCap.DepthTest);
        DrawDebug(activeChunk, deltaTime);
    }

    private static void DrawPersistentUI(Matrix4x4 projectionUI)
    {
        double length = Math.Sqrt(Math.Pow(CameraPosition.X - commandBlockLocation.X, 2) + Math.Pow(CameraPosition.Y - commandBlockLocation.Y,2) + Math.Pow(CameraPosition.Z - commandBlockLocation.Z,2));
        if (length < commandBlockActiveRadius)
        {
            UIHandler.Key.Render(projectionUI);
        }
    }

    private static void DrawLoadingScreenText()
    {
        FontRenderer.SetProjection(Matrix4x4.CreateOrthographicOffCenter(0, Width, Height, 0, -1f, 1f));
        FontStash.DrawText(Width / 3f, Height / 1.7f, Loader.Message);
        FontStash.DrawText(Width / 2.09f, Height / 1.97f, $"{Loader.PercentageCurrent}/{Loader.PercentageMaximum}");
    }

    private static void DrawDebug(Vector2 activeChunk, double deltaTime)
    {
        FontRenderer.SetProjection(Matrix4x4.CreateOrthographicOffCenter(0, Width, Height, 0, -1f, 1f));
        StringBuilder sb = new();
        sb.Append("X: ");
        sb.Append(Math.Round(CameraPosition.X, 2));
        sb.Append(", Y: ");
        sb.Append(Math.Round(CameraPosition.Y, 2));
        sb.Append(", Z: ");
        sb.Append(Math.Round(CameraPosition.Z, 2));
        FontStash.DrawText(20, 56, sb.ToString());
        if (fpsResetCounter >= fpsResetLimit)
        {
            fpsLast = 1 / deltaTime;
            fpsResetCounter = 0;
        }
        else
        {
            fpsResetCounter++;
        }

        FontStash.DrawText(20, 100, fpsLast.ToString("0.00") + " fps");
        FontStash.DrawText(20, 144, $"Active chunk - X: {activeChunk.X}; Y: {activeChunk.Y}");
        FontStash.DrawText(20, 188, $"Seed: {seed}");
        
        FontStash.DrawText(20, 256, $"Bigger ore chunks: {biggerOreGrowth}");
        FontStash.DrawText(20, 300, $"Better ore chance: {oreChance}");
        FontStash.DrawText(20, 344, $"Extra ore: {moreOreGrowth}");
        FontStash.DrawText(20, 388, "Left to set bigger ore chunks, right to add extra ore");
        FontStash.DrawText(20, 432, "Down to keep seed, Up to not");
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
            case Key.Up:
                seed = new Random().Next();
                Loader.ChangeChunkSettings(seed, biggerOreGrowth, moreOreGrowth, oreChance, carbonizer);
                Loader.CreateChunks(Gl, chunkAmount, ref chunks, ref commandBlockLocation);
                break;
            case Key.Down:
                Loader.ChangeChunkSettings(seed, biggerOreGrowth, moreOreGrowth, oreChance, carbonizer);
                Loader.CreateChunks(Gl, chunkAmount, ref chunks, ref commandBlockLocation);
                break;
            case Key.Left:
                biggerOreGrowth = !biggerOreGrowth;
                break;
            case Key.Right:      //   orechance/moreore/biggerore/carbonizer
                moreOreGrowth++; //TODO: 10/10/True/4 should be cap
                break;
            case Key.E:
                double length = Math.Sqrt(Math.Pow(CameraPosition.X - commandBlockLocation.X, 2) + Math.Pow(CameraPosition.Y - commandBlockLocation.Y,2) + Math.Pow(CameraPosition.Z - commandBlockLocation.Z,2));
                if (length < commandBlockActiveRadius)
                {
                    LastMousePosition = UIHandler.HandleCommandBlockUI(primaryMouse, Width, Height, LastMousePosition);
                }
                break;
            case Key.Number0:
                oreChance++;
                break;
            case Key.Number1:
                carbonizer++;
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
}
