using System.Collections;
using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Fuel-Pickups sind lokal: Jeder Spieler hat seine eigenen und sammelt sie nur mit dem
    /// eigenen Panzer ein. Fremde Panzer fahren einfach hindurch.
    /// </summary>
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

        // Verhindert, dass das Pickup mehrfach aufgesammelt wird
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
            if (m_IsPickedUp || !other.CompareTag("Player")) return;

            PlayerCar panzer = other.GetComponentInParent<PlayerCar>();
            if (panzer == null || !panzer.IsOwner) return;

            m_IsPickedUp = true;

            if (FuelMechanic.Instance != null)
            {
                FuelMechanic.Instance.AddFuel(m_FuelAmount);
            }

            if (m_PickupSound != null && m_AudioSource != null)
            {
                m_AudioSource.PlayOneShot(m_PickupSound);
            }

            if (m_SpriteRenderer != null)
            {
                m_SpriteRenderer.enabled = false;
            }

            StartCoroutine(DestroyAfterSound());
        }

        private IEnumerator DestroyAfterSound()
        {
            float delay = (m_PickupSound != null) ? m_PickupSound.length : 0f;
            yield return new WaitForSeconds(delay);

            // Dem Spawner sagen, dass der Platz wieder frei ist
            PickupSpawner spawner = transform.parent != null ? transform.parent.GetComponent<PickupSpawner>() : null;
            if (spawner != null)
            {
                spawner.ClearSpawnedPickup();
            }
        }
    }
}
