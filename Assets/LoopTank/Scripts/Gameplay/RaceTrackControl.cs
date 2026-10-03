using UnityEngine;

namespace TopDownRace
{
    public class RaceTrackControl : MonoBehaviour
    {
        public static RaceTrackControl m_Main;

        public Checkpoint[] m_Checkpoints;
        public Transform[] m_StartPositions;

        [Tooltip("An: Eine Runde zählt nur, wenn alle Zwischen-Checkpoints passiert wurden. Verhindert, dass man über der Ziellinie hin- und herfährt.")]
        public bool m_CheckpointsErzwingen = true;

        /// <summary>Anzahl der Zwischen-Checkpoints, die fuer eine gueltige Runde noetig sind.</summary>
        public int BenoetigteCheckpoints
        {
            get
            {
                if (!m_CheckpointsErzwingen || m_Checkpoints == null) return 0;

                int anzahl = 0;
                foreach (Checkpoint checkpoint in m_Checkpoints)
                {
                    if (checkpoint != null && checkpoint.m_ID != 0) anzahl++;
                }
                return anzahl;
            }
        }

        private void Awake()
        {
            m_Main = this;
        }
    }
}
