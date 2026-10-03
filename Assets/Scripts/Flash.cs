using UnityEngine;
using UnityEngine.Rendering.Universal;

// One enormous flash that fades away: Minnal going home. It works by turning
// the moonlight right up for a moment. Afterwards the night stays a little
// brighter than it was.
public class Flash : MonoBehaviour
{
    [SerializeField] Light2D moonlight;
    [SerializeField] float peak = 4f, seconds = 3f, afterwards = 0.7f;

    float age;

    void OnEnable() => age = 0f;

    void Update()
    {
        age += Time.deltaTime;
        moonlight.intensity = Mathf.Lerp(peak, afterwards, age / seconds);
        if (age > seconds) enabled = false;
    }
}
