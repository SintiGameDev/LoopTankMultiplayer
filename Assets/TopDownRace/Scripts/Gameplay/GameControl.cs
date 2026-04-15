using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System;

namespace TopDownRace
{
    public class GameControl : MonoBehaviour
    {
        // Singleton-Instanz, die von überall zugänglich ist
        public static GameControl m_Current;

        // NEUE ZEILE: Deklaration des OnRoundEnd-Events
        public event System.Action OnRoundEnd;

        // Dein ursprünglicher Code
        [Tooltip("Anzahl der Runden, die für den Sieg benötigt werden.")]
        public int m_levelRounds;

        [HideInInspector]
        public int m_FinishedLaps;

        [Tooltip("Prefab für das Spielerauto.")]
        public GameObject m_PlayerCarPrefab;

        [Tooltip("Prefab für die KI-Gegner.")]
        public GameObject m_RivalCarPrefab;

        [HideInInspector]
        public int m_PlayerPosition;

        [HideInInspector]
        public GameObject[] m_Cars;

        [HideInInspector]
        public bool m_LostRace;
        [HideInInspector]
        public bool m_WonRace;

        [HideInInspector]
        public bool m_StartRace;

        [HideInInspector]
        public int m_StartTimer;

        // Timer
        private Timer roundTimer;
        private float m_LapStartTime;

        // Timer-UI
        private TextMeshProUGUI m_TimerText;

        // NEU: Referenz auf die Kamera, die dem Spieler folgt
        [Tooltip("Das CameraFollow-Skript in der Szene.")]
        public CameraFollow m_CameraFollow;


        private void Awake()
        {
            // Sichere Singleton-Implementierung
            if (m_Current != null && m_Current != this)
            {
                Destroy(this.gameObject);
                return;
            }
            m_Current = this;
            DontDestroyOnLoad(this.gameObject);
        }

        // NEU: Diese Methode wird immer aufgerufen, wenn die Szene geladen wird
        // und ist der perfekte Ort, um alle Szene-spezifischen Referenzen zu finden.
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Initialisiere die Spielvariablen
            m_LostRace = false;
            m_WonRace = false;
            m_StartRace = false;
            m_FinishedLaps = 0;

            // Finde alle Szene-spezifischen Objekte neu
            FindSceneReferences();

            // Starte die Rennlogik
            StartCoroutine(Co_StartRace());
        }

        void FindSceneReferences()
        {
            // Finde das Timer-Text-Objekt in der Szene und weise es zu
            GameObject timerObject = GameObject.FindGameObjectWithTag("Timer");
            if (timerObject != null)
            {
                m_TimerText = timerObject.GetComponent<TextMeshProUGUI>();
                roundTimer = timerObject.GetComponent<Timer>();

                if (m_TimerText == null)
                {
                    Debug.LogError("Das Objekt mit dem Tag 'Timer' hat keine TextMeshProUGUI-Komponente!");
                }
                if (roundTimer == null)
                {
                    Debug.LogError("Das Objekt mit dem Tag 'Timer' hat keine Timer-Komponente!");
                }
            }
            else
            {
                Debug.LogError("Kein GameObject mit dem Tag 'Timer' in der Szene gefunden. Der Timer-Text kann nicht aktualisiert werden.");
            }

            // Finde das CameraFollow-Skript in der Szene neu
            m_CameraFollow = FindObjectOfType<CameraFollow>();
            if (m_CameraFollow == null)
            {
                Debug.LogError("CameraFollow-Skript nicht in der Szene gefunden. Die Kamera wird dem Spieler nicht folgen können!");
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            // Die gesamte Start-Logik wird jetzt in OnSceneLoaded verschoben.
            // Diese Methode kann leer bleiben, da der Start des Spiels beim Szenen-Laden
            // über OnSceneLoaded und Co_StartRace() gesteuert wird.
        }

        // Update is called once per frame
        void Update()
        {
            if (m_StartRace)
            {
                UpdatePlayerPosition();
            }
        }

        private void UpdatePlayerPosition()
        {
            int position = 0;
            // NEU: Füge eine Null-Prüfung für PlayerCar.m_Current hinzu, falls es noch nicht initialisiert ist
            if (PlayerCar.m_Current == null) return;

            int playerPoint = m_FinishedLaps * RaceTrackControl.m_Main.m_Checkpoints.Length + PlayerCar.m_Current.m_CurrentCheckpoint;

            for (int i = 1; i < 4; i++)
            {
                if (m_Cars[i] == null || m_Cars[i].GetComponent<Rivals>() == null) continue;
                int rivalPoint = m_Cars[i].GetComponent<Rivals>().m_FinishedLaps * RaceTrackControl.m_Main.m_Checkpoints.Length + m_Cars[i].GetComponent<Rivals>().m_WaypointsCounter;
                if (playerPoint < rivalPoint)
                {
                    position++;
                }
            }
            m_PlayerPosition = position;
        }

        public bool PlayerLapEndCheck()
        {
            // Löse das Event aus, um alle PickupSpawner zu informieren, dass eine Runde beendet ist.
            OnRoundEnd?.Invoke();

            if (roundTimer != null && m_FinishedLaps > 1)
            {
                roundTimer.AddTime(15f);
                Debug.Log($"GameControl: 15 Sekunden wurden hinzugefügt. Aktuelle Runden: {m_FinishedLaps}.");
            }
            else
            {
                Debug.Log($"GameControl: Keine Zeit hinzugefügt (m_FinishedLaps ist {m_FinishedLaps}).");
            }

            if (m_FinishedLaps == m_levelRounds)
            {
                if (!m_LostRace)
                {
                    // NEU: Füge eine Null-Prüfung hinzu
                    if (PlayerCar.m_Current != null) PlayerCar.m_Current.m_Control = false;
                    UISystem.ShowUI("win-ui");
                    m_WonRace = true;
                }
                else
                {
                    // NEU: Füge eine Null-Prüfung hinzu
                    if (PlayerCar.m_Current != null) PlayerCar.m_Current.m_Control = false;
                    UISystem.ShowUI("lose-ui");
                    var scoreText = GameObject.FindGameObjectsWithTag("Score");
                    if (scoreText.Length > 0 && scoreText[0].GetComponent<UnityEngine.UI.Text>() != null)
                    {
                        scoreText[0].GetComponent<UnityEngine.UI.Text>().text = m_FinishedLaps.ToString();
                    }
                }
                return true;
            }
            return false;
        }

        public void RivalsLapEndCheck(Rivals rival)
        {
            if (rival.m_FinishedLaps == m_levelRounds)
            {
                if (!m_WonRace)
                {
                    m_LostRace = true;
                    // NEU: Füge eine Null-Prüfung hinzu
                    if (PlayerCar.m_Current != null) PlayerCar.m_Current.m_Control = false;
                    UISystem.ShowUI("lose-ui");

                    var scoreText = GameObject.FindGameObjectsWithTag("Score");
                    if (scoreText.Length > 0 && scoreText[0].GetComponent<UnityEngine.UI.Text>() != null)
                    {
                        scoreText[0].GetComponent<UnityEngine.UI.Text>().text = m_FinishedLaps.ToString();
                    }
                }
            }
        }

        private IEnumerator Co_StartRace()
        {
            m_Cars = new GameObject[4];

            // Player spawnen
            GameObject playerCar = Instantiate(m_PlayerCarPrefab, RaceTrackControl.m_Main.m_StartPositions[0].position, RaceTrackControl.m_Main.m_StartPositions[0].rotation);
            m_Cars[0] = playerCar;

            // NEU: Weise die PlayerCar-Singleton-Instanz hier zu
            PlayerCar.m_Current = playerCar.GetComponent<PlayerCar>();

            var recorder = playerCar.GetComponent<LapRecorder>();
            if (recorder == null) recorder = playerCar.AddComponent<LapRecorder>();
            GhostManager.Instance.playerRecorder = recorder;
            GhostManager.Instance.OnLapStarted();

            // NEU: Weisen Sie der Kamera das neue Spielerauto als Ziel zu
            if (m_CameraFollow != null)
            {
                m_CameraFollow.SetTarget(playerCar.transform);
            }

            // Rivals spawnen
            for (int i = 1; i < 4; i++)
            {
                if (m_RivalCarPrefab == null) continue;

                GameObject rivalCar = Instantiate(m_RivalCarPrefab, RaceTrackControl.m_Main.m_StartPositions[i].position, RaceTrackControl.m_Main.m_StartPositions[i].rotation);
                m_Cars[i] = rivalCar;
            }

            m_PlayerPosition = 0;

            m_StartTimer = 3;
            for (int i = 3; i > 0; i--)
            {
                m_StartTimer = i;
                yield return new WaitForSeconds(1);
            }

            m_StartRace = true;
            m_StartTimer = 0;

            if (roundTimer != null)
            {
                roundTimer.StartTimer();
                StartLapTimer();
                Debug.Log("Rennen gestartet!");
            }
            else
            {
                Debug.LogError("Der Runden-Timer konnte nicht gestartet werden, da das 'Timer'-Objekt nicht gefunden wurde.");
            }
        }

        public float GetCurrentLapTime()
        {
            return Time.time - m_LapStartTime;
        }

        public void StartLapTimer()
        {
            m_LapStartTime = Time.time;
        }
    }
}