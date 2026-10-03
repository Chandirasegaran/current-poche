using UnityEngine;

// The crumbling edge of a ledge. Step (or get blown) onto it and you slide
// down and have to start that ledge again.
public class Cliff : MonoBehaviour
{
    [SerializeField] Transform climbBackTo;

    void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController>();
        if (player != null && player.IsOwner)
            player.SendBack(climbBackTo.position,
                "The wind took you off the ledge!\nKeep to the uphill side and lean into the gusts.");
    }
}
