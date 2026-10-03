using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// A streetlight. Dead ones wake up when enough minminis fly into them.
// The server owns the count; every machine just shows it.
public class StreetLight : NetworkBehaviour
{
    public const int Needed = 3;

    public static readonly List<StreetLight> All = new();

    [SerializeField] bool startsLit;
    [SerializeField] SpriteRenderer pole;
    [SerializeField] Sprite litSprite, deadSprite;
    [SerializeField] Light2D pool;
    [SerializeField] SpriteRenderer glow;

    readonly NetworkVariable<int> charge = new();
    int reserved; // minminis already on their way here (server only)
    float poolIntensity;
    Color glowColour;

    public bool IsLit => IsSpawned ? charge.Value >= Needed : startsLit;
    public Vector3 BulbPosition => glow.transform.position;

    void Awake()
    {
        poolIntensity = pool.intensity;
        glowColour = glow.color;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);
    void Start() => Refresh();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            reserved = 0;
            charge.Value = startsLit ? Needed : 0;
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
        if (charge.Value + reserved >= Needed) return false;
        reserved++;
        return true;
    }

    public void CancelReserve() => reserved--;

    public void Deliver()
    {
        reserved--;
        charge.Value++;
    }

    void OnChargeChanged(int before, int now)
    {
        Refresh();
        if (before < Needed && now >= Needed) StartCoroutine(Flash());
    }

    void Refresh()
    {
        bool lit = IsLit;
        pole.sprite = lit ? litSprite : deadSprite;
        pool.enabled = lit;
        pool.intensity = poolIntensity;

        // A half-fed lamp glows faintly, so you can see your progress.
        float fill = lit ? 1f : IsSpawned ? 0.45f * charge.Value / Needed : 0f;
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
