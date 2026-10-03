using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Treibstoff-Kanister auf der Strecke. Faehrt der eigene Panzer hinein, fuellt er den Tank
    /// (FuelMechanic) auf und verschwindet fuer eine Weile.
    ///
    /// Kanister sind lokal: Jeder Spieler hat seine eigenen und sammelt sie nur mit dem eigenen
    /// Panzer ein. Es gibt also keinen Streit darum und nichts, was ueber das Netz laufen muss.
    /// Braucht einen Trigger-Collider auf demselben Objekt und ein Kind namens "Optik".
    /// </summary>
    public class TreibstoffKanister : MonoBehaviour
    {
        [Tooltip("Treibstoff, den der Kanister auffüllt.")]
        public float m_Menge = 35f;
        [Tooltip("Sekunden, bis der Kanister wieder erscheint.")]
        public float m_RespawnSekunden = 14f;
        [Tooltip("Drehung in Grad pro Sekunde.")]
        public float m_DrehTempo = 80f;
        [Tooltip("Wie weit der Kanister auf und ab schwebt, als Anteil seiner Höhe über dem Boden.")]
        public float m_Schweben = 0.12f;

        private Transform m_Optik;
        private Vector3 m_OptikBasis;
        private bool m_Weg;
        private float m_ZurueckUm;
        private float m_Erscheinen = 1f;   // 0..1, fuer das Aufploppen
        private float m_Phase;

        private void Awake()
        {
            m_Optik = transform.Find("Optik");
            if (m_Optik != null) m_OptikBasis = m_Optik.localPosition;
            // Nicht alle Kanister im Gleichtakt
            m_Phase = (transform.position.x * 0.37f + transform.position.z * 0.53f) % 6.28f;
        }

        private void Update()
        {
            if (m_Weg)
            {
                if (Time.time < m_ZurueckUm) return;
                m_Weg = false;
                m_Erscheinen = 0f;
                if (m_Optik != null) m_Optik.gameObject.SetActive(true);
            }

            if (m_Optik == null) return;

            m_Erscheinen = Mathf.MoveTowards(m_Erscheinen, 1f, Time.deltaTime * 3f);
            float groesse = Mathf.SmoothStep(0f, 1f, m_Erscheinen);
            m_Optik.localScale = Vector3.one * groesse;
            m_Optik.Rotate(0f, m_DrehTempo * Time.deltaTime, 0f, Space.World);
            m_Optik.localPosition = m_OptikBasis + Vector3.up * (Mathf.Sin(Time.time * 2f + m_Phase) * m_Schweben * m_OptikBasis.y);
        }

        // Auch waehrend man drinsteht pruefen: Wer mit vollem Tank hineinfaehrt und dann boostet,
        // bekommt den Kanister, sobald wieder Platz im Tank ist.
        private void OnTriggerEnter(Collider anderes) => Pruefen(anderes);
        private void OnTriggerStay(Collider anderes) => Pruefen(anderes);

        private void Pruefen(Collider anderes)
        {
            if (m_Weg || m_Erscheinen < 1f) return;

            PlayerCar panzer = anderes.GetComponentInParent<PlayerCar>();
            if (panzer == null || !panzer.IsOwner || !panzer.Lebt.Value) return;

            FuelMechanic tank = FuelMechanic.Instance;
            if (tank == null || tank.CurrentFuel >= tank.MaxFuel - 0.5f) return;   // voller Tank: liegen lassen

            tank.AddFuel(m_Menge);
            if (RennHud.Instanz != null) RennHud.Instanz.Meldung("+" + Mathf.RoundToInt(m_Menge) + " Treibstoff");

            m_Weg = true;
            m_ZurueckUm = Time.time + m_RespawnSekunden;
            if (m_Optik != null) m_Optik.gameObject.SetActive(false);
        }
    }
}
