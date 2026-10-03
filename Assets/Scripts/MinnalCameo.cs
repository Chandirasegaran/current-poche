using UnityEngine;
using UnityEngine.Rendering.Universal;

// Minnal's first appearance: it flickers on the pump-house roof for a moment,
// then bolts away north. Quests switches this object on when the pump starts.
public class MinnalCameo : MonoBehaviour
{
    [SerializeField] Light2D glow;

    Vector3 perch;
    float age;

    void Awake() => perch = transform.position;

    void OnEnable()
    {
        age = 0f;
        transform.position = perch;
        Sfx.PlayAt("squeak", perch);
    }

    void Update()
    {
        age += Time.deltaTime;
        glow.intensity = 2.2f + Mathf.PerlinNoise(Time.time * 18f, 0f) * 1.8f;

        if (age < 4f)
            transform.position = perch + new Vector3(Mathf.Sin(age * 30f) * 0.04f, Mathf.Abs(Mathf.Sin(age * 5f)) * 0.15f, 0f);
        else
            transform.position += new Vector3(-6f, 34f, 0f) * Time.deltaTime; // gone, towards the cinema

        if (age > 6f) gameObject.SetActive(false);
    }
}
