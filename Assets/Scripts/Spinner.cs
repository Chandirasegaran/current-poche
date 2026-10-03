using UnityEngine;

// Turns something round and round: the windmill blades, once their brake is off.
public class Spinner : MonoBehaviour
{
    [SerializeField] float degreesPerSecond = -140f;

    void Update() => transform.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
}
