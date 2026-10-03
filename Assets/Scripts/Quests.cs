using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEngine;

// The story state of Chapter 1, shared by everyone in the game.
//
// To turn the power back on, the players must light every streetlight and
// collect four fuses, one from each person they help:
//   Paati        - find her glasses by the well
//   Tea Master   - lead his goat home with a banana leaf
//   The Chairman - run a block of ice across town before it melts
//   Umpire Ravi  - find six lost cricket balls
// Then the fuses go into the transformer and the lights come back.
//
// The server owns these values. When a player presses E on something, their
// machine works out what to say from the current state (Talk), and asks the
// server to make the change (ActRpc).
public class Quests : NetworkBehaviour
{
    public const int FusesNeeded = 4, BallCount = 6;

    // Difficulty. "Relaxed" (the host's setting) gives half as much time again
    // on timers and slows every hazard down.
    readonly NetworkVariable<bool> relaxed = new(true);
    static bool Easy => Instance == null || !Instance.IsSpawned || Instance.relaxed.Value;
    public static float Hazard => Easy ? 0.72f : 1f;   // multiplies hazard speeds
    static float Patience => Easy ? 1.5f : 1f;         // multiplies timers
    static float IceSeconds => 40f * Patience;

    [Flags]
    enum Flag
    {
        GlassesFound = 1, GlassesReturned = 2, GoatPenned = 4, GoatRewarded = 8,
        IceDelivered = 16, BallsReturned = 32, PowerRestored = 64,
        // Chapter 2: the fields
        BridgeDown = 128, BeltTaken = 256, BeltFitted = 512, PumpStarted = 1024,
        // Chapter 3: Raja Talkies
        FilmPlayed = 2048,
        // Chapter 4: the goods yard
        YardDone = 4096,
        // Chapters 5 and 6: the hills and the powerhouse
        Finished = 8192,
        // Side-jobs: optional, any time
        RadioFound = 1 << 14, RadioReturned = 1 << 15, CratesDelivered = 1 << 16, TempleLit = 1 << 17,
    }

    public const int SideJobCount = 3;

    public const int WindmillCount = 3;

    static readonly string[] TheEnd =
    {
        "|The generator turns. Down in the valley, every light in Minnalpatti dims at once, as the whole town lends its current.",
        "Lineman Murugesan|NOW, thambi! Throw the switch!",
        "|One enormous spark climbs the wire. The little lightning stands up, shakes itself, and for the first time tonight it is not afraid.",
        "|It looks back at you. Then it jumps.",
        "|For one second the whole sky is daylight.",
        "The whole town|CURRENT VANDHUDUCHU!!",
        "Radio|--and he has hit it for SIX! Minnalpatti win the district final off the very last ball!",
        "Paati|...I missed the whole match. Next time, kanna, fix the current BEFORE the last over.",
        "|From that night on, the lightning over Minnalpatti never struck a single thing. It only ever lit the way home.",
        "|THE END.   Thank you for playing CURRENT POCHU!   A Segar Games story.",
    };

    public const int LanternCount = 4;
    const int LeverPattern = 0b101; // levers 1 and 3 up, lever 2 down, as on the notice board

    static readonly string[] YardEnding =
    {
        "|The signal blinks from red to green. Out in the dark, a long goods train that has waited all night gives one tired whistle and begins to roll.",
        "|Riding on its headlamp, legs dangling, is the little lightning. It waves. You think it waves.",
        "Signal Rani|Thirty years on the railways, and that is the first passenger I have seen travel on the OUTSIDE of the lamp.",
        "Signal Rani|That train climbs to the windmill ridge, kanna, and then down to the old dam. If your bright friend is going home, it is going that way.",
        "|CHAPTER 4 COMPLETE.   The path up to the windmill ridge is open, on the north side of the yard.",
    };

    public const int ReelCount = 3;

    static readonly string[] FilmEnding =
    {
        "|The beam finds the screen. The old film crackles to life: a hero, a villain, a song in the rain.",
        "|And in the middle of the picture sits a tiny shape made of light, watching the film with its mouth open.",
        "Watchman Kannan|Aiyo. THAT is no ghost. The ghost was only my bedsheets on the line. That is something else.",
        "|The little lightning notices you. It squeaks, pulls the glow off the screen like a blanket, and shoots away west, along the railway line.",
        "Watchman Kannan|Poor thing. I think it only wanted a night-light. ...West is the goods yard, kanna. Mind the trains.",
        "|CHAPTER 3 COMPLETE.   The level crossing on Tank Road is open. The goods yard is west of town.",
    };

    static float ValveSeconds => 30f * Patience;

    static readonly string[] PumpEnding =
    {
        "|The pump coughs, catches, and roars. Water gushes into the channels.",
        "Farmer Periyasamy|Ahh! Listen to her sing! Thambi, you have saved the whole season's--",
        "|Something on the roof of the pump-house is glowing. It is the size of a kitten, and shaped like a lightning bolt.",
        "|It drinks the spark straight out of the motor, looks at you with two round, frightened eyes, and is gone. North. Towards the old cinema.",
        "Farmer Periyasamy|...That was not a minmini.",
        "|CHAPTER 2 COMPLETE.   The road north to Raja Talkies is open.",
    };

    public static Quests Instance { get; private set; }

    [SerializeField] GameObject[] powerOn;  // shown once the power is back (lit windows)
    [SerializeField] GameObject[] powerOff; // removed once the power is back (the east barricade)
    [SerializeField] GameObject bridgeBlocker, bridgeRaised, bridgeLowered;
    [SerializeField] SpriteRenderer[] valves;
    [SerializeField] Sprite valveShut, valveOpen;
    [SerializeField] StreetLight pump;
    [SerializeField] GameObject minnal;
    [SerializeField] GameObject[] pumpOff; // removed once the pump runs (the north barricade)
    [SerializeField] StreetLight projector;
    [SerializeField] Transform beamOrigin, screenTarget, spookPoint;
    [SerializeField] SpriteRenderer[] mirrors;
    [SerializeField] Sprite mirrorSlash, mirrorBackslash;
    [SerializeField] LineRenderer beam;
    [SerializeField] GameObject screenGlow, minnalOnScreen;
    [SerializeField] GameObject[] filmOff; // removed once the film has played (the west barricade)
    [SerializeField] StreetLight cabin;
    [SerializeField] SpriteRenderer[] levers;
    [SerializeField] Sprite leverUp, leverDown;
    [SerializeField] Transform yardEntrance;
    [SerializeField] GameObject minnalOnSignal;
    [SerializeField] GameObject[] yardOff; // removed once the signal is green (the gate to the hills)
    [SerializeField] Spinner[] blades;
    [SerializeField] GameObject[] windmillLights;
    [SerializeField] StreetLight generator;
    [SerializeField] GameObject minnalWaiting, minnalLeaving, skyFlash;
    [SerializeField] Transform crateSpot;       // where Selvam wants his crates
    [SerializeField] StreetLight[] templeLamps; // the oil lamps around the tank

    readonly NetworkVariable<int> flags = new();
    readonly NetworkVariable<int> ballMask = new();
    readonly NetworkVariable<int> fuses = new();
    readonly NetworkVariable<double> iceMeltsAt = new();
    readonly NetworkVariable<Vector3> valveShutsAt = new(); // one time per valve (x, y, z)
    readonly NetworkVariable<int> reelMask = new();   // which film reels have been found
    readonly NetworkVariable<int> mirrorMask = new(); // which mirrors lean like a forward slash

    readonly NetworkVariable<int> lanternMask = new(); // which signal lanterns have been found
    readonly NetworkVariable<int> leverMask = new();   // which point levers are up

    readonly NetworkVariable<int> brakeMask = new();   // which windmills have been released

    readonly List<Vector3> beamPoints = new();

    public bool Finished => Has(Flag.Finished);

    // How many side-jobs are done. Each one makes every torch reach further.
    public int SideJobs => (Has(Flag.RadioReturned) ? 1 : 0) + (Has(Flag.CratesDelivered) ? 1 : 0) + (Has(Flag.TempleLit) ? 1 : 0);
    int WindmillsTurning => Bits(brakeMask.Value, WindmillCount);

    public bool YardDone => Has(Flag.YardDone);
    public Vector3 YardEntrance => yardEntrance.position;
    int LanternsFound => Bits(lanternMask.Value, LanternCount);
    bool PointsSet => leverMask.Value == LeverPattern;

    public bool FilmPlayed => Has(Flag.FilmPlayed);
    public Vector3 SpookPoint => spookPoint.position;

    static int Bits(int mask, int count)
    {
        int set = 0;
        for (int i = 0; i < count; i++)
            if ((mask & (1 << i)) != 0) set++;
        return set;
    }

    int ReelsFound => Bits(reelMask.Value, ReelCount);

    // Follows the projector beam as it bounces off the mirrors. Returns true if
    // it ends on the screen. A "/" mirror turns east into north; a "\" mirror
    // turns east into south.
    bool TraceBeam()
    {
        beamPoints.Clear();
        Vector2 position = beamOrigin.position;
        Vector2 direction = Vector2.right;
        beamPoints.Add(position);

        for (int bounce = 0; bounce < 8; bounce++)
        {
            int hit = -1;
            float nearest = 60f;
            for (int i = 0; i < mirrors.Length; i++)
            {
                Vector2 to = (Vector2)mirrors[i].transform.position + Vector2.up * 0.7f - position;
                float along = Vector2.Dot(to, direction);
                float aside = Mathf.Abs(to.x * direction.y - to.y * direction.x);
                if (along > 0.1f && aside < 0.3f && along < nearest) { hit = i; nearest = along; }
            }

            Vector2 screen = screenTarget.position;
            if (direction == Vector2.up && Mathf.Abs(position.x - screen.x) < 4.8f &&
                screen.y > position.y && screen.y - position.y < nearest)
            {
                beamPoints.Add(new Vector2(position.x, screen.y));
                return true;
            }

            if (hit < 0)
            {
                beamPoints.Add(position + direction * 30f);
                return false;
            }

            position = (Vector2)mirrors[hit].transform.position + Vector2.up * 0.7f;
            beamPoints.Add(position);
            bool slash = (mirrorMask.Value & (1 << hit)) != 0;
            direction = slash ? new Vector2(direction.y, direction.x) : new Vector2(-direction.y, -direction.x);
        }
        return false;
    }

    Interactable[] actors;
    bool loading;

    // ------------------------------------------------------------ saving
    // The host's machine keeps the story in a small file, written whenever
    // something is achieved and read back when a game starts.

    [Serializable]
    class SaveData
    {
        public int flags, ballMask, fuses, reelMask, mirrorMask, lanternMask, leverMask, brakeMask;
        public List<string> lit = new(); // which lamps (and the pump) are powered
    }

    static string SavePath => Path.Combine(Application.persistentDataPath, "story.json");
    public static bool HasSave => File.Exists(SavePath);

    public static void EraseSave()
    {
        if (HasSave) File.Delete(SavePath);
    }

    // True once the players have got anywhere, so the opening scene can be skipped.
    public bool HasProgress => IsSpawned && (flags.Value != 0 || LampsLit > 1);

    static string Key(StreetLight lamp) =>
        $"{Mathf.RoundToInt(lamp.transform.position.x * 10)},{Mathf.RoundToInt(lamp.transform.position.y * 10)}";

    // How far along a saved story is, so two saves can be compared.
    static int Progress(SaveData data) =>
        Bits(data.flags, 31) + Bits(data.ballMask, 31) + Bits(data.reelMask, 31) + Bits(data.lanternMask, 31)
        + Bits(data.brakeMask, 31) + data.lit.Count;

    // Everyone in the game keeps a copy of the story, not only the host, so
    // whoever hosts next time can carry on from where the group left off.
    // A guest's copy is only replaced if the game they are in is at least as
    // far along as what they already had.
    public void Save()
    {
        if (!IsSpawned || loading) return;
        var data = new SaveData
        {
            flags = flags.Value, ballMask = ballMask.Value, fuses = fuses.Value,
            reelMask = reelMask.Value, mirrorMask = mirrorMask.Value,
            lanternMask = lanternMask.Value, leverMask = leverMask.Value, brakeMask = brakeMask.Value,
        };
        foreach (var lamp in StreetLight.Feedable)
            if (lamp.IsLit) data.lit.Add(Key(lamp));
        if (!IsServer && HasSave)
        {
            try
            {
                var mine = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (mine != null && Progress(mine) > Progress(data)) return;
            }
            catch (Exception e) { Debug.LogException(e); }
        }
        File.WriteAllText(SavePath, JsonUtility.ToJson(data));
    }

    IEnumerator Load()
    {
        yield return null; // give the streetlights a frame to appear on the network

        SaveData data = null;
        try { if (HasSave) data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath)); }
        catch (Exception e) { Debug.LogException(e); }
        if (data == null)
        {
            loading = false;
            yield break;
        }

        // Anything that was being carried when the game stopped goes back where it was.
        int saved = data.flags;
        if ((saved & (int)Flag.GlassesReturned) == 0) saved &= ~(int)Flag.GlassesFound;
        if ((saved & (int)Flag.BeltFitted) == 0) saved &= ~(int)Flag.BeltTaken;
        if ((saved & (int)Flag.RadioReturned) == 0) saved &= ~(int)Flag.RadioFound;

        flags.Value = saved;
        ballMask.Value = data.ballMask;
        fuses.Value = data.fuses;
        reelMask.Value = data.reelMask;
        mirrorMask.Value = data.mirrorMask;
        lanternMask.Value = data.lanternMask;
        leverMask.Value = data.leverMask;
        brakeMask.Value = data.brakeMask;
        foreach (var lamp in StreetLight.Feedable)
            if (data.lit.Contains(Key(lamp)) && !lamp.IsLit) lamp.ForceLit();
        yield return null;
        loading = false;
    }

    bool Has(Flag flag) => (flags.Value & (int)flag) != 0;
    void Raise(Flag flag) => flags.Value |= (int)flag;

    public bool PowerRestored => Has(Flag.PowerRestored);
    public bool GoatPenned => Has(Flag.GoatPenned);
    public int Fuses => fuses.Value;

    int BallsFound
    {
        get
        {
            int count = 0;
            for (int i = 0; i < BallCount; i++)
                if ((ballMask.Value & (1 << i)) != 0) count++;
            return count;
        }
    }

    static int LampsLit
    {
        get
        {
            int lit = 0;
            foreach (var lamp in StreetLight.All)
                if (lamp.IsLit) lit++;
            return lit;
        }
    }

    // Seconds until the carried ice melts, or -1 if nobody is carrying any.
    public float IceSecondsLeft =>
        IsSpawned && iceMeltsAt.Value > 0 ? (float)(iceMeltsAt.Value - NetworkManager.ServerTime.Time) : -1f;

    void Awake()
    {
        Instance = this;
        actors = FindObjectsByType<Interactable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            // Nothing is written to the save file until the old one has been read.
            loading = true;
            flags.Value = ballMask.Value = fuses.Value = 0;
            iceMeltsAt.Value = 0;
            valveShutsAt.Value = Vector3.zero;
            reelMask.Value = mirrorMask.Value = lanternMask.Value = leverMask.Value = brakeMask.Value = 0;
            StartCoroutine(Load());
        }
        foreach (var counter in new[] { ballMask, fuses, reelMask, mirrorMask, lanternMask, leverMask, brakeMask })
            counter.OnValueChanged += OnCounterChanged;
        flags.OnValueChanged += OnFlagsChanged;
        if (!IsServer) StartCoroutine(SaveWhenJoined());
    }

    public override void OnNetworkDespawn()
    {
        flags.OnValueChanged -= OnFlagsChanged;
        foreach (var counter in new[] { ballMask, fuses, reelMask, mirrorMask, lanternMask, leverMask, brakeMask })
            counter.OnValueChanged -= OnCounterChanged;
    }

    void OnCounterChanged(int before, int now) => Save();

    // A guest copies the host's story a moment after joining, once the lamps have arrived.
    IEnumerator SaveWhenJoined()
    {
        yield return new WaitForSeconds(2f);
        Save();
    }

    void OnFlagsChanged(int before, int now)
    {
        bool Became(Flag flag) => (before & (int)flag) == 0 && (now & (int)flag) != 0;

        if (loading) return; // restoring a save: no fanfare for old news
        Save();

        if (Became(Flag.PowerRestored)) Sfx.Play("power");
        if (Became(Flag.BridgeDown)) Sfx.Play("lamp");
        if (Became(Flag.Finished))
        {
            Sfx.Play("power");
            Sfx.Play("quest");
            minnalLeaving.SetActive(true);
            skyFlash.SetActive(true);
            if (GameUI.Instance != null) GameUI.Instance.Say(TheEnd);
        }
        if (Became(Flag.YardDone))
        {
            Sfx.Play("power");
            minnalOnSignal.SetActive(true);
            if (GameUI.Instance != null) GameUI.Instance.Say(YardEnding);
        }
        if (Became(Flag.FilmPlayed))
        {
            Sfx.Play("power");
            minnalOnScreen.SetActive(true);
            if (GameUI.Instance != null) GameUI.Instance.Say(FilmEnding);
        }
        if (Became(Flag.PumpStarted))
        {
            Sfx.Play("power");
            minnal.SetActive(true);
            if (GameUI.Instance != null) GameUI.Instance.Say(PumpEnding);
        }
    }

    float Now => (float)NetworkManager.ServerTime.Time;

    bool ValveOpen(int index) => IsSpawned && valveShutsAt.Value[index] > Now;

    int ValvesOpen => (ValveOpen(0) ? 1 : 0) + (ValveOpen(1) ? 1 : 0) + (ValveOpen(2) ? 1 : 0);

    // A line for the top of the screen while something is on a timer.
    public string Banner
    {
        get
        {
            if (!IsSpawned) return "";
            if (IceSecondsLeft > 0f) return $"ICE MELTS IN {Mathf.CeilToInt(IceSecondsLeft)}";
            if (!Has(Flag.BridgeDown) && ValvesOpen > 0)
            {
                float soonest = float.MaxValue;
                for (int i = 0; i < 3; i++)
                    if (ValveOpen(i)) soonest = Mathf.Min(soonest, valveShutsAt.Value[i] - Now);
                return $"VALVES {ValvesOpen}/3   {Mathf.CeilToInt(soonest)}";
            }
            return "";
        }
    }

    void Update()
    {
        // Things that have been picked up disappear for everyone.
        foreach (var actor in actors)
            if (actor != null && !string.IsNullOrEmpty(actor.action))
                actor.gameObject.SetActive(Visible(actor.action));

        bool power = IsSpawned && PowerRestored;
        foreach (var go in powerOn) go.SetActive(power);
        foreach (var go in powerOff) go.SetActive(!power);

        bool bridge = IsSpawned && Has(Flag.BridgeDown);
        bridgeBlocker.SetActive(!bridge);
        bridgeRaised.SetActive(!bridge);
        bridgeLowered.SetActive(bridge);
        for (int i = 0; i < valves.Length; i++)
            valves[i].sprite = bridge || ValveOpen(i) ? valveOpen : valveShut;

        // Raja Talkies: the mirrors, the beam, and the glowing screen.
        bool pumped = IsSpawned && Has(Flag.PumpStarted);
        foreach (var go in pumpOff) go.SetActive(!pumped);
        for (int i = 0; i < mirrors.Length; i++)
            mirrors[i].sprite = (mirrorMask.Value & (1 << i)) != 0 ? mirrorSlash : mirrorBackslash;

        bool projecting = IsSpawned && projector.IsLit;
        bool onScreen = projecting && TraceBeam();
        beam.enabled = projecting;
        if (projecting)
        {
            beam.positionCount = beamPoints.Count;
            beam.SetPositions(beamPoints.ToArray());
        }
        screenGlow.SetActive(IsSpawned && FilmPlayed);

        // The hills and the dam.
        foreach (var go in yardOff) go.SetActive(!(IsSpawned && YardDone));
        for (int i = 0; i < blades.Length; i++)
        {
            bool turning = (brakeMask.Value & (1 << i)) != 0;
            blades[i].enabled = turning;
            windmillLights[i].SetActive(turning);
        }
        minnalWaiting.SetActive(!(IsSpawned && Finished));
        if (IsSpawned && IsServer)
        {
            if (relaxed.Value != GameSettings.Relaxed) relaxed.Value = GameSettings.Relaxed;

            // Side-jobs that finish by themselves.
            if (!Has(Flag.CratesDelivered))
            {
                int delivered = 0;
                foreach (var crate in Crate.All)
                    if (!crate.Held && Vector2.Distance(crate.transform.position, crateSpot.position) < 2.6f) delivered++;
                if (Crate.All.Count > 0 && delivered == Crate.All.Count) SideJobDone(Flag.CratesDelivered, "All the crates are at the tea stall!");
            }
            if (!Has(Flag.TempleLit))
            {
                bool all = templeLamps.Length > 0;
                foreach (var lamp in templeLamps) all &= lamp.IsLit;
                if (all)
                {
                    SideJobDone(Flag.TempleLit, "Every lamp at the temple tank is lit.");
                    AnnounceRpc("", "bell");
                }
            }

            generator.Locked = WindmillsTurning < WindmillCount;
            if (generator.IsLit && !Finished) Raise(Flag.Finished);
        }

        // The goods yard.
        foreach (var go in filmOff) go.SetActive(!(IsSpawned && FilmPlayed));
        for (int i = 0; i < levers.Length; i++)
            levers[i].sprite = (leverMask.Value & (1 << i)) != 0 ? leverUp : leverDown;

        if (IsSpawned && IsServer)
        {
            cabin.Locked = LanternsFound < LanternCount || !PointsSet;
            if (cabin.IsLit && !YardDone) Raise(Flag.YardDone);
            projector.Locked = ReelsFound < ReelCount;
            if (onScreen && !FilmPlayed) Raise(Flag.FilmPlayed);
            pump.Locked = !Has(Flag.BeltFitted);
            if (!Has(Flag.BridgeDown) && ValvesOpen == 3)
            {
                Raise(Flag.BridgeDown);
                AnnounceRpc("All three valves open! The sluice bridge is down.", "quest");
            }
            if (!Has(Flag.PumpStarted) && pump.IsLit) Raise(Flag.PumpStarted);
        }

        if (IsSpawned && IsServer && iceMeltsAt.Value > 0 && NetworkManager.ServerTime.Time > iceMeltsAt.Value)
        {
            iceMeltsAt.Value = 0;
            foreach (var player in PlayerController.All)
                if (player.Carrying == PlayerController.Item.Ice) player.Carrying = PlayerController.Item.None;
            AnnounceRpc("The ice melted! Fetch another block from the cart.", "fail");
        }
    }

    bool Visible(string action)
    {
        if (!IsSpawned) return true;
        if (action == "glasses") return !Has(Flag.GlassesFound);
        if (action == "radio") return !Has(Flag.RadioFound);
        if (action.StartsWith("ball")) return (ballMask.Value & (1 << (action[4] - '0'))) == 0;
        if (action.StartsWith("reel")) return (reelMask.Value & (1 << (action[4] - '0'))) == 0;
        if (action.StartsWith("lantern")) return (lanternMask.Value & (1 << (action[7] - '0'))) == 0;
        return true;
    }

    // The checklist shown on screen.
    public string LogText()
    {
        if (!IsSpawned) return "";
        string chapter = ChapterText();
        return Finished && SideJobs == SideJobCount ? chapter : chapter + $"\nSide-jobs {SideJobs}/{SideJobCount}";
    }

    string ChapterText()
    {
        if (Finished) return "THE END\nThank you for playing!";
        if (YardDone)
            return "Kaatthaadi Hills\n"
                   + (WindmillsTurning == WindmillCount ? "+ " : "- ") + $"Windmills {WindmillsTurning}/{WindmillCount}\n"
                   + $"- Generator {generator.Charge}/{generator.Needed}";
        if (FilmPlayed)
            return "Goods Yard\n"
                   + (LanternsFound == LanternCount ? "+ " : "- ") + $"Lanterns {LanternsFound}/{LanternCount}\n"
                   + (PointsSet ? "+ " : "- ") + "Set the points\n"
                   + $"- Power the cabin {cabin.Charge}/{cabin.Needed}";
        if (Has(Flag.PumpStarted))
            return "Raja Talkies\n"
                   + (ReelsFound == ReelCount ? "+ " : "- ") + $"Film reels {ReelsFound}/{ReelCount}\n"
                   + (projector.IsLit ? "+ " : "- ") + $"Projector {projector.Charge}/{projector.Needed}\n"
                   + "- Beam to the screen";
        if (PowerRestored)
            return "The Pump-set\n"
                   + (Has(Flag.BridgeDown) ? "+ " : "- ") + "Open the bridge\n"
                   + (Has(Flag.BeltFitted) ? "+ " : "- ") + "Find the fan belt\n"
                   + $"- Start the pump {pump.Charge}/{pump.Needed}";
        string Line(bool done, string text) => (done ? "+ " : "- ") + text + "\n";
        return Line(LampsLit == StreetLight.All.Count, $"Streetlights {LampsLit}/{StreetLight.All.Count}")
               + Line(Has(Flag.GlassesReturned), "Paati's glasses")
               + Line(Has(Flag.GoatRewarded), "Selvam's goat")
               + Line(Has(Flag.IceDelivered), "Ice for the hall")
               + Line(Has(Flag.BallsReturned), $"Cricket balls {BallsFound}/{BallCount}")
               + $"Fuses {Fuses}/{FusesNeeded}";
    }

    // ------------------------------------------------------------ what people say

    // Runs on the machine of the player who pressed E. Returns the lines to show
    // and tells the server what was done.
    public string[] Talk(string action, PlayerController player)
    {
        string[] lines = Lines(action, player);
        ActRpc(action);
        return lines;
    }

    string[] Lines(string action, PlayerController player)
    {
        var item = player.Carrying;
        int lamps = LampsLit, total = StreetLight.All.Count;

        switch (action)
        {
            case "lineman":
                if (Finished) return new[] { "Lineman Murugesan|Nineteen years with the Electricity Board, and tonight I helped throw a lightning bolt back into the sky. I am putting in for overtime." };
                if (YardDone) return new[] { "Lineman Murugesan|It took the TRAIN? Up to the windmills? Of course it did. Everybody leaves this town by the night goods." };
                if (FilmPlayed) return new[] { "Lineman Murugesan|West, along the railway? Then it is heading for the goods yard. I will oil my cycle. You get some sleep." };
                if (Has(Flag.PumpStarted)) return new[] { "Lineman Murugesan|A lightning bolt. With EYES. Thambi, I have worked for the Electricity Board for nineteen years and nobody told me about this." };
                if (PowerRestored) return new[] { "Lineman Murugesan|Did you see where those minminis went? East, over the fields. The road is open now. Go and see old Periyasamy at the pump-set, and mind the bunds: one wrong step and you are in the paddy." };
                if (lamps == total && Fuses >= FusesNeeded) return new[] { "Lineman Murugesan|Every lamp lit AND four fuses? Go, go! Push them into the transformer by the EB office, at the east end of the bazaar." };
                if (lamps == total) return new[] { $"Lineman Murugesan|The ward is glowing! Now the transformer. It needs four fuses and I have... zero. You have {Fuses}. People here hoard fuses like gold. Help them and they will cough one up." };
                return new[]
                {
                    "Lineman Murugesan|It is NOT the fuse. Well. It is ALSO the fuse. But mainly the lines are empty, like somebody drank the current with a straw.",
                    "Lineman Murugesan|See those glowing bugs? Minminis. Each one carries a sip of it. Shine your torch at them and they follow you.",
                    "Lineman Murugesan|Walk three of them to a dead streetlight and it wakes up. Mind the bandicoots: they love to scatter minminis. They hate torchlight, so point it at them.",
                    $"Lineman Murugesan|Light all {total} lamps and find me four fuses, and I can restart the transformer. Paati, Selvam, the Chairman and that umpire boy each have one, I am sure of it.",
                };

            case "paati":
                if (Has(Flag.RadioFound) && !Has(Flag.RadioReturned)) return new[]
                {
                    "Paati|Your thatha's radio! It still has his thumbprint worn into the dial.",
                    "Paati|He always said a good torch is half a brave heart. Here, let me fix yours the way he fixed his.",
                };
                if (Has(Flag.RadioReturned)) return new[] { "Paati|The radio is on the shelf. When the current comes back, we will hear how the match ended." };
                if (Has(Flag.GlassesReturned)) return new[]
                {
                    "Paati|Now I can see how dark it is. Wonderful. Go on, kanna, the town is waiting.",
                    "Paati|One more thing. Your thatha's old radio is locked in the godown behind the west houses. The gate only opens while something heavy sits on the stone slab outside. Battery is heavy. Whistle, and he stays.",
                };
                if (item == PlayerController.Item.Glasses) return new[]
                {
                    "Paati|My glasses! Where were th-- by the WELL? I was only there to check the rope...",
                    "Paati|Here. Your thatha kept a spare fuse in his toolbox for forty years. Tonight it finally has a job.",
                };
                return new[]
                {
                    "Paati|No current, no serial, no cricket. And now I cannot find my glasses either.",
                    "Paati|I had them when I went to the well behind the west houses. Look there for me, kanna. Take Battery, that dog eats more than you do.",
                };

            case "glasses":
                return new[] { "|Paati's glasses. One arm is held on with thread." };

            case "radio":
                return new[] { "|An old valve radio, wrapped in a towel. THATHA is scratched on the back. Paati will want this." };

            case "shrine":
                return Has(Flag.TempleLit)
                    ? new[] { "|All five lamps burn around the tank. The water is full of little flames." }
                    : new[] { "|Five brass oil lamps stand around the temple tank, unlit. One minmini each would be enough to light them." };

            case "teamaster":
                if (Has(Flag.CratesDelivered)) return new[] { "Tea Master Selvam|Milk, goat, stove. Now THIS is a tea stall. Your torch looks brighter, or is it my mood?" };
                if (Has(Flag.GoatRewarded)) return new[]
                {
                    "Tea Master Selvam|Lakshmi is sulking, the stove is hot, and you are my favourite customer. Second tea is still full price.",
                    "Tea Master Selvam|If you want to stay my favourite: three crates of milk bottles are sitting at the bus stop on Kamarajar Street. Carry them here, by the stall. You can throw them too, they are good bottles.",
                };
                if (GoatPenned) return new[]
                {
                    "Tea Master Selvam|LAKSHMI! You found her! She gives the milk, thambi. No Lakshmi, no tea, no Minnalpatti.",
                    "Tea Master Selvam|Take this. It is the fuse from my mixie. I was saving it, but you have earned it.",
                };
                return new[]
                {
                    "Tea Master Selvam|No current, no mixie, no fridge. And when the lights died my goat Lakshmi bolted, down towards the temple tank.",
                    "Tea Master Selvam|She will follow anyone holding a banana leaf. The wedding hall has plenty by the door. Bring her to the pen across the road and I will make it worth your while.",
                };

            case "banana":
                if (GoatPenned) return new[] { "|Banana trees, tied up for the wedding." };
                if (item != PlayerController.Item.None) return new[] { "|Your hands are full." };
                return new[] { "|You tear off a banana leaf. The Chairman pretends not to notice." };

            case "chairman":
                if (Has(Flag.IceDelivered)) return new[] { "The Chairman|Cold rose milk for five hundred guests. I will mention you in my speech. Briefly." };
                if (item == PlayerController.Item.Ice) return new[]
                {
                    "The Chairman|ICE! Actual ice! Quickly, into the drum!",
                    "The Chairman|You have saved the wedding. Here, the generator room has a spare fuse. Do not tell the generator.",
                };
                return new[]
                {
                    "The Chairman|I have a speech at nine. NINE! And the rose milk for five hundred guests is getting WARM.",
                    $"The Chairman|Kumar's ice cart is parked at the far east end of Kamarajar Street. Bring me a block. It melts in {IceSeconds:0} seconds, so RUN.",
                };

            case "icecart":
                if (Has(Flag.IceDelivered)) return new[] { "|The ice cart is empty now. A puddle is spreading underneath." };
                if (item != PlayerController.Item.None) return new[] { "|Your hands are full." };
                if (IceSecondsLeft > 0) return new[] { "|Somebody is already running with a block. One at a time!" };
                return new[] { "|You heave out a block of ice. It is already dripping. RUN to the wedding hall!" };

            case "ravi":
                if (Has(Flag.BallsReturned)) return new[] { "Umpire Ravi|Six balls, all present. If the current comes back we can even finish OUR final." };
                if (BallsFound == BallCount) return new[]
                {
                    "Umpire Ravi|All six! Even the one Dinesh hit over the temple tank!",
                    "Umpire Ravi|A deal is a deal. This fuse is from the school bell. Nobody will miss the bell.",
                };
                return new[]
                {
                    "Umpire Ravi|We were playing our own final when the real one went dark. Everybody ran home and left the balls where they landed.",
                    $"Umpire Ravi|Six balls, all over town. You have found {BallsFound}. Bring me all of them and you can have the fuse from the school bell.",
                };

            case "transformer":
                if (PowerRestored) return new[] { "|The transformer hums like a happy fridge." };
                if (lamps < total || Fuses < FusesNeeded) return new[]
                {
                    "|The transformer is stone cold. Four empty fuse slots stare back at you.",
                    $"|Fuses: {Fuses} of {FusesNeeded}.   Streetlights: {lamps} of {total}.   It needs all of both.",
                };
                return new[]
                {
                    "|You push the last fuse home.",
                    "|The transformer shudders. Hums. And then--",
                    "The whole street|CURRENT VANDHUDUCHU!!",
                    "Radio|--needs six off the last ball! He swings, and it is HIGH in the air, and--",
                    "Radio|*static*",
                    "|Every light in Minnalpatti flickers. All at once, the minminis lift into the air and stream away east, over the paddy fields.",
                    "Lineman Murugesan|...That is not normal. Something out there is still pulling the current. Tomorrow, thambi, we follow them.",
                    "|CHAPTER 1 COMPLETE.   The road east is open. Follow the minminis into the fields.",
                };
        }

        if (action.StartsWith("mirror") || action.StartsWith("lever")) return Array.Empty<string>();
        if (action.StartsWith("brake"))
        {
            bool already = (brakeMask.Value & (1 << (action[5] - '0'))) != 0;
            return already
                ? new[] { "|The windmill turns steadily, humming in the wind." }
                : new[] { $"|You knock the brake off. The great blades shudder, catch the wind, and begin to turn. That makes {WindmillsTurning + 1} of {WindmillCount}." };
        }
        if (action.StartsWith("lantern")) return new[] { $"|A signalman's lantern, red glass on one side and green on the other. That makes {LanternsFound + 1} of {LanternCount}." };
        if (action.StartsWith("reel")) return new[] { $"|A dusty reel of film. The label says PART {action[4] - '0' + 1}. That makes {ReelsFound + 1} of {ReelCount}." };

        if (action.StartsWith("valve"))
        {
            if (!Has(Flag.BridgeDown) && GameUI.Instance != null)
                GameUI.Instance.Toast($"You haul the wheel round. It will creep shut in {ValveSeconds:0} seconds.\nGet all three open at once!", 4f);
            return Array.Empty<string>();
        }

        switch (action)
        {
            case "sluice":
                return Has(Flag.BridgeDown)
                    ? new[] { "|The sluice bridge is down. The canal rushes underneath." }
                    : new[] { "|The sluice bridge is raised. A faded sign: OPEN ALL THREE VALVES TO LOWER. There are valve wheels out on the bunds, north, south and east of here." };

            case "murugesan":
                if (Finished) return new[] { "Lineman Murugesan|Go home, thambi. Paati will want to know who won." };
                if (WindmillsTurning < WindmillCount) return new[]
                {
                    "Lineman Murugesan|You made it! I cycled up the long way. Look at it, on the dam wall. Poor thing is nearly out.",
                    "Lineman Murugesan|It fell out of last week's storm, and it has been drinking our current ever since, trying to get strong enough to jump home.",
                    $"Lineman Murugesan|Every line you mended tonight ends at this powerhouse. Get all three windmills on the ridge turning, you have {WindmillsTurning}, and wake the generator. Then I can send the whole town's current up one wire. One big jolt.",
                };
                return new[] { $"Lineman Murugesan|All three windmills are turning! Now the generator. It needs a spark to start: {generator.Needed} minminis. I think they know what it is for. Look how they are gathering." };

            case "minnal":
                return new[] { "|The little lightning is curled up on the dam wall, dim as a dying torch. It looks at the clouds, far above, and then at you." };

            case "generator":
                if (generator.IsLit) return new[] { "|The generator sings." };
                if (WindmillsTurning < WindmillCount) return new[] { $"|The powerhouse generator. It will not turn without the windmills. Windmills: {WindmillsTurning} of {WindmillCount}." };
                return new[] { $"|The windmills are feeding it now. It only needs a spark to start: {generator.Needed} minminis." };

            case "rani":
                if (YardDone) return new[] { "Signal Rani|Green all the way to the hills. Go on, catch your train of thought." };
                if (cabin.IsLit) return new[] { "Signal Rani|She is lit!" };
                if (LanternsFound == LanternCount && PointsSet) return new[] { $"Signal Rani|Lanterns hung, points set. All the cabin needs now is current. {cabin.Needed} of your glow-bugs, up at the signal cabin on the far side." };
                return new[]
                {
                    "Signal Rani|Stop right there. Goods yard. Three tracks, three engines, and not one of their drivers can see you in the dark.",
                    "Signal Rani|The night goods is stuck at the home signal because my cabin has no power. Something bright is sitting on its headlamp, and it will not move until the train does.",
                    $"Signal Rani|Help me get the signal to green. I need my {LanternCount} signal lanterns, which the wind scattered between the tracks. You have {LanternsFound}. And the three point levers by the cabin must be set as the notice board shows.",
                    "Signal Rani|Cross BEHIND the engines, never in front. If you see a headlamp coming, you run.",
                };

            case "points":
                return new[] { "|A notice board with a faded diagram of the three point levers: the first UP, the second DOWN, the third UP." };

            case "cabin":
                if (cabin.IsLit) return new[] { "|The signal cabin hums. The lamp on top shines green." };
                if (LanternsFound < LanternCount) return new[] { $"|The signal cabin is dark. There are four empty hooks for lanterns by the door. Lanterns: {LanternsFound} of {LanternCount}." };
                if (!PointsSet) return new[] { "|The lanterns are hung. A buzzer complains: the points are set wrong. Check the notice board." };
                return new[] { $"|Lanterns hung, points set. The cabin only needs power now: {cabin.Needed} minminis." };

            case "watchman":
                if (FilmPlayed) return new[] { "Watchman Kannan|Forty years of films on that screen, and the best show was tonight." };
                if (projector.IsLit) return new[] { "Watchman Kannan|She is running! Now the beam has to reach the screen. Those mirrors were for the matinee crowd to fix their hair. Turn them until the light gets there." };
                if (ReelsFound == ReelCount) return new[] { $"Watchman Kannan|All three reels! Thread them in... there. The lamp is cold, though. {projector.Needed} of your glow-bugs at the projector booth should wake it." };
                return new[]
                {
                    "Watchman Kannan|Raja Talkies. Closed since the big storm, except for me and the ghost.",
                    "Watchman Kannan|Something bright flew in here tonight and hid in the projector. It will not come out. Maybe if the film were running...",
                    $"Watchman Kannan|The last film is in three reels, lost somewhere in the yard. You have {ReelsFound}. And if the white thing comes for you, shine your torch right at it. It stops dead.",
                };

            case "projector":
                if (projector.IsLit) return new[] { "|The projector rattles happily. A hard white beam shoots out across the yard." };
                if (ReelsFound < ReelCount) return new[] { $"|An old film projector. The spools are empty. Reels: {ReelsFound} of {ReelCount}." };
                return new[] { $"|The reels are threaded, but the lamp is dead. It needs a spark: {projector.Needed} minminis." };

            case "scarecrow":
                if (Has(Flag.BeltTaken)) return new[] { "|The scarecrow looks less fashionable without his sash." };
                if (item != PlayerController.Item.None) return new[] { "|Your hands are full." };
                return new[] { "|The scarecrow is wearing a rubber fan belt as a sash. You unhook it. He does not object." };

            case "farmer":
                if (Has(Flag.PumpStarted)) return new[] { "Farmer Periyasamy|Forty years I have farmed here. Never once did the lightning come back for seconds." };
                if (Has(Flag.BeltFitted)) return new[] { $"Farmer Periyasamy|The belt is on. Now she only needs a spark. {pump.Needed} of those glow-bugs should do it. Walk them up to the pump-house." };
                if (item == PlayerController.Item.Belt) return new[]
                {
                    "Farmer Periyasamy|My fan belt! On the SCARECROW? Those crows have a sense of humour.",
                    $"Farmer Periyasamy|There. Fitted. Now she only needs a spark. {pump.Needed} of those glow-bugs should do it. Walk them up to the pump-house.",
                };
                return new[]
                {
                    "Farmer Periyasamy|No current for the pump-set, and the paddy drinks every night. Three more days of this and the crop is finished.",
                    "Farmer Periyasamy|And even if the current came: the crows stole her fan belt. I saw something black on the scarecrow in the north field. My knees are too old for those bunds in the dark.",
                };
        }

        if (action.StartsWith("ball")) return new[] { $"|A cricket ball, slightly chewed. That makes {BallsFound + 1} of {BallCount}." };
        return Array.Empty<string>();
    }

    // ------------------------------------------------------------ what actually happens (server)

    [Rpc(SendTo.Server)]
    void ActRpc(string action, RpcParams rpc = default)
    {
        if (!NetworkManager.ConnectedClients.TryGetValue(rpc.Receive.SenderClientId, out var client)) return;
        var player = client.PlayerObject.GetComponent<PlayerController>();
        var item = player.Carrying;

        switch (action)
        {
            case "glasses" when !Has(Flag.GlassesFound):
                Raise(Flag.GlassesFound);
                player.Carrying = PlayerController.Item.Glasses;
                AnnounceRpc("", "pickup");
                break;

            case "radio" when !Has(Flag.RadioFound):
                Raise(Flag.RadioFound);
                AnnounceRpc("", "pickup");
                break;

            case "paati" when Has(Flag.RadioFound) && !Has(Flag.RadioReturned):
                SideJobDone(Flag.RadioReturned, "Thatha's radio is home.");
                break;

            case "paati" when item == PlayerController.Item.Glasses && !Has(Flag.GlassesReturned):
                player.Carrying = PlayerController.Item.None;
                Raise(Flag.GlassesReturned);
                Reward();
                break;

            case "banana" when item == PlayerController.Item.None && !GoatPenned:
                player.Carrying = PlayerController.Item.Leaf;
                AnnounceRpc("", "pickup");
                break;

            case "teamaster" when GoatPenned && !Has(Flag.GoatRewarded):
                Raise(Flag.GoatRewarded);
                Reward();
                break;

            case "icecart" when item == PlayerController.Item.None && iceMeltsAt.Value == 0 && !Has(Flag.IceDelivered):
                player.Carrying = PlayerController.Item.Ice;
                iceMeltsAt.Value = NetworkManager.ServerTime.Time + IceSeconds;
                AnnounceRpc("", "pickup");
                break;

            case "chairman" when item == PlayerController.Item.Ice:
                player.Carrying = PlayerController.Item.None;
                iceMeltsAt.Value = 0;
                Raise(Flag.IceDelivered);
                Reward();
                break;

            case "ravi" when BallsFound == BallCount && !Has(Flag.BallsReturned):
                Raise(Flag.BallsReturned);
                Reward();
                break;

            case "transformer" when !PowerRestored && Fuses >= FusesNeeded && LampsLit == StreetLight.All.Count:
                Raise(Flag.PowerRestored);
                break;

            case "scarecrow" when item == PlayerController.Item.None && !Has(Flag.BeltTaken):
                Raise(Flag.BeltTaken);
                player.Carrying = PlayerController.Item.Belt;
                AnnounceRpc("", "pickup");
                break;

            case "farmer" when item == PlayerController.Item.Belt:
                player.Carrying = PlayerController.Item.None;
                Raise(Flag.BeltFitted);
                AnnounceRpc("The belt is fitted. Bring minminis to the pump!", "quest");
                break;

            default:
                if (action.StartsWith("brake") && action.Length == 6)
                {
                    brakeMask.Value |= 1 << (action[5] - '0');
                    AnnounceRpc("", "lamp");
                }
                else if (action.StartsWith("lever") && action.Length == 6)
                {
                    leverMask.Value ^= 1 << (action[5] - '0');
                    AnnounceRpc("", "click");
                }
                else if (action.StartsWith("lantern") && action.Length == 8)
                {
                    lanternMask.Value |= 1 << (action[7] - '0');
                    AnnounceRpc("", "pickup");
                }
                else if (action.StartsWith("mirror") && action.Length == 7)
                {
                    mirrorMask.Value ^= 1 << (action[6] - '0');
                    AnnounceRpc("", "click");
                }
                else if (action.StartsWith("reel") && action.Length == 5)
                {
                    reelMask.Value |= 1 << (action[4] - '0');
                    AnnounceRpc("", "pickup");
                }
                else if (action.StartsWith("valve") && action.Length == 6)
                {
                    var times = valveShutsAt.Value;
                    times[action[5] - '0'] = Now + ValveSeconds;
                    valveShutsAt.Value = times;
                    AnnounceRpc("", "click");
                }
                else if (action.StartsWith("ball") && action.Length == 5)
                {
                    ballMask.Value |= 1 << (action[4] - '0');
                    AnnounceRpc("", "pickup");
                }
                break;
        }
    }

    // Server: the goat reached her pen behind the player holding the leaf.
    public void PenGoat(PlayerController leader)
    {
        if (GoatPenned) return;
        Raise(Flag.GoatPenned);
        leader.Carrying = PlayerController.Item.None;
        AnnounceRpc("Lakshmi is home! Go and tell Tea Master Selvam.", "quest");
    }

    void SideJobDone(Flag flag, string message)
    {
        Raise(flag);
        AnnounceRpc($"{message}\nSide-job done: every torch now shines further.", "quest");
    }

    void Reward()
    {
        fuses.Value++;
        AnnounceRpc($"You got a fuse!   {fuses.Value} of {FusesNeeded}", "quest");
    }

    // Shows a message and plays a sound on every machine.
    [Rpc(SendTo.Everyone)]
    public void AnnounceRpc(string message, string sound)
    {
        if (!string.IsNullOrEmpty(sound)) Sfx.Play(sound);
        if (!string.IsNullOrEmpty(message) && GameUI.Instance != null) GameUI.Instance.Toast(message);
    }
}
