using System.Collections.Generic;
using UnityEngine;

// Anything you can walk up to and press E on: townsfolk, signs, the dog.
// Each line is written as "Speaker|What they say".
public class Interactable : MonoBehaviour
{
    public string verb = "Talk";
    [TextArea] public string[] lines;

    static readonly List<Interactable> all = new();

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public static Interactable Closest(Vector3 position, float range)
    {
        Interactable best = null;
        float bestDistance = range;
        foreach (var candidate in all)
        {
            float distance = Vector2.Distance(position, candidate.transform.position);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
            }
        }
        return best;
    }
}
