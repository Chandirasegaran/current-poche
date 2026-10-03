using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

// A crate you can pick up, carry over your head, and throw. Press the use key
// next to it to lift it, and again to throw it the way you are facing.
// The server moves it; everyone else just sees where it is.
public class Crate : NetworkBehaviour
{
    public static readonly List<Crate> All = new();

    const ulong Nobody = ulong.MaxValue;
    const float ThrowDistance = 3.2f, ThrowSeconds = 0.35f;

    readonly NetworkVariable<ulong> holder = new(Nobody);

    Interactable handle;
    Vector3 throwFrom, throwTo;
    float flight = -1f;

    public bool Held => holder.Value != Nobody;

    void Awake() => handle = GetComponent<Interactable>();
    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public override void OnNetworkSpawn()
    {
        if (IsServer) holder.Value = Nobody;
    }

    void Update()
    {
        handle.verb = Held ? "Throw" : "Pick up";
        if (!IsSpawned || !IsServer) return;

        if (Held)
        {
            var carrier = Carrier();
            if (carrier == null) { holder.Value = Nobody; return; }
            transform.position = carrier.transform.position + Vector3.up * 1.25f;
        }
        else if (flight >= 0f)
        {
            // An arc from the thrower's hands to the ground.
            flight += Time.deltaTime / ThrowSeconds;
            float t = Mathf.Clamp01(flight);
            transform.position = Vector3.Lerp(throwFrom, throwTo, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.8f;
            if (flight >= 1f)
            {
                flight = -1f;
                LandRpc();
            }
        }
    }

    PlayerController Carrier()
    {
        foreach (var player in PlayerController.All)
            if (player.OwnerClientId == holder.Value) return player;
        return null;
    }

    // Asked for by whoever pressed the use key next to this crate.
    [Rpc(SendTo.Server)]
    public void UseRpc(RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;
        if (!Held)
        {
            foreach (var other in All)
                if (other.holder.Value == sender) return; // already carrying one
            holder.Value = sender;
            flight = -1f;
        }
        else if (holder.Value == sender)
        {
            var carrier = Carrier();
            holder.Value = Nobody;
            throwFrom = transform.position;
            throwTo = carrier.transform.position + (Vector3)(carrier.Facing.normalized * ThrowDistance);
            flight = 0f;
        }
    }

    [Rpc(SendTo.Everyone)]
    void LandRpc() => Sfx.PlayAt("thud", transform.position);
}
