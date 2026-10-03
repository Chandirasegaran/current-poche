using Unity.Netcode;
using UnityEngine;

// A stone slab that holds a gate open while something heavy is on it: a
// player, Battery (tell him to stay), or a crate.
public class PressurePlate : NetworkBehaviour
{
    [SerializeField] SpriteRenderer slab;
    [SerializeField] Sprite up, down;
    [SerializeField] GameObject gate;

    readonly NetworkVariable<bool> pressed = new();

    void Update()
    {
        if (IsSpawned && IsServer)
        {
            bool weight = false;
            foreach (var player in PlayerController.All) weight |= On(player.transform);
            foreach (var crate in Crate.All) weight |= !crate.Held && On(crate.transform);
            if (Dog.Instance != null) weight |= On(Dog.Instance.transform);
            if (weight != pressed.Value)
            {
                pressed.Value = weight;
                ClickRpc();
            }
        }

        bool open = IsSpawned && pressed.Value;
        slab.sprite = open ? down : up;
        gate.SetActive(!open);
    }

    bool On(Transform thing) => Vector2.Distance(thing.position, transform.position) < 0.85f;

    [Rpc(SendTo.Everyone)]
    void ClickRpc() => Sfx.PlayAt("click", transform.position);
}
