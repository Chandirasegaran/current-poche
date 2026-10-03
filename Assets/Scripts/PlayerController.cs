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
        public Sprite idle;
        public Sprite walk1;
        public Sprite walk2;
    }

    [SerializeField] float moveSpeed = 4.5f;
    [SerializeField] float torchTurnSpeed = 12f;
    [SerializeField] SpriteRenderer body;
    [SerializeField] Transform torchPivot;
    [SerializeField] KidLook[] looks;
    [SerializeField] SpriteRenderer carryIcon;
    [SerializeField] Sprite[] itemSprites; // indexed by Item

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
        if (IsServer)
            look.Value = (byte)(OwnerClientId % (ulong)looks.Length);

        // IsOwner is true only on the machine of the player who controls this object.
        if (IsOwner)
        {
            Local = this;
            var spawn = GameObject.Find("SpawnPoint");
            var start = spawn != null ? spawn.transform.position : Vector3.zero;
            transform.position = start + new Vector3(OwnerClientId % 4 * 1.2f, 0f, 0f);
        }

        lastPosition = transform.position;
        All.Add(this);
    }

    public override void OnNetworkDespawn()
    {
        All.Remove(this);
        if (Local == this) Local = null;
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
        var keyboard = Keyboard.current;
        moveInput = Vector2.zero;
        Nearby = Interactable.Closest(transform.position, 1.7f);

        // Stand still while reading dialogue or using a menu.
        if (keyboard == null || GameUI.BlocksInput) return;

        // The movement keys can be changed in the settings; the arrow keys always work too.
        if (Controls.Held(GameAction.Up) || keyboard.upArrowKey.isPressed) moveInput.y += 1;
        if (Controls.Held(GameAction.Down) || keyboard.downArrowKey.isPressed) moveInput.y -= 1;
        if (Controls.Held(GameAction.Left) || keyboard.leftArrowKey.isPressed) moveInput.x -= 1;
        if (Controls.Held(GameAction.Right) || keyboard.rightArrowKey.isPressed) moveInput.x += 1;
        moveInput = moveInput.normalized;

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

        if (speed > 0.3f)
        {
            walkClock += Time.deltaTime;
            int frame = (int)(walkClock / 0.14f) % 2;
            body.sprite = frame == 0 ? kid.walk1 : kid.walk2;
            if (frame != walkFrame && IsOwner) Sfx.Play("step", 0.45f);
            walkFrame = frame;
        }
        else
        {
            walkClock = 0f;
            body.sprite = kid.idle;
        }

        carryIcon.sprite = itemSprites[(int)Carrying];
        carryIcon.transform.localPosition = new Vector3(0f, 1.5f + Mathf.Sin(Time.time * 4f) * 0.05f, 0f);

        var direction = facing.Value;
        if (Mathf.Abs(direction.x) > 0.01f) body.flipX = direction.x < 0;

        // The spotlight shines along its local "up", hence the -90.
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        torchPivot.rotation = Quaternion.Slerp(torchPivot.rotation,
            Quaternion.Euler(0f, 0f, angle), torchTurnSpeed * Time.deltaTime);
    }
}
