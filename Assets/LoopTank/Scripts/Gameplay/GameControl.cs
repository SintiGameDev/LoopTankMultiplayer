using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TopDownRace
{
    public enum RennPhase : byte { Warten, Countdown, Rennen, Beendet }

    public enum AusscheideGrund : byte { Phantom, ZeitAbgelaufen }

    /// <summary>
    /// Rennleitung. Liegt als Netzwerk-Objekt fest in der Rennszene.
    /// Der Host spawnt die Panzer, startet den Countdown und entscheidet, wer noch im Rennen ist.
    /// Runden, Timer und Fuel fuehrt jeder Client fuer seinen eigenen Panzer selbst.
    /// </summary>
    public class GameControl : NetworkBehaviour
    {
        public static GameControl m_Current;

        /// <summary>Wird ausgeloest, wenn der lokale Spieler eine Runde beendet.</summary>
        public event System.Action OnRoundEnd;

        [Tooltip("Anzahl der Runden, die im Einzelspieler für den Sieg benötigt werden.")]
        public int m_levelRounds;

        [HideInInspector]
        public int m_FinishedLaps;

        [Tooltip("Prefab für den Panzer. Muss ein NetworkObject haben und in der Netzwerk-Prefab-Liste stehen.")]
        public GameObject m_PlayerCarPrefab;

        [HideInInspector]
        public int m_PlayerPosition;

        [HideInInspector]
        public bool m_LostRace;
        [HideInInspector]
        public bool m_WonRace;

        [HideInInspector]
        public bool m_StartRace;

        [HideInInspector]
        public int m_StartTimer;

        [Tooltip("Das CameraFollow-Skript in der Szene.")]
        public CameraFollow m_CameraFollow;

        [Header("Multiplayer")]
        [Tooltip("Aus: Im Multiplayer sind nur die Ghosts der Gegner tödlich. An: auch die eigenen. Im Einzelspieler sind die eigenen Ghosts immer tödlich.")]
        public bool m_EigeneGhostsToedlich = false;

        [Tooltip("Länge des Countdowns in Sekunden.")]
        public float m_CountdownSekunden = 3f;

        public NetworkVariable<RennPhase> Phase = new NetworkVariable<RennPhase>(RennPhase.Warten);
        public NetworkVariable<int> SpielerAmStart = new NetworkVariable<int>(0);
        private NetworkVariable<double> m_StartZeit = new NetworkVariable<double>(0);

        public const ulong KeinSieger = ulong.MaxValue;

        public bool IstEinzelspieler => SpielerAmStart.Value <= 1;

        /// <summary>True, sobald der lokale Spieler ausgeschieden ist.</summary>
        public bool LokalRaus { get; private set; }

        private AusscheideGrund m_LokalerGrund;
        private bool m_ErgebnisGezeigt;

        // Nur auf dem Host: wer ist noch im Rennen.
        private readonly HashSet<ulong> m_Lebende = new HashSet<ulong>();

        // Timer
        private Timer roundTimer;
        private float m_LapStartTime;

        private void Awake()
        {
            m_Current = this;
            PlayerCar.Alle.Clear();
        }

        public override void OnDestroy()
        {
            if (m_Current == this) m_Current = null;
            base.OnDestroy();
        }

        private void Start()
        {
            FindSceneReferences();

            // Die Rennszene funktioniert nur innerhalb einer Netzwerk-Sitzung (auch der
            // Einzelspieler laeuft als Host). Wer sie direkt startet, landet im Menue.
            if (!NetzwerkSitzung.IstAktiv)
            {
                Debug.Log("[GameControl] Keine Netzwerk-Sitzung aktiv, wechsle ins Menü.");
                SceneManager.LoadScene(NetzwerkSitzung.MenueSzene);
            }
        }

        void FindSceneReferences()
        {
            GameObject timerObject = GameObject.FindGameObjectWithTag("Timer");
            if (timerObject != null)
            {
                roundTimer = timerObject.GetComponent<Timer>();
                if (roundTimer == null)
                {
                    Debug.LogError("Das Objekt mit dem Tag 'Timer' hat keine Timer-Komponente!");
                }
            }
            else
            {
                Debug.LogError("Kein GameObject mit dem Tag 'Timer' in der Szene gefunden. Der Timer kann nicht gestartet werden.");
            }

            if (m_CameraFollow == null)
            {
                m_CameraFollow = FindFirstObjectByType<CameraFollow>();
            }
            if (m_CameraFollow == null)
            {
                Debug.LogError("CameraFollow-Skript nicht in der Szene gefunden. Die Kamera wird dem Spieler nicht folgen können!");
            }
        }

        // ------------------------------------------------------------------
        // Netzwerk-Lebenszyklus
        // ------------------------------------------------------------------

        public override void OnNetworkSpawn()
        {
            if (!IsServer) return;

            NetworkManager.SceneManager.OnLoadEventCompleted += OnSzeneFuerAlleGeladen;
            NetworkManager.OnClientDisconnectCallback += OnClientGetrennt;
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer || NetworkManager == null) return;

            if (NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= OnSzeneFuerAlleGeladen;
            NetworkManager.OnClientDisconnectCallback -= OnClientGetrennt;
        }

        /// <summary>Host: Alle Clients haben die Rennszene geladen, jetzt Panzer verteilen.</summary>
        private void OnSzeneFuerAlleGeladen(string szenenName, LoadSceneMode modus, List<ulong> fertig, List<ulong> zeitueberschreitung)
        {
            if (szenenName != gameObject.scene.name || Phase.Value != RennPhase.Warten) return;

            var startPositionen = RaceTrackControl.m_Main.m_StartPositions;
            int slot = 0;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                Transform platz = startPositionen[slot % startPositionen.Length];
                GameObject panzer = Instantiate(m_PlayerCarPrefab, platz.position, platz.rotation);
                panzer.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, true);
                panzer.GetComponent<PlayerCar>().SpielerSlot.Value = slot;
                m_Lebende.Add(clientId);
                slot++;
            }

            SpielerAmStart.Value = slot;
            m_StartZeit.Value = NetworkManager.ServerTime.Time + m_CountdownSekunden + 0.5;
            Phase.Value = RennPhase.Countdown;
        }

        private void OnClientGetrennt(ulong clientId)
        {
            ServerSpielerRaus(clientId);
        }

        void Update()
        {
            if (!IsSpawned) return;

            switch (Phase.Value)
            {
                case RennPhase.Countdown:
                    double rest = m_StartZeit.Value - NetworkManager.ServerTime.Time;
                    m_StartTimer = Mathf.Clamp(Mathf.CeilToInt((float)rest), 0, Mathf.CeilToInt(m_CountdownSekunden));
                    if (rest <= 0)
                    {
                        RennenLokalStarten();
                        if (IsServer) Phase.Value = RennPhase.Rennen;
                    }
                    break;

                case RennPhase.Rennen:
                    RennenLokalStarten();
                    UpdatePlayerPosition();
                    break;
            }
        }

        private void RennenLokalStarten()
        {
            if (m_StartRace) return;

            m_StartRace = true;
            m_StartTimer = 0;
            StartLapTimer();

            if (roundTimer != null)
            {
                roundTimer.StartTimer();
            }
            else
            {
                Debug.LogError("Der Runden-Timer konnte nicht gestartet werden, da das 'Timer'-Objekt nicht gefunden wurde.");
            }
            Debug.Log("Rennen gestartet!");
        }

        /// <summary>Platzierung des lokalen Spielers nach gefahrenen Runden (0 = Erster).</summary>
        private void UpdatePlayerPosition()
        {
            if (PlayerCar.m_Current == null) return;

            int position = 0;
            int eigeneRunden = PlayerCar.m_Current.Runden.Value;
            foreach (var panzer in PlayerCar.Alle)
            {
                if (panzer != null && panzer != PlayerCar.m_Current && panzer.Runden.Value > eigeneRunden)
                    position++;
            }
            m_PlayerPosition = position;
        }

        // ------------------------------------------------------------------
        // Runden (lokal)
        // ------------------------------------------------------------------

        /// <summary>Wird vom Checkpoint aufgerufen, nachdem m_FinishedLaps erhoeht wurde.</summary>
        public bool PlayerLapEndCheck()
        {
            OnRoundEnd?.Invoke();

            if (roundTimer != null && m_FinishedLaps > 1)
            {
                roundTimer.AddTime(15f);
            }

            // Rundenziel gibt es nur im Einzelspieler; im Multiplayer gewinnt, wer uebrig bleibt.
            if (IstEinzelspieler && m_FinishedLaps == m_levelRounds)
            {
                LokalAusscheiden(AusscheideGrund.ZeitAbgelaufen);
                return true;
            }
            return false;
        }

        public float GetCurrentLapTime()
        {
            return Time.time - m_LapStartTime;
        }

        public void StartLapTimer()
        {
            m_LapStartTime = Time.time;
        }

        // ------------------------------------------------------------------
        // Ausscheiden und Rennende
        // ------------------------------------------------------------------

        public bool IstGhostToedlich(ulong ghostBesitzer)
        {
            if (IstEinzelspieler || m_EigeneGhostsToedlich) return true;
            return ghostBesitzer != NetworkManager.LocalClientId;
        }

        /// <summary>Vom Timer aufgerufen, wenn die Zeit des lokalen Spielers abgelaufen ist.</summary>
        public void ZeitAbgelaufen()
        {
            LokalAusscheiden(AusscheideGrund.ZeitAbgelaufen);
        }

        /// <summary>
        /// Der lokale Spieler ist raus (Ghost getroffen oder Zeit abgelaufen) und meldet das dem Host.
        /// </summary>
        public void LokalAusscheiden(AusscheideGrund grund)
        {
            if (LokalRaus || !IsSpawned || Phase.Value == RennPhase.Beendet) return;

            LokalRaus = true;
            m_LokalerGrund = grund;

            if (PlayerCar.m_Current != null) PlayerCar.m_Current.m_Control = false;
            if (roundTimer != null) roundTimer.StopTimer();

            if (!IstEinzelspieler && RennHud.Instanz != null)
            {
                RennHud.Instanz.BannerZeigen(grund == AusscheideGrund.Phantom
                    ? "Phantom-Kollision! Du bist raus und schaust zu."
                    : "Zeit abgelaufen! Du bist raus und schaust zu.");
            }

            AusscheidenRpc();
        }

        [Rpc(SendTo.Server)]
        private void AusscheidenRpc(RpcParams rpcParams = default)
        {
            ServerSpielerRaus(rpcParams.Receive.SenderClientId);
        }

        private void ServerSpielerRaus(ulong clientId)
        {
            if (!IsServer || !m_Lebende.Remove(clientId)) return;

            foreach (var panzer in PlayerCar.Alle)
            {
                if (SpielerAmStart.Value > 1 && panzer != null && panzer.IsSpawned && panzer.OwnerClientId == clientId)
                    panzer.Lebt.Value = false;
            }

            if (Phase.Value == RennPhase.Beendet) return;

            if (SpielerAmStart.Value <= 1)
            {
                if (m_Lebende.Count == 0) ServerRennenBeenden(KeinSieger);
            }
            else if (m_Lebende.Count <= 1)
            {
                ulong sieger = KeinSieger;
                foreach (ulong id in m_Lebende) sieger = id;
                ServerRennenBeenden(sieger);
            }
        }

        private void ServerRennenBeenden(ulong sieger)
        {
            Phase.Value = RennPhase.Beendet;

            int siegerSlot = -1;
            foreach (var panzer in PlayerCar.Alle)
            {
                if (panzer != null && panzer.OwnerClientId == sieger) siegerSlot = panzer.SpielerSlot.Value;
            }
            RennEndeRpc(sieger, siegerSlot);
        }

        [Rpc(SendTo.Everyone)]
        private void RennEndeRpc(ulong sieger, int siegerSlot)
        {
            if (m_ErgebnisGezeigt) return;
            m_ErgebnisGezeigt = true;

            if (PlayerCar.m_Current != null) PlayerCar.m_Current.m_Control = false;
            if (roundTimer != null) roundTimer.StopTimer();
            if (GhostManager.Instance != null) GhostManager.Instance.ClearAllGhosts();
            if (RennHud.Instanz != null) RennHud.Instanz.BannerZeigen("");

            bool gewonnen;
            string titel;
            if (IstEinzelspieler)
            {
                // Wie im Original: Zeit ueberstanden = geschafft, Ghost getroffen = verloren.
                gewonnen = m_LokalerGrund == AusscheideGrund.ZeitAbgelaufen;
                titel = gewonnen ? "Geschafft!" : "Phantom Collision";
            }
            else if (sieger == KeinSieger)
            {
                gewonnen = false;
                titel = "Unentschieden";
            }
            else
            {
                gewonnen = sieger == NetworkManager.LocalClientId;
                titel = gewonnen ? "Sieg!" : SpielerFarben.Name(siegerSlot) + " gewinnt";
            }

            m_WonRace = gewonnen;
            m_LostRace = !gewonnen;
            ErgebnisUI.Zeigen(gewonnen, titel, m_FinishedLaps);
        }

        /// <summary>Nur der Host kann eine neue Runde starten; die Szene wird fuer alle neu geladen.</summary>
        public void NeustartAnfordern()
        {
            if (!IsServer || !IsSpawned) return;
            NetworkManager.SceneManager.LoadScene(gameObject.scene.name, LoadSceneMode.Single);
        }

        // ------------------------------------------------------------------
        // Kamera
        // ------------------------------------------------------------------

        /// <summary>Kamera folgt dem eigenen Panzer; nach dem Ausscheiden einem, der noch faehrt.</summary>
        public void KameraZielAktualisieren()
        {
            if (m_CameraFollow == null) return;

            PlayerCar ziel = null;
            if (PlayerCar.m_Current != null && PlayerCar.m_Current.Lebt.Value)
            {
                ziel = PlayerCar.m_Current;
            }
            else
            {
                foreach (var panzer in PlayerCar.Alle)
                {
                    if (panzer != null && panzer.IsSpawned && panzer.Lebt.Value) { ziel = panzer; break; }
                }
                if (ziel == null) ziel = PlayerCar.m_Current;
            }

            if (ziel != null) m_CameraFollow.SetTarget(ziel.transform);
        }
    }
}
