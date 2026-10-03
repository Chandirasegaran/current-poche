using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// The "ghost" of Raja Talkies. It drifts towards anyone in the cinema yard and,
// if it touches them, they run all the way back to the gate. A torch pointed
// straight at it makes it freeze and fade. It vanishes for good once the film plays.
public class Ghost : NetworkBehaviour
{
    [SerializeField] SpriteRenderer body;

    readonly NetworkVariable<bool> frozen = new();

    Vector3 home, target;
    float wanderTimer, seed;

    public override void OnNetworkSpawn()
    {
        home = target = transform.position;
        seed = Random.value * 10f;
    }

    void Update()
    {
        // Every machine: float gently, and go see-through while frozen in torchlight.
        var colour = body.color;
        colour.a = Mathf.MoveTowards(colour.a, frozen.Value ? 0.25f : 0.9f, Time.deltaTime * 2f);
        body.color = colour;
        body.transform.localPosition = new Vector3(0f, 0.2f + Mathf.Sin(Time.time * 2f + seed) * 0.12f, 0f);

        if (IsSpawned && IsServer) Think();
    }

    void Think()
    {
        if (Quests.Instance.FilmPlayed)
        {
            NetworkObject.Despawn();
            return;
        }

        PlayerController nearest = null;
        float closest = 11f;
        bool inTorchlight = false;
        foreach (var player in PlayerController.All)
        {
            Vector2 toMe = transform.position - player.transform.position;
            float distance = toMe.magnitude;
            if (distance < 7f && Vector2.Angle(player.Facing, toMe) < 45f) inTorchlight = true;
            if (distance < closest) { closest = distance; nearest = player; }
        }

        frozen.Value = inTorchlight;
        if (inTorchlight) return;

        if (nearest != null && Vector3.Distance(nearest.transform.position, home) < 30f)
        {
            Move(nearest.transform.position, 2.6f);
            if (closest < 0.7f)
            {
                nearest.SpookRpc(Quests.Instance.SpookPoint);
                GetComponent<NetworkTransform>().Teleport(home, Quaternion.identity, Vector3.one);
            }
            return;
        }

        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            target = home + (Vector3)(Random.insideUnitCircle * 5f);
            wanderTimer = Random.Range(3f, 6f);
        }
        Move(target, 1f);
    }

    void Move(Vector3 destination, float speed)
    {
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
    }
}
