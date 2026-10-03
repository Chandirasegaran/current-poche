using Unity.Netcode;
using UnityEngine;

// A bandicoot: the big rats that own the town after dark. They chase anyone
// who is leading minminis and scatter them on contact. They can't stand
// torchlight, so shining your torch at one sends it running.
public class Bandicoot : NetworkBehaviour
{
    enum State { Prowling, Chasing, Fleeing }

    [SerializeField] SpriteRenderer body;
    [SerializeField] Sprite frame1, frame2;

    const float ChaseRange = 6f, TorchRange = 6.5f, TorchHalfAngle = 45f;

    State state;
    Vector3 lair, target, lastPosition;
    PlayerController prey;
    float timer;

    public override void OnNetworkSpawn()
    {
        lair = target = lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 moved = transform.position - lastPosition;
        lastPosition = transform.position;
        if (moved.sqrMagnitude > 0.00001f)
        {
            body.sprite = Time.time % 0.24f < 0.12f ? frame1 : frame2;
            if (Mathf.Abs(moved.x) > 0.001f) body.flipX = moved.x < 0;
        }

        if (IsSpawned && IsServer) Think();
    }

    void Think()
    {
        timer -= Time.deltaTime;

        // Any torch pointed at me sends me running, whatever I was doing.
        foreach (var player in PlayerController.All)
        {
            Vector2 toMe = transform.position - player.transform.position;
            if (state != State.Fleeing && toMe.magnitude < TorchRange && Vector2.Angle(player.Facing, toMe) < TorchHalfAngle)
            {
                Flee(player, 3f);
                SqueakRpc(ulong.MaxValue);
            }
        }

        switch (state)
        {
            case State.Prowling:
                if (Vector3.Distance(transform.position, target) < 0.2f || timer <= 0f)
                {
                    target = lair + (Vector3)(Random.insideUnitCircle * 4f);
                    timer = Random.Range(2f, 5f);
                }
                Move(target, 1.3f);

                foreach (var player in PlayerController.All)
                    if (Vector2.Distance(transform.position, player.transform.position) < ChaseRange && Minmini.Followers(player) > 0)
                    {
                        prey = player;
                        state = State.Chasing;
                    }
                break;

            case State.Chasing:
                if (prey == null || !prey.IsSpawned || Minmini.Followers(prey) == 0 ||
                    Vector2.Distance(transform.position, prey.transform.position) > ChaseRange * 1.6f)
                {
                    state = State.Prowling;
                    break;
                }
                Move(prey.transform.position, 5f);
                if (Vector2.Distance(transform.position, prey.transform.position) < 0.6f)
                {
                    Minmini.Scatter(prey);
                    SqueakRpc(prey.OwnerClientId);
                    Flee(prey, 4f);
                }
                break;

            case State.Fleeing:
                if (prey != null && prey.IsSpawned)
                {
                    Vector3 away = (transform.position - prey.transform.position).normalized;
                    Move(transform.position + away, 5.6f);
                }
                if (timer <= 0f)
                {
                    state = State.Prowling;
                    target = lair;
                    timer = 6f;
                }
                break;
        }
    }

    void Flee(PlayerController from, float seconds)
    {
        prey = from;
        state = State.Fleeing;
        timer = seconds;
    }

    void Move(Vector3 destination, float speed)
    {
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
    }

    [Rpc(SendTo.Everyone)]
    void SqueakRpc(ulong victim)
    {
        Sfx.PlayAt("squeak", transform.position, 0.8f);
        if (victim == NetworkManager.LocalClientId && GameUI.Instance != null)
            GameUI.Instance.Toast("A bandicoot scattered your minminis!\nShine your torch at them to scare them off.", 5f);
    }
}
