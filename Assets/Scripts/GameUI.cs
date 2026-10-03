using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Everything drawn on top of the game: the title screen, the join code and
// streetlight counter, dialogue boxes, hints and the pause menu.
// The whole interface is built here in code and drawn on a tiny 216-pixel-tall
// "screen" that is scaled up by a whole number, so it stays crisp like the art.
public class GameUI : MonoBehaviour
{
    public static GameUI Instance { get; private set; }

    // True while the player should not be walking around.
    public static bool BlocksInput => Instance != null &&
        (Instance.dialogue.activeSelf || Instance.pause.activeSelf || Instance.settings.activeSelf);

    static readonly Color Yellow = new(1f, 0.86f, 0.42f);
    static readonly Color Pale = new(0.82f, 0.86f, 0.98f);
    static readonly Color Dim = new(0.5f, 0.55f, 0.72f);
    static readonly Color Ink = new(0.16f, 0.1f, 0.06f);

    static readonly string[] Intro =
    {
        "Radio|...and that is the end of the 46th over! Minnalpatti need 34 runs off 24 balls, and the whole district is watchi--",
        "Radio|*click*",
        "The whole street|CURRENT POCHU!!",
        "Paati|Aiyo. Right in the last overs. Kanna, take the torch and go see what that Murugesan has done now.",
    };

    CanvasScaler scaler;
    GameObject title, hud, dialogue, pause, toast;
    Button soloButton, hostButton, joinButton;
    GameObject eraseButton, settings, credits, touchControls, quitButton;
    RectTransform stickBase, stickKnob;
    TouchScreenKeyboard softKeyboard;
    PixelLabel musicValue, soundValue, fullscreenValue, difficultyValue;
    readonly PixelLabel[] keyLabels = new PixelLabel[Controls.Names.Length];
    int rebinding = -1; // which action is waiting for a key press, if any
    bool showTasks = true;
    float joinedAt;
    GameObject questPanel;
    PixelLabel questLabel, iceLabel;
    int blipped;
    PixelLabel statusLabel, codeEntryLabel, codeLabel, playersLabel, lightsLabel, hintLabel, toastLabel;
    PixelLabel speakerLabel, lineLabel, moreLabel;
    Image bulbIcon;
    Sprite bulbOn, bulbOff;

    string codeEntry = "";
    readonly Queue<string> lines = new();
    string currentLine = "";
    float revealed, toastTimer;
    string afterDialogueToast;
    bool introShown, celebrated;

    void Awake()
    {
        Instance = this;
        Build();
        Sfx.Begin();
        // Start in the window mode the player chose last time (fullscreen by default).
        bool desktop = !Application.isEditor && !Application.isMobilePlatform &&
                       Application.platform != RuntimePlatform.WebGLPlayer;
        if (desktop && Screen.fullScreen != GameSettings.Fullscreen)
            GameSettings.Fullscreen = GameSettings.Fullscreen;
        TouchInput.Active = Application.isMobilePlatform;
    }

    void OnEnable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput += OnTextInput;
    }

    void OnDisable()
    {
        if (Keyboard.current != null) Keyboard.current.onTextInput -= OnTextInput;
    }

    // ------------------------------------------------------------ every frame

    void Update()
    {
        scaler.scaleFactor = Mathf.Max(1, Mathf.FloorToInt(Screen.height / 216f));

        // Each part of the world has its own music.
        var here = PlayerController.Local != null ? (Vector2)PlayerController.Local.transform.position : Vector2.zero;
        Sfx.SetTrack(here.x >= 66f ? "music_fields"
            : here.x < -65f ? (here.y >= -5f ? "music_hills" : "music_yard")
            : here.y >= 40f ? "music_cinema" : "music_town");
        Sfx.Tick();

        var keys = Keyboard.current;
        UpdateTouch(keys);
        if (Controls.Tapped(Key.F11)) GameSettings.Fullscreen = !GameSettings.Fullscreen;
        if (settings.activeSelf) UpdateSettings(keys);
        if (Controls.Tapped(Key.Escape) && (settings.activeSelf || credits.activeSelf))
        {
            settings.SetActive(false);
            credits.SetActive(false);
            return;
        }

        var sessions = SessionManager.Instance;
        bool inGame = sessions != null && sessions.InGame;
        title.SetActive(!inGame);
        hud.SetActive(inGame);

        if (inGame) UpdateGame(sessions);
        else UpdateTitle(sessions);
    }

    void UpdateTitle(SessionManager sessions)
    {
        introShown = celebrated = false;
        dialogue.SetActive(false);
        pause.SetActive(false);
        lines.Clear();

        bool usable = sessions != null && sessions.Ready && !sessions.Busy;
        soloButton.interactable = sessions != null && !sessions.Busy; // solo works offline too
        eraseButton.SetActive(Quests.HasSave);
        joinedAt = Time.time;
        hostButton.interactable = usable;
        joinButton.interactable = usable && codeEntry.Length > 0;
        statusLabel.Text = sessions != null ? sessions.Status : "";

        bool blink = Time.unscaledTime % 1f < 0.5f;
        codeEntryLabel.Text = codeEntry.Length == 0 && !blink ? "CODE" : codeEntry + (blink ? "_" : " ");
        codeEntryLabel.Colour = codeEntry.Length == 0 ? Dim : Yellow;

        // On a phone the code is typed with the on-screen keyboard.
        if (softKeyboard != null)
        {
            string typed = "";
            foreach (char c in softKeyboard.text)
                if (char.IsLetterOrDigit(c) && typed.Length < 8) typed += char.ToUpperInvariant(c);
            codeEntry = typed;
            if (softKeyboard.status != TouchScreenKeyboard.Status.Visible) softKeyboard = null;
        }

        if (!usable) return;
        if (Controls.Tapped(Key.Backspace) && codeEntry.Length > 0)
            codeEntry = codeEntry[..^1];
        if (Controls.Tapped(Key.Enter) && codeEntry.Length > 0)
            _ = sessions.Join(codeEntry);
    }

    void OnTextInput(char typed)
    {
        if (!title.activeSelf || codeEntry.Length >= 8 || !char.IsLetterOrDigit(typed)) return;
        codeEntry += char.ToUpperInvariant(typed);
    }

    void UpdateGame(SessionManager sessions)
    {
        var session = sessions.Session;
        codeLabel.Text = session != null ? session.Code : "SOLO";
        playersLabel.Text = session != null ? $"{session.PlayerCount} of {session.MaxPlayers} players" : "Esc: menu";

        int lit = 0;
        foreach (var lamp in StreetLight.All)
            if (lamp.IsLit) lit++;
        int total = StreetLight.All.Count;
        lightsLabel.Text = $"{lit} / {total}";
        bulbIcon.sprite = lit > 0 ? bulbOn : bulbOff;

        var player = PlayerController.Local;
        if (player == null) return;

        if (!introShown)
        {
            // Wait a moment so a saved game has loaded before deciding what to show.
            if (Time.time - joinedAt < 0.8f) return;
            introShown = true;
            if (Quests.Instance != null && Quests.Instance.HasProgress)
            {
                celebrated = lit == total;
                Toast("Welcome back. Your progress was loaded.\nCheck the task list for what is left.");
                return;
            }
            Say(Intro);
            afterDialogueToast = "Shine your torch on the glowing minminis.\nLead 3 of them to a dead streetlight.\nEsc opens the menu and settings.";
        }
        else if (!celebrated && total > 0 && lit == total)
        {
            celebrated = true;
            Sfx.Play("quest");
            Toast("Every streetlight is lit!\nNow the transformer needs four fuses.");
        }

        var quests = Quests.Instance;
        if (Controls.Pressed(GameAction.Tasks)) showTasks = !showTasks;
        questPanel.SetActive(showTasks && !dialogue.activeSelf); // never cover the story
        if (quests != null)
        {
            questLabel.Text = quests.LogText();
            string banner = quests.Banner;
            iceLabel.gameObject.SetActive(banner.Length > 0);
            if (banner.Length > 0) iceLabel.Text = banner;
        }

        if (Controls.Tapped(Key.Escape) && !dialogue.activeSelf && !settings.activeSelf)
            pause.SetActive(!pause.activeSelf);

        if (settings.activeSelf) return;
        bool confirm = Controls.Pressed(GameAction.Interact) || Controls.Tapped(Key.Space) ||
                       Controls.Tapped(Key.Enter) || TouchInput.Use;
        // A click or a tap anywhere also moves dialogue along.
        bool click = Pointer.current != null && Pointer.current.press.wasPressedThisFrame;

        if (dialogue.activeSelf) UpdateDialogue(confirm || click);
        else if (confirm && !pause.activeSelf && player.Nearby != null) Interact(player.Nearby, player);

        var nearby = dialogue.activeSelf || pause.activeSelf ? null : player.Nearby;
        hintLabel.gameObject.SetActive(nearby != null);
        if (nearby != null) hintLabel.Text = TouchInput.Active ? nearby.verb : $"[{Controls.Label(GameAction.Interact)}] {nearby.verb}";

        // Messages sit at the bottom of the screen, or just above the dialogue box.
        ((RectTransform)toast.transform).anchoredPosition = new Vector2(0f, dialogue.activeSelf ? 62f : 8f);
        if (toast.activeSelf && (toastTimer -= Time.deltaTime) <= 0f) toast.SetActive(false);
    }

    // ------------------------------------------------------------ touch controls

    // Phones and tablets get a stick on the left and buttons on the right.
    void UpdateTouch(Keyboard keyboard)
    {
        var screen = Touchscreen.current;
        if (screen != null && screen.primaryTouch.press.isPressed) TouchInput.Active = true;
        if (!Application.isMobilePlatform && keyboard != null && keyboard.anyKey.wasPressedThisFrame) TouchInput.Active = false;

        bool playing = hud.activeSelf && !dialogue.activeSelf && !pause.activeSelf && !settings.activeSelf;
        touchControls.SetActive(TouchInput.Active && playing);

        // The stick appears wherever a finger lands on the left side of the screen.
        TouchInput.Move = Vector2.zero;
        var home = new Vector2(52f, 52f);
        stickBase.anchoredPosition = home;
        stickKnob.anchoredPosition = home;
        if (screen == null || !touchControls.activeSelf) return;

        foreach (var touch in screen.touches)
        {
            if (!touch.press.isPressed) continue;
            Vector2 start = touch.startPosition.ReadValue();
            if (start.x > Screen.width * 0.45f || start.y > Screen.height * 0.75f) continue;

            float reach = Screen.height * 0.13f;
            Vector2 pull = Vector2.ClampMagnitude(touch.position.ReadValue() - start, reach);
            if (pull.magnitude > reach * 0.2f) TouchInput.Move = pull / reach;
            stickBase.anchoredPosition = start / scaler.scaleFactor;
            stickKnob.anchoredPosition = (start + pull) / scaler.scaleFactor;
            break;
        }
    }

    void LateUpdate()
    {
        // Button taps last exactly one frame.
        TouchInput.Use = TouchInput.Whistle = false;
    }

    void BuildTouchControls(Transform canvas)
    {
        touchControls = Group(canvas, "Touch Controls");
        var corner = Vector2.zero;
        stickBase = Round(touchControls.transform, "stick_base", corner, new Vector2(52, 52), 0.55f).rectTransform;
        stickKnob = Round(touchControls.transform, "stick_knob", corner, new Vector2(52, 52), 0.8f).rectTransform;

        var right = Vector2.right;
        TouchButton("USE", right, new Vector2(-40, 62), new Vector2(56, 34), () => TouchInput.Use = true);
        TouchButton("DOG", right, new Vector2(-96, 40), new Vector2(46, 24), () => TouchInput.Whistle = true);
        var edge = new Vector2(1f, 0.5f);
        TouchButton("MENU", edge, new Vector2(-28, 16), new Vector2(46, 20), () => pause.SetActive(true));
        TouchButton("TASKS", edge, new Vector2(-28, -10), new Vector2(46, 20), () => showTasks = !showTasks);
        touchControls.SetActive(false);
    }

    void TouchButton(string text, Vector2 anchor, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction tap)
    {
        var button = MakeButton(touchControls.transform, text, anchor, position, size, tap);
        var colours = button.colors;
        colours.normalColor = new Color(1f, 1f, 1f, 0.8f);
        button.colors = colours;
    }

    static Image Round(Transform parent, string sprite, Vector2 anchor, Vector2 position, float alpha)
    {
        var image = Box(parent, sprite, anchor, new Vector2(0.5f, 0.5f), position, Vector2.zero);
        ActualSize(image);
        image.color = new Color(1f, 1f, 1f, alpha);
        return image;
    }

    // ------------------------------------------------------------ settings

    void UpdateSettings(Keyboard keyboard)
    {
        musicValue.Text = Mathf.RoundToInt(GameSettings.Music * 10) + " / 10";
        soundValue.Text = Mathf.RoundToInt(GameSettings.Sound * 10) + " / 10";
        fullscreenValue.Text = GameSettings.Fullscreen ? "ON" : "OFF";
        difficultyValue.Text = GameSettings.Relaxed ? "RELAXED" : "NORMAL";
        for (int i = 0; i < keyLabels.Length; i++)
            keyLabels[i].Text = rebinding == i ? "press a key" : Controls.Label((GameAction)i);

        // Waiting for the player to press the new key for an action.
        if (rebinding < 0 || keyboard == null) return;
        foreach (var key in keyboard.allKeys)
        {
            if (!key.wasPressedThisFrame) continue;
            if (key.keyCode != Key.Escape) Controls.Set((GameAction)rebinding, key.keyCode);
            rebinding = -1;
            break;
        }
    }

    void BuildSettings(Transform canvas)
    {
        var top = new Vector2(0.5f, 1f);
        var centre = new Vector2(0.5f, 0.5f);
        var topLeft = new Vector2(0f, 1f);

        settings = Backdrop(canvas, "Settings");
        var panel = Box(settings.transform, "panel_9s", centre, centre, Vector2.zero, new Vector2(250, 212)).transform;
        Label(panel, "SETTINGS", top, top, new Vector2(0, -6), Yellow);

        PixelLabel Row(string name, float y, System.Action<int> change)
        {
            Label(panel, name, topLeft, topLeft, new Vector2(14, y), Pale);
            MakeButton(panel, "-", topLeft, new Vector2(118, y + 2), new Vector2(18, 15), () => change(-1));
            MakeButton(panel, "+", topLeft, new Vector2(196, y + 2), new Vector2(18, 15), () => change(1));
            return Label(panel, "", topLeft, top, new Vector2(157, y), Yellow);
        }
        musicValue = Row("Music", -22, step => GameSettings.Music += step * 0.1f);
        soundValue = Row("Sound", -38, step => { GameSettings.Sound += step * 0.1f; Sfx.Play("pickup"); });

        Label(panel, "Fullscreen (F11)", topLeft, topLeft, new Vector2(14, -54), Pale);
        var toggle = MakeButton(panel, "", topLeft, new Vector2(157, -52), new Vector2(96, 15),
            () => GameSettings.Fullscreen = !GameSettings.Fullscreen);
        fullscreenValue = toggle.GetComponentInChildren<PixelLabel>();

        Label(panel, "Difficulty", topLeft, topLeft, new Vector2(14, -70), Pale);
        var ease = MakeButton(panel, "", topLeft, new Vector2(157, -68), new Vector2(96, 15),
            () => GameSettings.Relaxed = !GameSettings.Relaxed);
        difficultyValue = ease.GetComponentInChildren<PixelLabel>();

        for (int i = 0; i < Controls.Names.Length; i++)
        {
            int action = i;
            float y = -88 - i * 14;
            Label(panel, Controls.Names[i], topLeft, topLeft, new Vector2(14, y), Pale);
            var button = MakeButton(panel, "", topLeft, new Vector2(170, y + 2), new Vector2(86, 13), () => rebinding = action);
            keyLabels[i] = button.GetComponentInChildren<PixelLabel>();
        }

        MakeButton(panel, "RESET KEYS", top, new Vector2(-58, -189), new Vector2(104, 17), Controls.ResetAll);
        MakeButton(panel, "BACK", top, new Vector2(58, -189), new Vector2(104, 17), () => settings.SetActive(false));
        settings.SetActive(false);

        credits = Backdrop(canvas, "Credits");
        var page = Box(credits.transform, "panel_9s", centre, centre, Vector2.zero, new Vector2(260, 168)).transform;
        Label(page, "CURRENT POCHU!", top, top, new Vector2(0, -8), Yellow);
        Label(page,
            "A Segar Games story\n\n" +
            "Story, design, code, pixel art and music\n" +
            "made by Chandirasegaran, with Claude\n\n" +
            "Built with Unity and Netcode for GameObjects\n\n" +
            "Thank you for playing. Nandri!",
            top, top, new Vector2(0, -26), Pale);
        MakeButton(page, "BACK", top, new Vector2(0, -142), new Vector2(104, 17), () => credits.SetActive(false));
        credits.SetActive(false);
    }

    void Interact(Interactable target, PlayerController player)
    {
        if (target.TryGetComponent<Crate>(out var crate))
        {
            crate.UseRpc();
            return;
        }
        if (target.GetComponent<Dog>() != null) Sfx.Play("bark", 0.8f);
        bool scripted = !string.IsNullOrEmpty(target.action) && Quests.Instance != null;
        var script = scripted ? Quests.Instance.Talk(target.action, player) : target.lines;
        if (script.Length > 0) Say(script);
    }

    // ------------------------------------------------------------ dialogue

    // Each line is "Speaker|What they say".
    public void Say(IEnumerable<string> script)
    {
        foreach (string line in script) lines.Enqueue(line);
        if (!dialogue.activeSelf) NextLine();
    }

    public void Toast(string message, float seconds = 7f)
    {
        toastLabel.Text = message;
        toastTimer = seconds;
        toast.SetActive(true);
    }

    void NextLine()
    {
        if (lines.Count == 0)
        {
            dialogue.SetActive(false);
            if (afterDialogueToast != null) Toast(afterDialogueToast);
            afterDialogueToast = null;
            return;
        }

        string[] parts = lines.Dequeue().Split('|');
        speakerLabel.Text = parts.Length > 1 ? parts[0] : "";
        currentLine = parts[^1];
        revealed = 0f;
        blipped = 0;
        lineLabel.Text = "";
        dialogue.SetActive(true);
    }

    void UpdateDialogue(bool confirm)
    {
        // Letters appear one by one; pressing E finishes the line, then moves on.
        bool finished = revealed >= currentLine.Length;
        if (confirm && finished) { NextLine(); return; }
        if (confirm) revealed = currentLine.Length;

        revealed = Mathf.Min(currentLine.Length, revealed + Time.deltaTime * 55f);
        lineLabel.Text = currentLine[..(int)revealed];
        if ((int)revealed >= blipped + 3 && revealed < currentLine.Length)
        {
            blipped = (int)revealed;
            Sfx.Play("blip", 0.5f);
        }
        moreLabel.gameObject.SetActive(revealed >= currentLine.Length && Time.unscaledTime % 0.8f < 0.5f);
    }

    // ------------------------------------------------------------ building the interface

    void Build()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem));
            events.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.referencePixelsPerUnit = 16;
        var canvas = canvasObject.transform;

        bulbOn = Resources.Load<Sprite>("UI/bulb_on");
        bulbOff = Resources.Load<Sprite>("UI/bulb_off");
        var top = new Vector2(0.5f, 1f);
        var topLeft = new Vector2(0f, 1f);
        var topRight = new Vector2(1f, 1f);
        var bottom = new Vector2(0.5f, 0f);
        var centre = new Vector2(0.5f, 0.5f);

        // ---- title screen
        title = Group(canvas, "Title");
        var shade = Box(title.transform, null, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
        shade.rectTransform.anchorMax = Vector2.one;
        shade.color = new Color(0.02f, 0.03f, 0.09f, 0.5f);

        var logo = Box(title.transform, "logo", top, top, new Vector2(0, -8), Vector2.zero);
        ActualSize(logo);
        Label(title.transform, "a power-cut adventure for 1 to 4 friends", top, top, new Vector2(0, -90), Pale);

        soloButton = MakeButton(title.transform, "PLAY SOLO", top, new Vector2(0, -107), new Vector2(136, 22),
            () => SessionManager.Instance.PlaySolo());
        hostButton = MakeButton(title.transform, "HOST ONLINE", top, new Vector2(0, -132), new Vector2(136, 22),
            () => _ = SessionManager.Instance.Host());
        var field = Box(title.transform, "field_9s", top, top, new Vector2(-29, -157), new Vector2(78, 24));
        codeEntryLabel = Label(field.transform, "CODE", centre, centre, new Vector2(0, -1), Dim);
        joinButton = MakeButton(title.transform, "JOIN", top, new Vector2(41, -157), new Vector2(54, 24),
            () => _ = SessionManager.Instance.Join(codeEntry));
        statusLabel = Label(title.transform, "", top, top, new Vector2(0, -186), Yellow);
        Label(title.transform, "Segar Games", bottom, bottom, new Vector2(0, 3), Dim);
        eraseButton = MakeButton(title.transform, "ERASE SAVE", Vector2.right, new Vector2(-58, 26), new Vector2(104, 20),
            Quests.EraseSave).gameObject;
        quitButton = MakeButton(title.transform, "QUIT", Vector2.right, new Vector2(-58, 50), new Vector2(104, 20), Application.Quit).gameObject;
        quitButton.SetActive(Application.platform != RuntimePlatform.WebGLPlayer); // a web page can't quit

        // Tapping the code box on a phone brings up its keyboard.
        field.raycastTarget = true;
        field.gameObject.AddComponent<Button>().onClick.AddListener(() =>
        {
            if (TouchScreenKeyboard.isSupported) softKeyboard = TouchScreenKeyboard.Open(codeEntry, TouchScreenKeyboardType.Default, false);
        });
        MakeButton(title.transform, "SETTINGS", Vector2.zero, new Vector2(58, 50), new Vector2(104, 20), () => settings.SetActive(true));
        MakeButton(title.transform, "CREDITS", Vector2.zero, new Vector2(58, 26), new Vector2(104, 20), () => credits.SetActive(true));

        // ---- in-game heads-up display
        hud = Group(canvas, "HUD");
        var codePanel = Box(hud.transform, "panel_9s", topLeft, topLeft, new Vector2(6, -6), new Vector2(98, 31));
        Label(codePanel.transform, "CODE", topLeft, topLeft, new Vector2(8, -4), Dim);
        codeLabel = Label(codePanel.transform, "", topLeft, topLeft, new Vector2(38, -4), Yellow);
        playersLabel = Label(codePanel.transform, "", topLeft, topLeft, new Vector2(8, -15), Pale);

        var lightsPanel = Box(hud.transform, "panel_9s", topRight, topRight, new Vector2(-6, -6), new Vector2(66, 20));
        bulbIcon = Box(lightsPanel.transform, "bulb_off", topLeft, topLeft, new Vector2(7, -4), Vector2.zero);
        ActualSize(bulbIcon);
        lightsLabel = Label(lightsPanel.transform, "", topLeft, topLeft, new Vector2(22, -4), Pale);

        questPanel = Box(hud.transform, "panel_9s", topRight, topRight, new Vector2(-6, -29), new Vector2(96, 72)).gameObject;
        questLabel = Label(questPanel.transform, "", topLeft, topLeft, new Vector2(6, -4), Pale, 0, true);
        iceLabel = Label(hud.transform, "", top, top, new Vector2(0, -8), new Color(0.6f, 0.9f, 1f));

        hintLabel = Label(hud.transform, "", top, top, new Vector2(0, -24), Yellow); // above the player, clear of the action

        toast = Box(hud.transform, "panel_9s", bottom, bottom, new Vector2(0, 80), new Vector2(236, 36)).gameObject;
        toastLabel = Label(toast.transform, "", centre, centre, new Vector2(0, -1), Pale, 222, true);
        toast.SetActive(false);

        dialogue = Box(hud.transform, "panel_9s", bottom, bottom, new Vector2(0, 6), new Vector2(300, 52)).gameObject;
        speakerLabel = Label(dialogue.transform, "", topLeft, topLeft, new Vector2(10, -5), Yellow);
        lineLabel = Label(dialogue.transform, "", topLeft, topLeft, new Vector2(10, -17), Pale, 280, true);
        moreLabel = Label(dialogue.transform, ">", Vector2.right, Vector2.right, new Vector2(-8, 4), Yellow);
        dialogue.SetActive(false);

        pause = Box(hud.transform, "panel_9s", centre, centre, Vector2.zero, new Vector2(150, 138)).gameObject;
        MakeButton(pause.transform, "QUIT GAME", top, new Vector2(0, -104), new Vector2(120, 22), Application.Quit);
        Label(pause.transform, "PAUSED", top, top, new Vector2(0, -8), Yellow);
        MakeButton(pause.transform, "RESUME", top, new Vector2(0, -26), new Vector2(120, 22), () => pause.SetActive(false));
        MakeButton(pause.transform, "SETTINGS", top, new Vector2(0, -52), new Vector2(120, 22), () =>
        {
            pause.SetActive(false);
            settings.SetActive(true);
        });
        MakeButton(pause.transform, "LEAVE GAME", top, new Vector2(0, -78), new Vector2(120, 22),
            () => _ = SessionManager.Instance.Leave());
        pause.SetActive(false);

        BuildTouchControls(canvas);
        BuildSettings(canvas);
    }

    // A dark sheet over the whole screen, to put a menu on. It also swallows
    // clicks, so buttons underneath can't be pressed by accident.
    static GameObject Backdrop(Transform parent, string name)
    {
        var group = Group(parent, name);
        var sheet = group.AddComponent<Image>();
        sheet.color = new Color(0.02f, 0.03f, 0.09f, 0.93f);
        return group;
    }

    static GameObject Group(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect.gameObject;
    }

    // Shows a picture at one screen pixel per art pixel, unstretched.
    static void ActualSize(Image image)
    {
        image.type = Image.Type.Simple;
        image.rectTransform.sizeDelta = image.sprite.rect.size;
    }

    static void Place(RectTransform rect, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 position)
    {
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
    }

    // A picture or stretchy panel. A null sprite gives a plain coloured rectangle.
    static Image Box(Transform parent, string sprite, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var image = new GameObject(sprite ?? "Shade", typeof(Image)).GetComponent<Image>();
        Place(image.rectTransform, parent, anchor, pivot, position);
        image.rectTransform.sizeDelta = size;
        if (sprite != null)
        {
            image.sprite = Resources.Load<Sprite>("UI/" + sprite);
            image.type = Image.Type.Sliced;
        }
        image.raycastTarget = false;
        return image;
    }

    static PixelLabel Label(Transform parent, string text, Vector2 anchor, Vector2 pivot, Vector2 position,
        Color colour, int wrapWidth = 0, bool small = false)
    {
        var label = new GameObject("Label", typeof(RawImage)).AddComponent<PixelLabel>();
        label.GetComponent<RawImage>().raycastTarget = false;
        Place((RectTransform)label.transform, parent, anchor, pivot, position);
        label.wrapWidth = wrapWidth;
        label.small = small;
        label.Colour = colour;
        label.Text = text;
        return label;
    }

    static Button MakeButton(Transform parent, string text, Vector2 anchor, Vector2 position, Vector2 size,
        UnityEngine.Events.UnityAction onClick)
    {
        var image = Box(parent, "button_9s", anchor, new Vector2(0.5f, 1f), position, size);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colours = button.colors;
        colours.highlightedColor = new Color(1f, 0.95f, 0.8f);
        colours.pressedColor = new Color(0.8f, 0.7f, 0.5f);
        colours.selectedColor = Color.white;
        colours.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.7f);
        colours.fadeDuration = 0.05f;
        button.colors = colours;
        button.onClick.AddListener(() => Sfx.Play("click"));
        button.onClick.AddListener(onClick);

        var half = new Vector2(0.5f, 0.5f);
        Label(image.transform, text, half, half, new Vector2(0, 0), Ink);
        return button;
    }
}
