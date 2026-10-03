using UnityEngine;
using UnityEngine.Rendering.Universal;

// A glimpse of Minnal: it flickers in place for a moment, then bolts away.
// Quests switches this object on at the end of a chapter.
public class MinnalCameo : MonoBehaviour
{
    [SerializeField] Light2D glow;
    [SerializeField] Vector3 escape = new(-6f, 34f, 0f); // which way it bolts

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
            transform.position += escape * Time.deltaTime;

        if (age > 6f) gameObject.SetActive(false);
    }
}
