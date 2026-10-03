using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// A spark-bug. It drifts about until a torch shines on it, then follows that
// player. If it passes a dead streetlight on the way, it flies in and feeds it.
// All of the thinking runs on the server; other machines only see the result.
public class Minmini : NetworkBehaviour
{
    enum State { Wandering, Following, Delivering }

    [SerializeField] Transform visual;

    const float NoticeRange = 6.5f, TorchHalfAngle = 48f, LoseRange = 13f, LampRange = 4f;

    static readonly List<Minmini> all = new();

    State state;
    Vector3 home, target;
    PlayerController leader;
    StreetLight lamp;
    float thinkTimer, orbitAngle, orbitRadius, bobSeed;

    public override void OnNetworkSpawn()
    {
        bobSeed = Random.value * 10f;
        if (!IsServer) return;
        all.Add(this);
        home = target = transform.position;
        orbitAngle = Random.value * 360f;
        orbitRadius = Random.Range(0.7f, 1.5f);
    }

    public override void OnNetworkDespawn() => all.Remove(this);

    // Server: how many minminis are trailing this player right now.
    public static int Followers(PlayerController player)
    {
        int count = 0;
        foreach (var minmini in all)
            if (minmini.state == State.Following && minmini.leader == player) count++;
        return count;
    }

    // Server: a bandicoot got in among them, and they all bolt.
    public static void Scatter(PlayerController player)
    {
        foreach (var minmini in all)
        {
            if (minmini.state != State.Following || minmini.leader != player) continue;
            minmini.StartWandering();
            minmini.home = minmini.transform.position + (Vector3)(Random.insideUnitCircle.normalized * 7f);
            minmini.target = minmini.home;
            minmini.thinkTimer = 2.5f; // too startled to notice a torch for a moment
        }
    }

    void Update()
    {
        // Every machine: a little hover and pulse so they feel alive.
        float t = Time.time * 3f + bobSeed;
        visual.localPosition = new Vector3(0f, 0.35f + Mathf.Sin(t) * 0.08f, 0f);
        visual.localScale = Vector3.one * (0.8f + Mathf.Sin(t * 1.7f) * 0.12f);

        if (IsSpawned && IsServer) Think();
    }

    void Think()
    {
        thinkTimer -= Time.deltaTime;

        switch (state)
        {
            case State.Wandering:
                if (thinkTimer <= 0f)
                {
                    thinkTimer = Random.Range(0.2f, 0.4f);
                    leader = FindTorch();
                    if (leader != null) { state = State.Following; break; }
                    if (Vector3.Distance(transform.position, target) < 0.2f)
                        target = home + (Vector3)(Random.insideUnitCircle * 2.5f);
                }
                Move(target, thinkTimer > 1f ? 6f : 0.8f);
                break;

            case State.Following:
                if (leader == null || !leader.IsSpawned ||
                    Vector3.Distance(transform.position, leader.transform.position) > LoseRange)
                {
                    StartWandering();
                    break;
                }
                if (thinkTimer <= 0f)
                {
                    thinkTimer = 0.3f;
                    lamp = FindLamp();
                    if (lamp != null) { state = State.Delivering; break; }
                }
                orbitAngle += 70f * Time.deltaTime;
                var offset = Quaternion.Euler(0f, 0f, orbitAngle) * Vector3.right * orbitRadius;
                Move(leader.transform.position + offset, 6f);
                break;

            case State.Delivering:
                Move(lamp.BulbPosition, 7f);
                if (Vector3.Distance(transform.position, lamp.BulbPosition) < 0.15f)
                {
                    lamp.Deliver();
                    lamp = null;
                    WorldSpawner.Instance.Recycle(this);
                }
                break;
        }
    }

    void Move(Vector3 destination, float speed)
    {
        transform.position = Vector3.MoveTowards(transform.position, destination, speed * Time.deltaTime);
    }

    void StartWandering()
    {
        state = State.Wandering;
        leader = null;
        home = target = transform.position;
    }

    // Sent somewhere new after feeding a lamp, so the town never runs out.
    public void Respawn(Vector3 position)
    {
        GetComponent<NetworkTransform>().Teleport(position, Quaternion.identity, Vector3.one);
        StartWandering();
    }

    // Is a player close by, or shining their torch at me?
    PlayerController FindTorch()
    {
        foreach (var player in PlayerController.All)
        {
            Vector2 toMe = transform.position - player.transform.position;
            float distance = toMe.magnitude;
            if (distance < 1.6f) return player;
            if (distance < NoticeRange && Vector2.Angle(player.Facing, toMe) < TorchHalfAngle) return player;
        }
        return null;
    }

    StreetLight FindLamp()
    {
        foreach (var candidate in StreetLight.Feedable)
        {
            if (candidate.IsLit) continue;
            if (Vector2.Distance(leader.transform.position, candidate.transform.position) > LampRange) continue;
            if (candidate.TryReserve()) return candidate;
        }
        return null;
    }
}
