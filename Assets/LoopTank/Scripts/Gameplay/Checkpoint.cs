using UnityEngine;

namespace TopDownRace
{
    public class Checkpoint : MonoBehaviour
    {
        public int m_ID;
        [HideInInspector]
        public bool isPassed;

        public bool isFinishLine;

        void Start()
        {
            isPassed = false;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (!collision.CompareTag("Player")) return;

            // Jeder Client wertet Checkpoints nur fuer den eigenen Panzer aus.
            PlayerCar panzer = collision.GetComponentInParent<PlayerCar>();
            if (panzer == null || !panzer.IsOwner || !panzer.m_Control) return;
            if (GameControl.m_Current == null || !GameControl.m_Current.m_StartRace) return;

            if (m_ID != 0)
            {
                panzer.CheckpointPassiert(m_ID);
                return;
            }

            // Ziellinie: zaehlt nur, wenn vorher die Zwischen-Checkpoints passiert wurden.
            if (!panzer.DarfRundeBeenden(RaceTrackControl.m_Main.BenoetigteCheckpoints)) return;

            float lapTime = GameControl.m_Current.GetCurrentLapTime();
            GhostManager.Instance.OnLapFinished(lapTime);

            GameControl.m_Current.m_FinishedLaps++;
            panzer.RundeBeendet(GameControl.m_Current.m_FinishedLaps);
            Debug.Log("Checkpoint: Ziellinie überquert. Runde: " + GameControl.m_Current.m_FinishedLaps);

            if (!GameControl.m_Current.PlayerLapEndCheck())
            {
                GameControl.m_Current.StartLapTimer();
                GhostManager.Instance.OnLapStarted();
            }
        }
    }
}
