using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A streetlight. Dead ones wake up when enough minminis fly into them.
// The server owns the count; every machine just shows it.
// The pump-set in the fields works the same way, it just needs more of them.
public class StreetLight : NetworkBehaviour
{
    // The town's streetlights (what the counter on screen counts)...
    public static readonly List<StreetLight> All = new();
    // ...and everything minminis can power, which includes the pump.
    public static readonly List<StreetLight> Feedable = new();

    // Server: while locked, minminis ignore this (the pump before its belt is fitted).
    public bool Locked { get; set; }

    [SerializeField] int needed = 3;
    [SerializeField] bool isStreetlight = true;
    [SerializeField] bool startsLit;
    [SerializeField] SpriteRenderer pole;
    [SerializeField] Sprite litSprite, deadSprite;
    [SerializeField] Light2D pool;
    [SerializeField] SpriteRenderer glow;

    readonly NetworkVariable<int> charge = new();
    int reserved; // minminis already on their way here (server only)
    float poolIntensity;
    Color glowColour;

    public bool IsLit => IsSpawned ? charge.Value >= needed : startsLit;
    public int Charge => IsSpawned ? charge.Value : 0;
    public int Needed => needed;
    public Vector3 BulbPosition => glow.transform.position;

    void Awake()
    {
        poolIntensity = pool.intensity;
        glowColour = glow.color;
    }

    void OnEnable()
    {
        Feedable.Add(this);
        if (isStreetlight) All.Add(this);
    }

    void OnDisable()
    {
        Feedable.Remove(this);
        All.Remove(this);
    }
    void Start() => Refresh();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            reserved = 0;
            charge.Value = startsLit ? needed : 0;
        }
        charge.OnValueChanged += OnChargeChanged;
        Refresh();
    }

    public override void OnNetworkDespawn()
    {
        charge.OnValueChanged -= OnChargeChanged;
        Refresh();
    }

    // Server: a minmini asks whether this lamp still has room for it.
    public bool TryReserve()
    {
        if (Locked || charge.Value + reserved >= needed) return false;
        reserved++;
        return true;
    }

    public void CancelReserve() => reserved--;

    // Server: used when loading a saved game.
    public void ForceLit() => charge.Value = needed;

    public void Deliver()
    {
        reserved--;
        charge.Value++;
    }

    void OnChargeChanged(int before, int now)
    {
        Refresh();
        if (now > before) Sfx.PlayAt("minmini", transform.position);
        if (before < needed && now >= needed)
        {
            if (IsServer && Quests.Instance != null) Quests.Instance.Save();
            Sfx.PlayAt("lamp", transform.position);
            StartCoroutine(Flash());
        }
    }

    void Refresh()
    {
        bool lit = IsLit;
        pole.sprite = lit ? litSprite : deadSprite;
        pool.enabled = lit;
        pool.intensity = poolIntensity;

        // A half-fed lamp glows faintly, so you can see your progress.
        float fill = lit ? 1f : IsSpawned ? 0.45f * charge.Value / needed : 0f;
        glow.color = new Color(glowColour.r, glowColour.g, glowColour.b, glowColour.a * fill);
    }

    IEnumerator Flash()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.7f)
        {
            pool.intensity = Mathf.Lerp(poolIntensity * 3.5f, poolIntensity, t);
            yield return null;
        }
        pool.intensity = poolIntensity;
    }
}
