using UnityEngine;

/// <summary>
/// Pixel-art 9-slice frames for panels and buttons, built in code so the look stays in one place.
/// Each texture is 12x12 art pixels drawn at 2x: a dark outline, a lit top-left edge, a shaded bottom-right edge.
/// </summary>
public static class UiSkin
{
    public const int Border = 6; // screen pixels of each frame edge the 9-slice keeps unstretched

    static Color C(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;

    /// <summary>One frame texture. Texture y runs bottom-up, so the "top" edge is the high rows.</summary>
    public static Texture2D Frame(Color outline, Color fill, Color light, Color shade)
    {
        const int Art = 12, Scale = 2, Size = Art * Scale;
        var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int ax = x / Scale, ay = y / Scale;
                bool edgeX = ax == 0 || ax == Art - 1, edgeY = ay == 0 || ay == Art - 1;
                Color c;
                if (edgeX && edgeY) c = Color.clear;                // rounded corner
                else if (edgeX || edgeY) c = outline;
                else if (ax == 1 || ay == Art - 2) c = light;        // left and top inner edge
                else if (ax == Art - 2 || ay == 1) c = shade;        // right and bottom inner edge
                else c = fill;
                t.SetPixel(x, y, c);
            }
        t.Apply();
        return t;
    }

    public static GUIStyle Button(Font font, int size)
    {
        var s = new GUIStyle(GUI.skin.button)
        {
            font = font, fontSize = size, alignment = TextAnchor.MiddleCenter,
            border = new RectOffset(Border, Border, Border, Border),
            padding = new RectOffset(10, 10, 4, 6),
        };
        s.normal.background = Frame(C("#1c140e"), C("#5a4030"), C("#8a6a48"), C("#3a281c"));
        s.hover.background = Frame(C("#e8c060"), C("#6e5038"), C("#a8844e"), C("#46301f"));
        s.active.background = Frame(C("#e8c060"), C("#3e2c20"), C("#2e2016"), C("#5a4030"));
        s.normal.textColor = C("#f0e6d0");
        s.hover.textColor = C("#ffe9a0");
        s.active.textColor = C("#ffe9a0");
        // IMGUI draws disabled buttons from the normal state, dimmed by GUI.enabled
        s.onNormal = s.normal;
        s.onHover = s.hover;
        return s;
    }

    /// <summary>A dark parchment-edged panel for dialogs, shops and overlays.</summary>
    public static GUIStyle Panel()
    {
        var s = new GUIStyle { border = new RectOffset(Border, Border, Border, Border) };
        s.normal.background = Frame(C("#120e16"), new Color(0.07f, 0.06f, 0.09f, 0.95f), C("#8d6e48"), C("#4a3624"));
        return s;
    }
}
