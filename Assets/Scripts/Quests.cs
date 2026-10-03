using System;
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
    const float IceSeconds = 40f;

    [Flags]
    enum Flag
    {
        GlassesFound = 1, GlassesReturned = 2, GoatPenned = 4, GoatRewarded = 8,
        IceDelivered = 16, BallsReturned = 32, PowerRestored = 64,
        // Chapter 2: the fields
        BridgeDown = 128, BeltTaken = 256, BeltFitted = 512, PumpStarted = 1024,
    }

    const float ValveSeconds = 30f;

    static readonly string[] PumpEnding =
    {
        "|The pump coughs, catches, and roars. Water gushes into the channels.",
        "Farmer Periyasamy|Ahh! Listen to her sing! Thambi, you have saved the whole season's--",
        "|Something on the roof of the pump-house is glowing. It is the size of a kitten, and shaped like a lightning bolt.",
        "|It drinks the spark straight out of the motor, looks at you with two round, frightened eyes, and is gone. North. Towards the old cinema.",
        "Farmer Periyasamy|...That was not a minmini.",
        "|CHAPTER 2 COMPLETE.   (Chapter 3: Last Show at Raja Talkies is coming.)",
    };

    public static Quests Instance { get; private set; }

    [SerializeField] GameObject[] powerOn;  // shown once the power is back (lit windows)
    [SerializeField] GameObject[] powerOff; // removed once the power is back (the east barricade)
    [SerializeField] GameObject bridgeBlocker, bridgeRaised, bridgeLowered;
    [SerializeField] SpriteRenderer[] valves;
    [SerializeField] Sprite valveShut, valveOpen;
    [SerializeField] StreetLight pump;
    [SerializeField] GameObject minnal;

    readonly NetworkVariable<int> flags = new();
    readonly NetworkVariable<int> ballMask = new();
    readonly NetworkVariable<int> fuses = new();
    readonly NetworkVariable<double> iceMeltsAt = new();
    readonly NetworkVariable<Vector3> valveShutsAt = new(); // one time per valve (x, y, z)

    Interactable[] actors;

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
            flags.Value = ballMask.Value = fuses.Value = 0;
            iceMeltsAt.Value = 0;
            valveShutsAt.Value = Vector3.zero;
        }
        flags.OnValueChanged += OnFlagsChanged;
    }

    public override void OnNetworkDespawn() => flags.OnValueChanged -= OnFlagsChanged;

    void OnFlagsChanged(int before, int now)
    {
        bool Became(Flag flag) => (before & (int)flag) == 0 && (now & (int)flag) != 0;

        if (Became(Flag.PowerRestored)) Sfx.Play("power");
        if (Became(Flag.BridgeDown)) Sfx.Play("lamp");
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

        if (IsSpawned && IsServer)
        {
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
        if (action.StartsWith("ball")) return (ballMask.Value & (1 << (action[4] - '0'))) == 0;
        return true;
    }

    // The checklist shown on screen.
    public string LogText()
    {
        if (!IsSpawned) return "";
        if (Has(Flag.PumpStarted)) return "Chapter 2 complete!\nMore is coming.";
        if (PowerRestored)
            return "The Pump-set\n"
                   + (Has(Flag.BridgeDown) ? "+ " : "- ") + "Open the sluice bridge\n"
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
                if (Has(Flag.GlassesReturned)) return new[] { "Paati|Now I can see how dark it is. Wonderful. Go on, kanna, the town is waiting." };
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

            case "teamaster":
                if (Has(Flag.GoatRewarded)) return new[] { "Tea Master Selvam|Lakshmi is sulking, the stove is hot, and you are my favourite customer. Second tea is still full price." };
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
                if (action.StartsWith("valve") && action.Length == 6)
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
