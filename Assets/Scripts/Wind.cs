using UnityEngine;

// A gusty stretch of the ridge. Every few seconds the wind blows hard across
// it and shoves this machine's player sideways. Walk into the wind, or get
// blown off the ledge.
public class Wind : MonoBehaviour
{
    [SerializeField] Vector2 size = new(30f, 4f);
    [SerializeField] Vector2 push = new(0f, -5.5f);
    [SerializeField] float period = 6f, gustSeconds = 1.6f, offset;
    [SerializeField] ParticleSystem streaks;

    bool wasGusting;

    bool Gusting => (Time.time + offset) % period < gustSeconds;

    void FixedUpdate()
    {
        var player = PlayerController.Local;
        bool gusting = Gusting;

        if (gusting != wasGusting)
        {
            if (gusting) streaks.Play();
            else streaks.Stop();
            if (gusting && player != null && Vector2.Distance(player.transform.position, transform.position) < size.x)
                Sfx.Play("gust", 0.8f);
        }
        wasGusting = gusting;

        if (!gusting || player == null) return;
        Vector2 local = player.transform.position - transform.position;
        if (Mathf.Abs(local.x) < size.x / 2f && Mathf.Abs(local.y) < size.y / 2f)
            player.Push += push;
    }
}
