using UnityEngine;
using Unity.Netcode;

public class SpawnManager : NetworkBehaviour
{
    [Header("Spawn Punkte")]
    [SerializeField] private Transform[] spawnPunkte;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback += SpielerSpawnen;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback -= SpielerSpawnen;
    }

    private void SpielerSpawnen(ulong clientId)
    {
        if (spawnPunkte == null || spawnPunkte.Length == 0) return;

        // Spawn-Punkt basierend auf Client-Anzahl auswaehlen
        int index = (int)(clientId % (ulong)spawnPunkte.Length);
        Transform punkt = spawnPunkte[index];

        // Spieler-Objekt an Spawn-Punkt verschieben
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null)
            {
                client.PlayerObject.transform.position = punkt.position;
                client.PlayerObject.transform.rotation = punkt.rotation;
            }
        }
    }
}
