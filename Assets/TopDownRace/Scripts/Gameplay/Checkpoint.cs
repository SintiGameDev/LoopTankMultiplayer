using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TopDownRace
{
    public class Checkpoint : MonoBehaviour
    {

        public int m_ID;
        [HideInInspector]
        public bool isPassed;

        public bool isFinishLine;
        // Start is called before the first frame update
        void Start()
        {
            isPassed = false;

        }

        // Update is called once per frame
        void Update()
        {

        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.tag == "Player")
            {
               Debug.Log("Checkpoint: Player has passed checkpoint " + m_ID + "and Current Checkpoint" + PlayerCar.m_Current.m_CurrentCheckpoint );
                //PlayerCar.m_Current.m_CurrentCheckpoint = PlayerCar.m_Current.m_CurrentCheckpoint - 1;
                 if (PlayerCar.m_Current.m_CurrentCheckpoint == m_ID) //TODO: Hier in den Iff komme ich nicht rein
                //if(true == true) //TODO: Hier in den Iff komme ich nicht rein
                Debug.Log("Checkpoint: Player has passed checkpoint " + m_ID + " and Current Checkpoint " + PlayerCar.m_Current.m_CurrentCheckpoint);
                {
                    if (m_ID == 0)
                    {
                        // --- GHOST: Runde beenden ---
                        float lapTime = GameControl.m_Current.GetCurrentLapTime(); // Passe dies ggf. an!
                        GhostManager.Instance.OnLapFinished(lapTime);

                        GameControl.m_Current.m_FinishedLaps++;
                        Debug.Log("Checkpoint: Player has passed the finish line. Current Lap: " + GameControl.m_Current.m_FinishedLaps);
                        if (!GameControl.m_Current.PlayerLapEndCheck())
                        {
                            //Debug.Log("Checkpoint: Player has finished the lap, resetting checkpoints.");
                            PlayerCar.m_Current.m_CurrentCheckpoint = 1;

                            // --- GHOST: Neue Runde starten ---
                            GhostManager.Instance.OnLapStarted();
                        }
                    }
                    else
                    {
                        PlayerCar.m_Current.m_CurrentCheckpoint = m_ID + 1;
                        if (PlayerCar.m_Current.m_CurrentCheckpoint > RaceTrackControl.m_Main.m_Checkpoints.Length - 1)
                        {
                            PlayerCar.m_Current.m_CurrentCheckpoint = 0;
                        }
                    }
                }
            }
            else if (collision.gameObject.tag == "Rival")
            {
                collision.gameObject.GetComponent<Rivals>().Checkpoint(m_ID);
            }
        }
    }
}