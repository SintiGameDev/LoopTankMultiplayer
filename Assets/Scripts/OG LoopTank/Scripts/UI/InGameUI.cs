using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;


namespace TopDownRace
{
    public class InGameUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField]
        private Text m_LevelRounds;
        [SerializeField]
        private Text m_FinishedRounds;
        public Text m_Timer;
        public Text m_Position;

        [Header("Audio")]
        [Tooltip("Der Sound für den Countdown bei 3")]
        public AudioClip m_CountdownSound3;
        [Tooltip("Der Sound für den Countdown bei 2")]
        public AudioClip m_CountdownSound2;
        [Tooltip("Der Sound für den Countdown bei 1")]
        public AudioClip m_CountdownSound1;
        [Tooltip("Der Sound, der bei 'GO!' abgespielt wird.")]
        public AudioClip m_GoSound;

        private AudioSource m_CountdownAudioSource;
        private int m_lastTime = -1; // Standardwert -1, um den ersten Wert (3) zu erkennen
        private bool m_GoPlayed = false;

        public static InGameUI Current;


        void Awake()
        {
            Current = this;

            m_CountdownAudioSource = GetComponent<AudioSource>();
            if (m_CountdownAudioSource == null)
            {
                m_CountdownAudioSource = gameObject.AddComponent<AudioSource>();
            }
            m_CountdownAudioSource.playOnAwake = false;
        }

        // Die Start-Methode ist nicht mehr notwendig, da m_lastTime mit dem Standardwert -1 startet.
        // Falls du sie für andere Zwecke benötigst, kannst du sie leer lassen.
        void Start()
        {
        }

        void Update()
        {
            if (GameControl.m_Current == null) return;

            // Die Logik, wann der "Go!"-Text erscheinen soll
            if (GameControl.m_Current.m_StartRace && !m_GoPlayed)
            {
                m_Timer.text = "GO!";
                if (m_GoSound != null)
                {
                    m_CountdownAudioSource.PlayOneShot(m_GoSound);
                }
                m_GoPlayed = true;
                StartCoroutine(DeactivateTimerAfterDelay(1.0f));
            }
            // Die eigentliche Countdown-Logik
            else if (!GameControl.m_Current.m_StartRace)
            {
                int currentTime = Mathf.FloorToInt(GameControl.m_Current.m_StartTimer);

                if (currentTime != m_lastTime)
                {
                    // Update der Timer-Textanzeige
                    m_Timer.text = currentTime.ToString();

                    switch (currentTime)
                    {
                        case 3:
                            if (m_CountdownSound3 != null) m_CountdownAudioSource.PlayOneShot(m_CountdownSound3);
                            break;
                        case 2:
                            if (m_CountdownSound2 != null) m_CountdownAudioSource.PlayOneShot(m_CountdownSound2);
                            break;
                        case 1:
                            if (m_CountdownSound1 != null) m_CountdownAudioSource.PlayOneShot(m_CountdownSound1);
                            break;
                    }
                    m_lastTime = currentTime;
                }
            }


            m_FinishedRounds.text = GameControl.m_Current.m_FinishedLaps.ToString();
            m_LevelRounds.text = GameControl.m_Current.m_levelRounds.ToString();
            m_Position.text = (GameControl.m_Current.m_PlayerPosition + 1).ToString() + "th / 4";
        }

        private IEnumerator DeactivateTimerAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            m_Timer.gameObject.SetActive(false);
        }

        public void BtnPause()
        {
        }
    }
}