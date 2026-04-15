using UnityEngine;

public class PickUpManager : MonoBehaviour
{
    // Die OnTriggerEnter2D-Methode wird automatisch von Unity aufgerufen,
    // wenn ein Objekt mit einem Collider (und IsTrigger aktiviert)
    // von einem anderen Objekt (dem Spieler) berührt wird.
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Überprüfe, ob das kollidierende Objekt das "PlayerCar" ist.
        // Dafür nutzen wir einen Tag. Stelle sicher, dass dein Player-Objekt
        // in Unity den Tag "Player" hat.
        if (other.CompareTag("Player"))
        {
            Debug.Log("Pickup kollidiert mit Spieler! Pickup wird zerstört.");

            // Zerstöre das Game-Objekt, an dem dieses Skript hängt (also das Pickup selbst).
            Destroy(gameObject);
        }
    }
}