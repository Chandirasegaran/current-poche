using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

// Lives on the Player prefab. Every connected player gets one copy of this
// object, and that copy exists on EVERY machine in the game.
// NetworkBehaviour = MonoBehaviour + knowledge about who owns this object.
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : NetworkBehaviour
{
    [Serializable]
    public class KidLook
    {
        // Facing the camera, facing away, and side-on (flipped for left).
        public Sprite idle, walk1, walk2;
        public Sprite upIdle, upWalk1, upWalk2;
        public Sprite sideIdle, sideWalk1, sideWalk2;
    }

    [SerializeField] float moveSpeed = 4.5f;
    [SerializeField] float torchTurnSpeed = 12f;
    [SerializeField] SpriteRenderer body;
    [SerializeField] Transform torchPivot;
    [SerializeField] KidLook[] looks;
    [SerializeField] UnityEngine.Rendering.Universal.Light2D torch;
    [SerializeField] SpriteRenderer carryIcon;
    [SerializeField] SpriteRenderer nameTag;

    // Quick things to say to friends, on keys 1 to 4.
    public static readonly string[] Emotes = { "Come here!", "Wait!", "Look!", "Ha ha!" };
    string tagText;
    float emoteUntil;
    [SerializeField] Sprite[] itemSprites; // indexed by Item

    // The eight kids, in the order of the "looks" list: four boys, then four girls.
    public static readonly string[] Names = { "Kavin", "Abdul", "Arul", "Muthu", "Yazhini", "Mercy", "Nila", "Kayal" };

    public string CharacterName => Names[look.Value % Names.Length];

    // Things a player can be holding for a task.
    public enum Item : byte { None, Glasses, Leaf, Ice, Belt }

    // The player object that belongs to this machine (the camera follows it).
    public static PlayerController Local { get; private set; }

    // Every player currently in the game, on this machine's view of it.
    public static readonly List<PlayerController> All = new();

    public Vector2 Facing => facing.Value;

    // Extra shove from outside (the wind), added to this frame's movement.
    public Vector2 Push { get; set; }

    // What this player is holding. Only the server may change it.
    public Item Carrying
    {
        get => (Item)carrying.Value;
        set => carrying.Value = (byte)value;
    }

    // The closest thing this player could talk to or use right now, if any.
    public Interactable Nearby { get; private set; }

    // NetworkVariables are values that every machine sees.
    // "look" is decided by the server; "facing" is written by the owning player.
    readonly NetworkVariable<byte> look = new();
    readonly NetworkVariable<Vector2> facing = new(Vector2.down,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    readonly NetworkVariable<byte> carrying = new();

    Rigidbody2D rb;
    int walkFrame;
    Vector2 moveInput;
    Vector3 lastPosition;
    float walkClock;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    // Called on every machine when this player object appears on the network.
    public override void OnNetworkSpawn()
    {
        // IsOwner is true only on the machine of the player who controls this object.
        if (IsOwner)
        {
            Local = this;
            ChooseLookRpc((byte)GameSettings.Character); // tell everyone which kid I picked
            var spawn = GameObject.Find("SpawnPoint");
            var start = spawn != null ? spawn.transform.position : Vector3.zero;
            transform.position = start + new Vector3(OwnerClientId % 4 * 1.2f, 0f, 0f);
        }

        lastPosition = transform.position;
        All.Add(this);
    }

    // Each player picks their kid on the title screen; the server passes it on.
    [Rpc(SendTo.Server)]
    void ChooseLookRpc(byte choice)
    {
        look.Value = (byte)(choice % looks.Length);
        if (OwnerClientId != NetworkManager.ServerClientId && Quests.Instance != null)
            Quests.Instance.AnnounceRpc($"{CharacterName} joined the game.", "pickup");
    }

    public override void OnNetworkDespawn()
    {
        // A player who leaves puts down whatever they were carrying, and the others are told.
        if (IsServer && !NetworkManager.ShutdownInProgress && Quests.Instance != null && Quests.Instance.IsSpawned)
        {
            Quests.Instance.Dropped(Carrying);
            if (OwnerClientId != NetworkManager.ServerClientId)
                Quests.Instance.AnnounceRpc($"{CharacterName} left the game.", "fail");
        }
        All.Remove(this);
        if (Local == this) Local = null;
    }

    // An emote goes to the server, which shows it to everyone.
    [Rpc(SendTo.Server)]
    public void EmoteRpc(byte which) => ShowEmoteRpc(which);

    [Rpc(SendTo.Everyone)]
    void ShowEmoteRpc(byte which)
    {
        emoteUntil = Time.time + 2.5f;
        SetTag(Emotes[which % Emotes.Length], new Color(1f, 0.86f, 0.42f));
        Sfx.PlayAt("click", transform.position);
    }

    // Writes the floating text above this player's head.
    void SetTag(string text, Color colour)
    {
        if (text == tagText) return;
        tagText = text;
        if (nameTag.sprite != null)
        {
            Destroy(nameTag.sprite.texture);
            Destroy(nameTag.sprite);
        }
        var texture = PixelFont.Render(text, 0, true);
        nameTag.sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0f), 16f);
        nameTag.color = colour;
    }

    // Sent by the server to the player's own machine when a ghost or a train
    // catches them: they are put back at a safe spot.
    [Rpc(SendTo.Owner)]
    public void SpookRpc(Vector3 backTo, string message) => SendBack(backTo, message);

    // Puts this machine's own player back at a safe spot, with a message.
    public void SendBack(Vector3 backTo, string message)
    {
        rb.position = backTo;
        transform.position = backTo;
        Sfx.Play("fail");
        if (GameUI.Instance != null)
            GameUI.Instance.Toast(message, 5f);
    }

    void Update()
    {
        if (IsSpawned && IsOwner) ReadInput();
        Animate();
    }

    void ReadInput()
    {
        moveInput = Vector2.zero;
        Nearby = Interactable.Closest(transform.position, 1.7f);

        // Stand still while reading dialogue or using a menu.
        if (GameUI.BlocksInput) return;

        // Keys (which can be changed in the settings), arrows, or the touch stick.
        moveInput = Controls.Movement();

        for (int i = 0; i < Emotes.Length; i++)
            if (Controls.Tapped(Key.Digit1 + i)) EmoteRpc((byte)i);
        if (TouchInput.Emote) EmoteRpc(0);

        if ((Controls.Pressed(GameAction.Whistle) || TouchInput.Whistle) && Dog.Instance != null)
        {
            Sfx.Play("whistle");
            Dog.Instance.WhistleRpc();
        }

        // The torch points where you last walked.
        if (moveInput != Vector2.zero && moveInput != facing.Value)
            facing.Value = moveInput;
    }

    void FixedUpdate()
    {
        // Only the owner moves its own copy. NetworkTransform sends the result
        // to everyone else, and physics keeps us out of walls.
        if (IsSpawned && IsOwner)
        {
            rb.linearVelocity = moveInput * moveSpeed + Push;
            Push = Vector2.zero;
        }
    }

    // Runs for every player on every machine, so remote players animate too.
    void Animate()
    {
        var kid = looks[look.Value % looks.Length];
        float speed = (transform.position - lastPosition).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        lastPosition = transform.position;

        // Pick the set of pictures for the way this player is facing.
        var look2 = facing.Value;
        bool sideways = Mathf.Abs(look2.x) > Mathf.Abs(look2.y);
        bool away = !sideways && look2.y > 0f;
        Sprite still = sideways ? kid.sideIdle : away ? kid.upIdle : kid.idle;
        Sprite stepA = sideways ? kid.sideWalk1 : away ? kid.upWalk1 : kid.walk1;
        Sprite stepB = sideways ? kid.sideWalk2 : away ? kid.upWalk2 : kid.walk2;

        if (speed > 0.3f)
        {
            walkClock += Time.deltaTime;
            int frame = (int)(walkClock / 0.14f) % 2;
            body.sprite = frame == 0 ? stepA : stepB;
            if (frame != walkFrame && IsOwner) Sfx.Play("step", 0.45f);
            walkFrame = frame;
        }
        else
        {
            walkClock = 0f;
            body.sprite = still;
        }

        // Friends have their name over their head; an emote replaces it for a moment.
        if (Time.time > emoteUntil) SetTag(IsOwner ? "" : CharacterName, new Color(0.82f, 0.86f, 0.98f, 0.85f));
        nameTag.transform.localPosition = new Vector3(0f, Carrying == Item.None ? 1.4f : 1.95f, 0f);

        // Every side-job finished makes everyone's torch reach a little further.
        if (Quests.Instance != null) torch.pointLightOuterRadius = 7.5f + Quests.Instance.SideJobs;

        carryIcon.sprite = itemSprites[(int)Carrying];
        carryIcon.transform.localPosition = new Vector3(0f, 1.5f + Mathf.Sin(Time.time * 4f) * 0.05f, 0f);

        var direction = facing.Value;
        body.flipX = sideways && direction.x < 0; // the side view is drawn facing right

        // The spotlight shines along its local "up", hence the -90.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        torchPivot.rotation = Quaternion.Slerp(torchPivot.rotation,
            Quaternion.Euler(0f, 0f, angle), torchTurnSpeed * Time.deltaTime);
    }
}
