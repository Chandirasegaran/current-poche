using UnityEngine;
using UnityEngine.Rendering.Universal;

// Makes a light waver like a candle flame.
[RequireComponent(typeof(Light2D))]
public class FlickerLight : MonoBehaviour
{
    [SerializeField] float amount = 0.25f;
    [SerializeField] float speed = 6f;

    Light2D flame;
    float baseIntensity;
    float seed;

    void Awake()
    {
        flame = GetComponent<Light2D>();
        baseIntensity = flame.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        float noise = Mathf.PerlinNoise(seed, Time.time * speed) - 0.5f;
        flame.intensity = baseIntensity * (1f + noise * 2f * amount);
    }
}
