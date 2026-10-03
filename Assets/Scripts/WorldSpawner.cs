using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

// When a game starts, the server uses this to bring the town to life:
// it scatters the minminis and lets Battery the dog out.
public class WorldSpawner : MonoBehaviour
{
    public static WorldSpawner Instance { get; private set; }

    [SerializeField] NetworkObject minminiPrefab;
    [SerializeField] NetworkObject dogPrefab;
    [SerializeField] Transform minminiSpots;
    [SerializeField] Transform dogSpot;
    [SerializeField] int minminiCount = 44;
    [SerializeField] NetworkObject bandicootPrefab;
    [SerializeField] Transform bandicootSpots;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        NetworkManager.Singleton.OnServerStarted += Populate;
    }

    void Populate()
    {
        var spots = new List<Transform>();
        foreach (Transform spot in minminiSpots) spots.Add(spot);

        for (int i = 0; i < minminiCount && spots.Count > 0; i++)
        {
            int pick = Random.Range(0, spots.Count);
            Instantiate(minminiPrefab, spots[pick].position, Quaternion.identity).Spawn();
            spots.RemoveAt(pick);
        }

        Instantiate(dogPrefab, dogSpot.position, Quaternion.identity).Spawn();

        foreach (Transform lair in bandicootSpots)
            Instantiate(bandicootPrefab, lair.position, Quaternion.identity).Spawn();
    }

    // Moves a minmini that has fed a lamp to a spot away from every player.
    public void Recycle(Minmini minmini)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            var spot = minminiSpots.GetChild(Random.Range(0, minminiSpots.childCount)).position;
            bool farFromEveryone = true;
            foreach (var player in PlayerController.All)
                if (Vector2.Distance(spot, player.transform.position) < 12f) farFromEveryone = false;

            if (farFromEveryone || attempt == 19)
            {
                minmini.Respawn(spot);
                return;
            }
        }
    }
}
