using System.Numerics;
using GLCraft.GameObjects;

namespace GLCraft.UI;

public class Button(int id, Image bg, string text, Vector2 position, Vector2 dimension)
{
    public int Id = id;
    public int ClickedAmount;
    public Image BackgroundImage = bg;
    public string Text = text;
    public Vector2 Dimension = dimension;
    public Vector2 Position = position;

    public void Click()
    {
        ClickedAmount++;
    }
}