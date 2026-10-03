using UnityEngine;
using UnityEngine.UI;

// A piece of on-screen text in the pixel font.
[RequireComponent(typeof(RawImage))]
public class PixelLabel : MonoBehaviour
{
    public int wrapWidth;
    public int scale = 1;

    RawImage image;
    string shown;

    public string Text
    {
        get => shown;
        set
        {
            if (value == shown) return;
            shown = value;

            if (image == null) image = GetComponent<RawImage>();
            if (image.texture != null) Destroy(image.texture);
            var texture = PixelFont.Render(value, wrapWidth);
            image.texture = texture;
            image.rectTransform.sizeDelta = new Vector2(texture.width, texture.height) * scale;
        }
    }

    public Color Colour
    {
        set
        {
            if (image == null) image = GetComponent<RawImage>();
            image.color = value;
        }
    }
}
