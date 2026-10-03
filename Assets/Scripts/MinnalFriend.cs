using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Minnal in the last chapter. Once the signal is green it waits at the foot of
// the ridge and then follows the players up, flickering and weak. At the dam it
// settles on the wall until the generator sends it home.
// The server moves it; every machine shows it.
public class MinnalFriend : NetworkBehaviour
{
    [SerializeField] SpriteRenderer body;
    [SerializeField] Light2D glow;
    [SerializeField] Transform perch; // its place on the dam wall

    readonly NetworkVariable<bool> settled = new();

    float seed;

    void Awake() => seed = Random.value * 10f;

    void Update()
    {
        var quests = Quests.Instance;
        bool here = IsSpawned && quests != null && quests.YardDone && !quests.Finished;
        body.enabled = glow.enabled = here;
        if (!here) return;

        // Dim and unsteady: it has very little current left.
        glow.intensity = 0.7f + Mathf.PerlinNoise(seed, Time.time * 7f) * 0.9f;
        body.transform.localPosition = new Vector3(0f, 0.9f + Mathf.Sin(Time.time * 3f + seed) * 0.12f, 0f);

        if (!IsServer) return;
        if (settled.Value)
        {
            transform.position = Vector3.MoveTowards(transform.position, perch.position, 6f * Time.deltaTime);
            return;
        }

        // Follow whoever is nearest on the hillside, a step behind them.
        PlayerController friend = null;
        float closest = 40f;
        foreach (var player in PlayerController.All)
        {
            float distance = Vector2.Distance(transform.position, player.transform.position);
            if (player.transform.position.x < -65f && distance < closest) { closest = distance; friend = player; }
        }
        if (friend == null) return;

        if (closest > 1.6f)
            transform.position = Vector3.MoveTowards(transform.position, friend.transform.position, 5.2f * Time.deltaTime);
        if (Vector2.Distance(transform.position, perch.position) < 16f) settled.Value = true; // it has reached the dam
    }
}
