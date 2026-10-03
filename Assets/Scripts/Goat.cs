using Unity.Netcode;
using UnityEngine;

// Lakshmi the goat. She grazes until someone walks up holding a banana leaf,
// then follows them. Lead her to her pen and she stays there.
[RequireComponent(typeof(Rigidbody2D))]
public class Goat : NetworkBehaviour
{
    [SerializeField] SpriteRenderer body;
    [SerializeField] Sprite idle, walk1, walk2;
    [SerializeField] Transform pen;
    [SerializeField] float speed = 3.9f;

    Rigidbody2D rb;
    Vector3 lastPosition;
    float walkClock;
    bool wasMoving;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        lastPosition = transform.position;
    }

    void FixedUpdate()
    {
        if (!IsSpawned || !IsServer) return;

        if (Quests.Instance.GoatPenned)
        {
            rb.linearVelocity = Vector2.zero;
            rb.position = Vector2.MoveTowards(rb.position, pen.position, 3f * Time.fixedDeltaTime);
            return;
        }

        PlayerController leader = null;
        float closest = 8f;
        foreach (var player in PlayerController.All)
        {
            if (player.Carrying != PlayerController.Item.Leaf) continue;
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (distance < closest) { closest = distance; leader = player; }
        }

        if (leader == null || closest < 1.6f)
            rb.linearVelocity = Vector2.zero;
        else
            rb.linearVelocity = (Vector2)(leader.transform.position - transform.position).normalized * speed;

        if (leader != null && Vector2.Distance(transform.position, pen.position) < 2.4f)
            Quests.Instance.PenGoat(leader);
    }

    void Update()
    {
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        bool moving = moved.magnitude / Mathf.Max(Time.deltaTime, 0.0001f) > 0.3f;

        if (moving)
        {
            walkClock += Time.deltaTime;
            body.sprite = (int)(walkClock / 0.13f) % 2 == 0 ? walk1 : walk2;
            if (Mathf.Abs(moved.x) > 0.001f) body.flipX = moved.x < 0;
            if (!wasMoving && Random.value < 0.5f) Sfx.PlayAt("bleat", transform.position, 0.7f);
        }
        else body.sprite = idle;
        wasMoving = moving;
    }
}
