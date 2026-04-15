using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TopDownRace
{
    public class Rivals : MonoBehaviour
    {

        [HideInInspector]
        public Transform m_TargetDestination;
        [HideInInspector]
        public int m_WaypointsCounter;

        [HideInInspector]
        public int m_FinishedLaps;

        [HideInInspector]
        public bool m_Control = false;

        // Start is called before the first frame update
        void Start()
        {
            m_Control = true;
            m_WaypointsCounter = 1;
        }

        // Update is called once per frame
        void Update()
        {
            // Prüfe, ob RaceTrackControl und Checkpoints existieren
            if (RaceTrackControl.m_Main == null ||
                RaceTrackControl.m_Main.m_Checkpoints == null ||
                RaceTrackControl.m_Main.m_Checkpoints.Length == 0)
                return;

            // Prüfe, ob der Index gültig ist
            if (m_WaypointsCounter < 0 || m_WaypointsCounter >= RaceTrackControl.m_Main.m_Checkpoints.Length)
                return;

            // Prüfe, ob das Checkpoint-Objekt existiert
            if (RaceTrackControl.m_Main.m_Checkpoints[m_WaypointsCounter] == null)
                return;

            m_TargetDestination = RaceTrackControl.m_Main.m_Checkpoints[m_WaypointsCounter].transform;

            float distance = Vector2.Distance(m_TargetDestination.position, transform.position);

            // Restlicher Code wie gehabt...
            Vector3 movementDirection = m_TargetDestination.position - transform.position;
            movementDirection.z = 0;
            movementDirection.Normalize();

            if (GameControl.m_Current != null && GameControl.m_Current.m_StartRace)
            {
                if (m_Control)
                {
                    GetComponent<CarPhysics>().m_InputAccelerate = 1;

                    float delta = Vector3.SignedAngle(movementDirection, transform.right, Vector3.forward);
                    if (GetComponent<CarPhysics>().m_InputAccelerate < 0)
                    {
                        GetComponent<CarPhysics>().m_InputSteer = Mathf.Sign(delta);
                    }
                    else
                    {
                        GetComponent<CarPhysics>().m_InputSteer = -Mathf.Sign(delta);
                    }
                }
            }
        }

        public void Checkpoint(int num)
        {
            if (num == m_WaypointsCounter)
            {
                if (num == 0)
                {
                    m_FinishedLaps++;
                    GameControl.m_Current.RivalsLapEndCheck(this);
                }
                m_WaypointsCounter++;
                if (m_WaypointsCounter > RaceTrackControl.m_Main.m_Checkpoints.Length - 1)
                {
                    m_WaypointsCounter = 0;
                }

            }
        }
    }
}