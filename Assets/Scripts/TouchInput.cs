using UnityEngine;

// What the on-screen controls are doing this frame, for phones and tablets.
// GameUI draws the stick and buttons and fills these in; the rest of the
// game reads them alongside the keyboard.
public static class TouchInput
{
    // True once the player has touched the screen (and until they use a keyboard).
    public static bool Active;

    public static Vector2 Move;           // the stick, each axis from -1 to 1
    public static bool Use, Whistle, Emote; // true for the one frame a button was tapped
}
