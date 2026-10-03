using UnityEngine;
using UnityEngine.InputSystem;

// Everything the player can do with a key, and which key does it. The keys can
// be changed in the settings menu and are remembered between sessions.
public enum GameAction { Up, Down, Left, Right, Interact, Tasks, Whistle }

public static class Controls
{
    public static readonly string[] Names =
        { "Move up", "Move down", "Move left", "Move right", "Talk / use", "Task list", "Call Battery" };

    static readonly Key[] defaults = { Key.W, Key.S, Key.A, Key.D, Key.E, Key.Tab, Key.Q };
    static Key[] keys;

    static void Load()
    {
        if (keys != null) return;
        keys = new Key[defaults.Length];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = (Key)PlayerPrefs.GetInt("key" + i, (int)defaults[i]);
    }

    public static Key Get(GameAction action)
    {
        Load();
        return keys[(int)action];
    }

    public static void Set(GameAction action, Key key)
    {
        Load();
        keys[(int)action] = key;
        PlayerPrefs.SetInt("key" + (int)action, (int)key);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        for (int i = 0; i < defaults.Length; i++) Set((GameAction)i, defaults[i]);
    }

    public static string Label(GameAction action) => Get(action).ToString();

    // Whether a fixed key (one that can't be remapped) went down this frame.
    public static bool Tapped(Key key)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }

    static bool Down(Key key)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].isPressed;
    }

    // Movement as a direction, from the keys (remappable ones or arrows) or the touch stick.
    public static Vector2 Movement()
    {
        var move = TouchInput.Active ? TouchInput.Move : Vector2.zero;
        if (Held(GameAction.Up) || Down(Key.UpArrow)) move.y += 1;
        if (Held(GameAction.Down) || Down(Key.DownArrow)) move.y -= 1;
        if (Held(GameAction.Left) || Down(Key.LeftArrow)) move.x -= 1;
        if (Held(GameAction.Right) || Down(Key.RightArrow)) move.x += 1;
        return Vector2.ClampMagnitude(move, 1f);
    }

    public static bool Held(GameAction action)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[Get(action)].isPressed;
    }

    public static bool Pressed(GameAction action)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[Get(action)].wasPressedThisFrame;
    }
}
