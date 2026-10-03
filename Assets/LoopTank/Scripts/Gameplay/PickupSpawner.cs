using UnityEngine;

namespace TopDownRace
{
    public class PickupSpawner : MonoBehaviour
    {
        [Tooltip("Das Prefab des PickUp-Objekts, das gespawnt werden soll.")]
        public GameObject m_PickupPrefab;

        // Referenz auf das aktuell gespawnte Pickup-Objekt
        private GameObject m_SpawnedPickup;

        private void Start()
        {
            // Abonnieren des OnRoundEnd-Events, um das Pickup neu zu spawnen
            if (GameControl.m_Current != null)
            {
                GameControl.m_Current.OnRoundEnd += SpawnPickup;
            }
            else
            {
                Debug.LogError("GameControl-Instanz nicht gefunden. PickupSpawner kann keine Rundenende-Events abonnieren.");
            }

            // Spawne das Pickup beim Start der Szene
            SpawnPickup();
        }

        private void OnDestroy()
        {
            // Das Event abbestellen, wenn das Objekt zerstört wird, um Fehler zu vermeiden
            if (GameControl.m_Current != null)
            {
                GameControl.m_Current.OnRoundEnd -= SpawnPickup;
            }
        }

        /// <summary>
        /// Spawnt das PickUp-Objekt und passt seine Rendering-Reihenfolge an.
        /// </summary>
        public void SpawnPickup()
        {
            // Zerstöre das alte Pickup, falls vorhanden
            if (m_SpawnedPickup != null)
            {
                Destroy(m_SpawnedPickup);
                m_SpawnedPickup = null;
            }

            if (m_PickupPrefab != null)
            {
                // Instanziiere das Prefab an der Position des Spawners
                m_SpawnedPickup = Instantiate(m_PickupPrefab, transform.position, Quaternion.identity, transform);

                // Suche nach dem SpriteRenderer des gespawnten Pickups
                SpriteRenderer pickupRenderer = m_SpawnedPickup.GetComponent<SpriteRenderer>();
                SpriteRenderer spawnerRenderer = GetComponent<SpriteRenderer>();

                if (pickupRenderer != null && spawnerRenderer != null)
                {
                    // Setze den Sorting Layer und die Order in Layer, damit das Pickup über dem Spawner gerendert wird
                    pickupRenderer.sortingLayerID = spawnerRenderer.sortingLayerID;
                    pickupRenderer.sortingOrder = spawnerRenderer.sortingOrder + 1;
                }
                else if (pickupRenderer != null)
                {
                    // Fallback, wenn der Spawner keinen SpriteRenderer hat
                    pickupRenderer.sortingOrder = 100;
                }
            }
            else
            {
                Debug.LogError("Pickup-Prefab ist nicht zugewiesen!", this);
            }
        }

        /// <summary>
        /// Zerstört das aktuell gespawnte Pickup-Objekt.
        /// Wird vom Pickup selbst aufgerufen, wenn es aufgesammelt wird.
        /// </summary>
        public void ClearSpawnedPickup()
        {
            if (m_SpawnedPickup != null)
            {
                Destroy(m_SpawnedPickup);
                m_SpawnedPickup = null;
            }
        }
    }
}