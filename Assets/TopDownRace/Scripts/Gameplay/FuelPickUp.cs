using UnityEngine;
using System.Collections; // Füge dies hinzu, um Coroutinen zu verwenden

namespace TopDownRace
{
    public class FuelPickup : MonoBehaviour
    {
        [Header("Fuel Pickup Settings")]
        [Tooltip("Die Menge an Kraftstoff, die dem Spieler hinzugefügt wird.")]
        [SerializeField]
        private float m_FuelAmount = 25f;

        [Tooltip("Der Sound, der abgespielt wird, wenn der Spieler das Pickup aufnimmt.")]
        [SerializeField]
        private AudioClip m_PickupSound;

        private AudioSource m_AudioSource;
        private SpriteRenderer m_SpriteRenderer;

        // Eine Flagge, um zu verhindern, dass das Pickup mehrfach aufgesammelt wird
        private bool m_IsPickedUp = false;

        void Start()
        {
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
            }
            m_SpriteRenderer = GetComponent<SpriteRenderer>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Prüfe, ob der kollidierende Collider zum Spieler gehört und ob das Pickup noch nicht aufgesammelt wurde
            if (other.CompareTag("Player") && !m_IsPickedUp)
            {
                m_IsPickedUp = true; // Setze die Flagge, um weitere Trigger zu ignorieren

                // Rufe die Methode im FuelMechanic-Skript auf, um den Kraftstoff aufzufüllen
                if (FuelMechanic.Instance != null)
                {
                    FuelMechanic.Instance.AddFuel(m_FuelAmount);
                }

                // Spielt den Soundeffekt ab (falls vorhanden)
                if (m_PickupSound != null && m_AudioSource != null)
                {
                    m_AudioSource.PlayOneShot(m_PickupSound);
                }

                // Versteckt das Sprite, damit es nicht mehr sichtbar ist
                if (m_SpriteRenderer != null)
                {
                    m_SpriteRenderer.enabled = false;
                }

                // Starte eine Coroutine, um das Objekt nach dem Abspielen des Sounds zu zerstören
                StartCoroutine(DestroyAfterSound());
            }
        }

        private IEnumerator DestroyAfterSound()
        {
            // Warte, bis der Sound zu Ende gespielt ist
            float delay = (m_PickupSound != null) ? m_PickupSound.length : 0f;
            yield return new WaitForSeconds(delay);

            // Finde den Spawner und sage ihm, dass der Platz wieder frei ist
            PickupSpawner spawner = transform.parent.GetComponent<PickupSpawner>();
            if (spawner != null)
            {
                spawner.ClearSpawnedPickup();
            }
        }
    }
}