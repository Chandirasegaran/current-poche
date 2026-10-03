using System.Collections.Generic;
using UnityEngine;

// Draws text with the game's own pixel fonts. Each font is a picture
// (Resources/UI/font.png and font_small.png) holding every character in a
// 16x6 grid; to write a sentence we copy the right squares into a new texture.
public static class PixelFont
{
    const int Columns = 16, Rows = 6, FirstChar = 32;

    class Atlas
    {
        public Color32[] pixels;
        public int width, height, cellWidth, cellHeight, lineHeight;
        public int[] advance;

        // Pixel of character cell "index" at (x, y), with y counted from the top.
        public Color32 At(int index, int x, int y)
        {
            int column = index % Columns, row = index / Columns;
            return pixels[(height - 1 - (row * cellHeight + y)) * width + column * cellWidth + x];
        }
    }

    static Atlas regular, small;

    static Atlas Load(string resource, int lineHeight)
    {
        var texture = Resources.Load<Texture2D>(resource);
        var atlas = new Atlas
        {
            pixels = texture.GetPixels32(), width = texture.width, height = texture.height,
            cellWidth = texture.width / Columns, cellHeight = texture.height / Rows, lineHeight = lineHeight,
            advance = new int[Columns * Rows],
        };

        // Measure each character so narrow letters like "i" take less room.
        for (int i = 0; i < atlas.advance.Length; i++)
        {
            int widest = -1;
            for (int x = 0; x < atlas.cellWidth; x++)
            for (int y = 0; y < atlas.cellHeight; y++)
                if (atlas.At(i, x, y).a > 0) widest = Mathf.Max(widest, x);
            atlas.advance[i] = widest < 0 ? 3 : widest + 1; // each cell already has a blank column on its left
        }
        return atlas;
    }

    static Atlas Font(bool smallText)
    {
        regular ??= Load("UI/font", 11);
        small ??= Load("UI/font_small", 9);
        return smallText ? small : regular;
    }

    static int Index(char c) => c >= FirstChar && c < FirstChar + Columns * Rows ? c - FirstChar : '?' - FirstChar;

    static int Width(Atlas font, string text)
    {
        int width = 0;
        foreach (char c in text) width += font.advance[Index(c)];
        return width;
    }

    // Splits text into lines no wider than wrapWidth pixels (0 = never wrap).
    static List<string> Wrap(Atlas font, string text, int wrapWidth)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.Split('\n'))
        {
            string line = "";
            foreach (string word in paragraph.Split(' '))
            {
                string candidate = line.Length == 0 ? word : line + " " + word;
                if (wrapWidth > 0 && line.Length > 0 && Width(font, candidate) > wrapWidth)
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

    public static Texture2D Render(string text, int wrapWidth = 0, bool smallText = false)
    {
        var font = Font(smallText);
        var lines = Wrap(font, text ?? "", wrapWidth);

        int width = 1;
        foreach (string line in lines) width = Mathf.Max(width, Width(font, line));
        int height = lines.Count * font.lineHeight + (font.cellHeight - font.lineHeight);

        var pixels = new Color32[width * height];
        for (int l = 0; l < lines.Count; l++)
        {
            int pen = 0;
            foreach (char c in lines[l])
            {
                int index = Index(c);
                for (int y = 0; y < font.cellHeight; y++)
                for (int x = 0; x < font.advance[index] && pen + x < width; x++)
                {
                    var colour = font.At(index, x, y);
                    if (colour.a > 0)
                        pixels[(height - 1 - (l * font.lineHeight + y)) * width + pen + x] = colour;
                }
                pen += font.advance[index];
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
