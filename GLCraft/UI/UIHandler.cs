using System.Numerics;
using FontStash.NET;
using GLCraft.GameObjects;
using Silk.NET.Input;
using GLCraft.Fonts;
using Silk.NET.OpenGL;

namespace GLCraft.UI;

public static class UIHandler
{
    public static Image Background;
    public static Image Key;
    public static bool BlockCameraAndMovement = false;
    public static GL Gl;
    public static List<Button> Buttons = new List<Button>();

    private static int click = 0;

    public static Vector2 HandleCommandBlockUI(IMouse mouse, int width, int height, Vector2 lastMousePosition)
    {
        var centeredPosition = new Vector2(width / 2f, height / 2f);

        if (!BlockCameraAndMovement)
        {
            BlockCameraAndMovement = true;
            mouse.Cursor.CursorMode = CursorMode.Normal;
            mouse.Position = centeredPosition;
            return centeredPosition;
        }

        mouse.Position = lastMousePosition;
        mouse.Cursor.CursorMode = CursorMode.Raw;
        BlockCameraAndMovement = false;
        return lastMousePosition;
    }

    public static void CheckClick(Vector2 location)
    {
        foreach (var button in Buttons)
        {
            if (location.X > button.Position.X && location.X < button.Position.X + button.Dimension.X &&
                location.Y > button.Position.Y && location.Y < button.Position.Y + button.Dimension.Y)
            {
                click++;
            }
        }
    }

    public static void DrawCommandBlockUI(Matrix4x4 projectionMatrix, ref GLFons FontRenderer, ref Fontstash FontStash, int Width, int Height)
    {
        Background.Render(projectionMatrix);
        foreach (var button in Buttons)
        {
            button.BackgroundImage.Render(projectionMatrix);
        }
        FontRenderer.SetProjection(Matrix4x4.CreateOrthographicOffCenter(0, Width, Height, 0, -1f, 1f));
        FontStash.DrawText(Width / 2f, Height / 2f, click.ToString());
    }
}
