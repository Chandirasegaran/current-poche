using Unity.Netcode;
using UnityEngine;

// Battery, the street dog. He trots after whichever player is closest, until
// someone whistles: then he sits and stays where he is (handy for holding
// down a stone slab) until the next whistle.
// The server moves him; every machine animates him.
[RequireComponent(typeof(Rigidbody2D))]
public class Dog : NetworkBehaviour
{
    [SerializeField] SpriteRenderer body;
    [SerializeField] Sprite idle, walk1, walk2;
    [SerializeField] float speed = 4.2f;

    public static Dog Instance { get; private set; }

    readonly NetworkVariable<bool> staying = new();

    Rigidbody2D rb;
    Vector3 lastPosition;
    float walkClock;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lastPosition = transform.position;
        Instance = this;
    }

    [Rpc(SendTo.Server)]
    public void WhistleRpc()
    {
        staying.Value = !staying.Value;
        HeardRpc(staying.Value);
    }

    [Rpc(SendTo.Everyone)]
    void HeardRpc(bool stay)
    {
        Sfx.Play("bark", 0.7f);
        if (GameUI.Instance != null) GameUI.Instance.Toast(stay ? "Battery: STAY. He sits down." : "Battery: COME. He follows again.", 3f);
    }

    void FixedUpdate()
    {
        if (!IsSpawned || !IsServer) return;

        PlayerController friend = null;
        float closest = float.MaxValue;
        foreach (var player in PlayerController.All)
        {
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < closest) { closest = distance; friend = player; }
        }

        if (staying.Value || friend == null || closest < 2.2f)
        {
            rb.linearVelocity = Vector2.zero;
        }
        else if (closest > 16f && !staying.Value)
        {
            // Left far behind (or stuck behind a house): catch up at once.
            rb.position = friend.transform.position + Vector3.right;
        }
        else
        {
            Vector2 direction = (friend.transform.position - transform.position).normalized;
            rb.linearVelocity = direction * speed;
        }
    }

    void Update()
    {
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        float pace = moved.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);

        if (pace > 0.3f)
        {
            walkClock += Time.deltaTime;
            body.sprite = (int)(walkClock / 0.12f) % 2 == 0 ? walk1 : walk2;
            if (Mathf.Abs(moved.x) > 0.001f) body.flipX = moved.x < 0;
        }
        else body.sprite = idle;
    }
}
