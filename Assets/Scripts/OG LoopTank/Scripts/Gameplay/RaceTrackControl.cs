using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TopDownRace
{
    public class RaceTrackControl : MonoBehaviour
    {
        public static RaceTrackControl m_Main;

        public Checkpoint[] m_Checkpoints;
        public Transform[] m_StartPositions;

        private void Awake()
        {
            m_Main = this;
        }
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}