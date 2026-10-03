using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Ein Tor auf der Strecke. ID 0 ist die Ziellinie, alle anderen sind Zwischen-Checkpoints,
    /// die in aufsteigender Reihenfolge durchfahren werden muessen (siehe RaceTrackControl).
    /// Braucht einen Trigger-Collider (2D oder 3D), der die ganze Streckenbreite abdeckt.
    /// </summary>
    public class Checkpoint : MonoBehaviour
    {
        public int m_ID;
        [HideInInspector]
        public bool isPassed;

        public bool isFinishLine;

        public bool IstZiel => isFinishLine || m_ID == 0;

        void Start()
        {
            isPassed = false;
        }

        // 2D- und 3D-Physik melden sich ueber verschiedene Callbacks, die Logik ist dieselbe.
        private void OnTriggerEnter2D(Collider2D collision) => Durchfahren(collision.gameObject);
        private void OnTriggerEnter(Collider collision) => Durchfahren(collision.gameObject);

        private void Durchfahren(GameObject anderes)
        {
            // Jeder Client wertet Checkpoints nur fuer den eigenen Panzer aus.
            PlayerCar panzer = anderes.GetComponentInParent<PlayerCar>();
            if (panzer == null || !panzer.IsOwner || !panzer.m_Control) return;
            if (GameControl.m_Current == null || !GameControl.m_Current.m_StartRace) return;
            if (RaceTrackControl.m_Main == null) return;

            if (!IstZiel)
            {
                panzer.CheckpointPassiert(m_ID);
                return;
            }

            // Ziellinie: zaehlt nur, wenn vorher alle Zwischen-Checkpoints an der Reihe waren.
            if (!panzer.DarfRundeBeenden()) return;

            float lapTime = GameControl.m_Current.GetCurrentLapTime();
            if (GhostManager.Instance != null) GhostManager.Instance.OnLapFinished(lapTime);

            GameControl.m_Current.m_FinishedLaps++;
            panzer.RundeBeendet(GameControl.m_Current.m_FinishedLaps);
            Debug.Log("Checkpoint: Ziellinie überquert. Runde: " + GameControl.m_Current.m_FinishedLaps);

            if (!GameControl.m_Current.PlayerLapEndCheck())
            {
                GameControl.m_Current.StartLapTimer();
                if (GhostManager.Instance != null) GhostManager.Instance.OnLapStarted();
            }
        }
    }
}
