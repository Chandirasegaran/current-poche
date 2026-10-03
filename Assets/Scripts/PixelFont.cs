using System.Collections.Generic;
using UnityEngine;

// Draws text with the game's own pixel font. The font is a picture
// (Resources/UI/font.png) holding every character in a 16x6 grid; to write a
// sentence we copy the right squares into a new little texture.
public static class PixelFont
{
    const int Columns = 16, Rows = 6, FirstChar = 32;
    public const int LineHeight = 11;

    static Color32[] atlas;
    static int atlasWidth, atlasHeight, cellWidth, cellHeight;
    static int[] advance;

    static void Load()
    {
        if (atlas != null) return;
        var texture = Resources.Load<Texture2D>("UI/font");
        atlas = texture.GetPixels32();
        atlasWidth = texture.width;
        atlasHeight = texture.height;
        cellWidth = atlasWidth / Columns;
        cellHeight = atlasHeight / Rows;

        // Measure each character so narrow letters like "i" take less room.
        advance = new int[Columns * Rows];
        for (int i = 0; i < advance.Length; i++)
        {
            int widest = -1;
            for (int x = 0; x < cellWidth; x++)
            for (int y = 0; y < cellHeight; y++)
                if (Source(i, x, y).a > 0) widest = Mathf.Max(widest, x);
            advance[i] = widest < 0 ? 4 : widest + 1; // each cell already has a blank column on its left
        }
    }

    // Pixel of character cell "index" at (x, y), with y counted from the top.
    static Color32 Source(int index, int x, int y)
    {
        int column = index % Columns, row = index / Columns;
        return atlas[(atlasHeight - 1 - (row * cellHeight + y)) * atlasWidth + column * cellWidth + x];
    }

    static int Index(char c) => c >= FirstChar && c < FirstChar + Columns * Rows ? c - FirstChar : '?' - FirstChar;

    static int Width(string text)
    {
        int width = 0;
        foreach (char c in text) width += advance[Index(c)];
        return width;
    }

    // Splits text into lines no wider than wrapWidth pixels (0 = never wrap).
    static List<string> Wrap(string text, int wrapWidth)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (wrapWidth > 0 && line.Length > 0 && Width(candidate) > wrapWidth)
                {
                    lines.Add(line);
                    line = word;
                }
                else line = candidate;
            }
            lines.Add(line);
        }
        return lines;
    }

    public static Texture2D Render(string text, int wrapWidth = 0)
    {
        Load();
        var lines = Wrap(text ?? "", wrapWidth);

        int width = 1;
        foreach (string line in lines) width = Mathf.Max(width, Width(line));
        int height = lines.Count * LineHeight + (cellHeight - LineHeight);

        var pixels = new Color32[width * height];
        for (int l = 0; l < lines.Count; l++)
        {
            int pen = 0;
            foreach (char c in lines[l])
            {
                int index = Index(c);
                for (int y = 0; y < cellHeight; y++)
                for (int x = 0; x < advance[index] && pen + x < width; x++)
                {
                    var colour = Source(index, x, y);
                    if (colour.a > 0)
                        pixels[(height - 1 - (l * LineHeight + y)) * width + pen + x] = colour;
                }
                pen += advance[index];
            }
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
        };
        texture.SetPixels32(pixels);
        texture.Apply();
        return texture;
    }
}
