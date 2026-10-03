using System.Collections.Generic;
using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Kennt die Checkpoints und Startplaetze der Strecke.
    ///
    /// Regel fuer eine gueltige Runde: Die Zwischen-Checkpoints muessen in aufsteigender
    /// Reihenfolge ihrer ID durchfahren werden, danach zaehlt die Ziellinie (ID 0).
    /// Checkpoints ausser der Reihe werden ignoriert. Abkuerzen und Rueckwaertsfahren ueber
    /// die Ziellinie bringt damit keine Runde.
    /// </summary>
    public class RaceTrackControl : MonoBehaviour
    {
        public static RaceTrackControl m_Main;

        [Tooltip("Alle Checkpoints der Strecke. Die Reihenfolge im Array ist egal, es zählt die ID. Leere Einträge werden übersprungen.")]
        public Checkpoint[] m_Checkpoints;
        public Transform[] m_StartPositions;

        [Tooltip("An: Eine Runde zählt nur, wenn alle Zwischen-Checkpoints in der richtigen Reihenfolge durchfahren wurden. Aus: Jede Zielüberquerung zählt.")]
        public bool m_CheckpointsErzwingen = true;

        private int[] m_ZwischenIds;

        /// <summary>IDs der Zwischen-Checkpoints in der Reihenfolge, in der sie durchfahren werden muessen.</summary>
        public int[] ZwischenIds
        {
            get
            {
                if (m_ZwischenIds == null) CheckpointsNeuEinlesen();
                return m_ZwischenIds;
            }
        }

        /// <summary>Anzahl der Zwischen-Checkpoints, die fuer eine gueltige Runde noetig sind.</summary>
        public int BenoetigteCheckpoints => m_CheckpointsErzwingen ? ZwischenIds.Length : 0;

        /// <summary>Nach jeder Aenderung an m_Checkpoints aufrufen (z. B. vom Streckengenerator).</summary>
        public void CheckpointsNeuEinlesen()
        {
            var ids = new List<int>();
            if (m_Checkpoints != null)
            {
                foreach (Checkpoint checkpoint in m_Checkpoints)
                {
                    if (checkpoint != null && !checkpoint.IstZiel && !ids.Contains(checkpoint.m_ID)) ids.Add(checkpoint.m_ID);
                }
            }
            ids.Sort();
            m_ZwischenIds = ids.ToArray();
        }

        private void Awake()
        {
            m_Main = this;
            CheckpointsNeuEinlesen();
        }
    }
}
