using UnityEngine;

// Keeps the camera on this machine's own player. On the title screen, with
// nobody to follow, it drifts slowly along the street instead.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] float smoothTime = 0.15f;
    [SerializeField] Vector2 offset = new(0f, 0.6f);
    [SerializeField] Vector2 titleCentre = new(-8f, 3.5f);
    [SerializeField] float titleDrift = 14f;

    Vector3 velocity;

    void LateUpdate()
    {
        Vector3 target;
        float smoothing = smoothTime;

        var player = PlayerController.Local;
        if (player != null)
        {
            target = player.transform.position + (Vector3)offset;
        }
        else
        {
            target = titleCentre + Vector2.right * (Mathf.Sin(Time.time * 0.08f) * titleDrift);
            smoothing = 1.2f;
        }

        target.z = transform.position.z;
        transform.position = Vector3.SmoothDamp(transform.position, target, ref velocity, smoothing);
    }
}
