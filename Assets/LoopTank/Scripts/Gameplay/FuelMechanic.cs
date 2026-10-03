using UnityEngine;
using TMPro;

namespace TopDownRace
{
    public class FuelMechanic : MonoBehaviour
    {
        // Singleton-Muster, um von anderen Skripten leicht darauf zugreifen zu können
        public static FuelMechanic Instance { get; private set; }

        [Header("Fuel Settings")]
        [Tooltip("Die maximale Menge an Kraftstoff.")]
        [SerializeField]
        private float m_MaxFuel = 100f;

        [Tooltip("Die Rate, mit der der Kraftstoff pro Sekunde abnimmt, wenn der Boost aktiv ist.")]
        [SerializeField]
        private float m_FuelDrainRate = 10f;

        [Tooltip("Die Rate, mit der der Kraftstoff pro Sekunde aufgefüllt wird, wenn der Boost inaktiv ist.")]
        [SerializeField]
        private float m_FuelRefillRate = 5f;

        // Der aktuelle Kraftstoffwert, der von anderen Skripten ausgelesen werden kann
        public float CurrentFuel { get; private set; }

        [Header("UI Settings")]
        [Tooltip("Die TextMeshPro-Komponente, die den aktuellen Kraftstoffwert anzeigt.")]
        [SerializeField]
        private TMP_Text m_FuelTextUI;

        private float m_LastDisplayedFuel = -1f; // Zur Optimierung der UI-Aktualisierung

        void Awake()
        {
            // Implementierung des Singletons
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            }
            else
            {
                Instance = this;
            }
        }

        void Start()
        {
            // Initialisiert den Kraftstoff beim Start
            CurrentFuel = m_MaxFuel;
            UpdateFuelUI();

            // Die UI-Anzeige zu Beginn deaktivieren
            SetFuelUIActive(false);
        }

        void Update()
        {
            // Aktualisiert die UI, wenn sich der Wert ändert
            UpdateFuelUI();
        }

        // Methode zum Abziehen des Kraftstoffs
        public void DrainFuel()
        {
            CurrentFuel -= m_FuelDrainRate * Time.deltaTime;
            // Sicherstellen, dass der Kraftstoff nicht unter null fällt
            CurrentFuel = Mathf.Max(0, CurrentFuel);
        }

        // Methode zum Auffüllen des Kraftstoffs
        public void RefillFuel()
        {
            CurrentFuel += m_FuelRefillRate * Time.deltaTime;
            // Sicherstellen, dass der Kraftstoff das Maximum nicht überschreitet
            CurrentFuel = Mathf.Min(m_MaxFuel, CurrentFuel);
        }

        public void AddFuel(float amount)
        {
            CurrentFuel += amount;
            CurrentFuel = Mathf.Min(m_MaxFuel, CurrentFuel);
            UpdateFuelUI(); // Stellt sicher, dass die UI sofort aktualisiert wird
        }

        // NEUE METHODE: Steuert die Sichtbarkeit der UI
        public void SetFuelUIActive(bool isActive)
        {
            if (m_FuelTextUI != null && m_FuelTextUI.gameObject != null)
            {
                m_FuelTextUI.gameObject.SetActive(isActive);
            }
        }

        // Methode zur Aktualisierung der UI
        private void UpdateFuelUI()
        {
            if (m_FuelTextUI != null)
            {
                // Aktualisiere den Text nur, wenn sich der ganzzahlige Wert ändert
                int currentFuelInt = Mathf.FloorToInt(CurrentFuel);
                if (currentFuelInt != m_LastDisplayedFuel)
                {
                    m_FuelTextUI.text = $"Fuel: {currentFuelInt}";
                    m_LastDisplayedFuel = currentFuelInt;
                }
            }
            else
            {
                // Warnung, falls die UI-Referenz im Inspector nicht gesetzt ist
                Debug.LogWarning("Die Fuel-UI-Referenz ist nicht im Inspector von FuelMechanic zugewiesen!");
            }
        }
    }
}