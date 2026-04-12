using System.Numerics;
using FontStash.NET;
using GLCraft.GameObjects;
using Silk.NET.Input;
using GLCraft.Fonts;
using GLCraft.Fundamental;
using Silk.NET.OpenGL;

namespace GLCraft.UI;

public static class UIHandler
{
    public static Image Background;
    public static Image Key;
    public static Image Cursor;
    public static bool BlockCameraAndMovement = false;
    public static GL Gl;
    public static List<Button> Buttons = new List<Button>();
    public static Dictionary<BlockType, int> Resources = new Dictionary<BlockType, int>();

    private const int BOUGHT_AMOUNT_X_OFFSET = 600;
    private static int[] upgradeLimits = [99, 1, 10, 10, 3];
    private static int[] upgradeBasePrice = [100, 5000, 7500, 7500, 10000];
    private static int[] upgradeScaling = [1000, 0, 10000, 10000, 15000];
    private static int[] resourceValues = [2,1,1,1,3,4,8,16,25,2,2,3,3];
    private static string[] upgradeNames = ["Chunk amount", "Bigger ore growth", "More ore growth", "Ore chance", "Coal to Diamond"];
    private static int totalMoney = 0;
    private static bool freeReset = false;
    private static int resetCost = 10000;

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
        for (var index = 0; index < Buttons.Count; index++)
        {
            var button = Buttons[index];
            if (location.X > button.Position.X && location.X < button.Position.X + button.Dimension.X &&
                location.Y > button.Position.Y && location.Y < button.Position.Y + button.Dimension.Y)
            {
                if (button.Id >= 100)
                {
                    SpecialClick(button.Id);
                    continue;
                }
                if(upgradeLimits[index] > button.ClickedAmount)
                    CheckBuy(button, index);
            }
        }
    }

    private static void CheckBuy(Button button, int index)
    {
        if (totalMoney >= upgradeBasePrice[index] + upgradeScaling[index] * button.ClickedAmount)
        {
            freeReset = true;
            button.Click();
            switch (button.Id)
            {
                case 0:
                    Program.chunkAmount++;
                    break;
                case 1:
                    Program.biggerOreGrowth = true;
                    break;
                case 2:
                    Program.moreOreGrowth++;
                    break;
                case 3:
                    Program.oreChance++;
                    break;
                case 4:
                    Program.carbonizer++;
                    break;
            }
        }
    }

    private static void SpecialClick(int id)
    {
        if (id == 100)
        {
            if (freeReset || totalMoney >= resetCost)
            {
                if (freeReset)
                {
                    freeReset = false;
                }
                else
                {
                    resetCost += resetCost;
                }

                Program.ResetChunks();
            }
        }
    }

    public static void DrawCommandBlockUI(Matrix4x4 projectionMatrix, ref GLFons FontRenderer, ref Fontstash FontStash, int Width, int Height)
    {
        FontRenderer.SetProjection(Matrix4x4.CreateOrthographicOffCenter(0, Width, Height, 0, -1f, 1f));
        Background.Render(projectionMatrix);
        List<Button> renderLater = new List<Button>();
        for (var index = 0; index < Buttons.Count; index++)
        {
            var button = Buttons[index];
            if (button.Id >= 100)
            {
                renderLater.Add(button);
                continue;
            }
            button.BackgroundImage.Render(projectionMatrix);
            float y = button.Position.Y + button.Dimension.Y / 1.6f;
            FontStash.DrawText(button.Position.X + button.Dimension.X / 2f - button.Text.Length * 10, y, button.Text);
            FontStash.DrawText(button.Position.X - button.Dimension.X / 3f, y, button.ClickedAmount + "/" + upgradeLimits[index]);
            FontStash.DrawText(button.Position.X - button.Dimension.X * 1.6f, y, upgradeNames[index]);
            FontStash.DrawText(button.Position.X + button.Dimension.X * 1.3f, y, "Cost: " + (upgradeBasePrice[index] + upgradeScaling[index] * button.ClickedAmount));
        }

        foreach (var button in renderLater)
        {
            button.BackgroundImage.Render(projectionMatrix);
            float y = button.Position.Y + button.Dimension.Y / 1.6f;
            FontStash.DrawText(button.Position.X + button.Dimension.X / 2f - button.Text.Length * 10, y, button.Text);
            string msg = $"Free: {freeReset}, Cost: {resetCost}";
            FontStash.DrawText(button.Position.X + button.Dimension.X / 2f - msg.Length * 9, y - 80, msg);
        }

        int i = 0;
        int total = 0;
        int value = 0;
        foreach (var resource in Resources)
        {
            if(resource.Key == BlockType.Special || resource.Key == BlockType.CommandBlock)
                continue;
            FontStash.DrawText(Width / 1.4f, 250 + 44 * i, resource.Key + ": " + resource.Value + " * " + resourceValues[i]);
            total += resource.Value;
            value += resource.Value * resourceValues[i];
            i++;
        }
        totalMoney = value;
        FontStash.DrawText(Width / 1.4f, 250 + 45 * i, $"Total: {total}, Value: {value}");
    }
}
