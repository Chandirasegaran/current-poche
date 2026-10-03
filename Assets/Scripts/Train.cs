using Unity.Netcode;
using UnityEngine;

// A shunting engine that trundles back and forth along its track in the goods
// yard. Anyone standing on the track when it arrives gets sent back to the yard
// entrance. The server drives it; it stops once the signal has been set.
public class Train : NetworkBehaviour
{
    [SerializeField] SpriteRenderer body;
    [SerializeField] float westEnd = -140f, eastEnd = -80f, speed = 9f;

    float direction = 1f;
    float lastX;

    void Update()
    {
        float moved = transform.position.x - lastX;
        lastX = transform.position.x;
        if (Mathf.Abs(moved) > 0.0001f) body.flipX = moved < 0f; // headlamp leads

        if (!IsSpawned || !IsServer || Quests.Instance.YardDone) return;

        var position = transform.position;
        position.x += direction * speed * Time.deltaTime;
        if (position.x > eastEnd) direction = -1f;
        if (position.x < westEnd) direction = 1f;
        transform.position = position;

        foreach (var player in PlayerController.All)
        {
            Vector2 offset = player.transform.position - position;
            if (Mathf.Abs(offset.x) < 3.2f && offset.y > -0.4f && offset.y < 1.1f)
                player.SpookRpc(Quests.Instance.YardEntrance,
                    "TOOOOT! You jumped clear and ran back to the gate.\nWatch the headlamp and cross behind the engine.");
        }
    }
}
