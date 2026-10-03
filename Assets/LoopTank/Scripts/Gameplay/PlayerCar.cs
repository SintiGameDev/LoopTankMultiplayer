using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Der Panzer eines Spielers. Jeder Client steuert und simuliert nur seinen eigenen Panzer
    /// (IsOwner); die Position wird ueber NetworkTransform an alle anderen verteilt.
    /// </summary>
    public class PlayerCar : NetworkBehaviour
    {
        [HideInInspector]
        public float m_Speed;

        [HideInInspector]
        public int m_CurrentCheckpoint;

        [HideInInspector]
        public bool m_Control = false;

        /// <summary>Der Panzer des lokalen Spielers (nicht mehr "der einzige Panzer").</summary>
        public static PlayerCar m_Current;

        /// <summary>Alle aktuell gespawnten Panzer, auf jedem Client.</summary>
        public static readonly List<PlayerCar> Alle = new List<PlayerCar>();

        [Tooltip("Die Geschwindigkeit, mit der sich der gesamte Panzer (Body) beim Bewegen und im Stand dreht (A/D-Tasten).")]
        [Range(0.1f, 10.0f)]
        public float m_RotationSpeed = 3.0f;

        [Tooltip("Die Geschwindigkeit, mit der sich der TankTop mit den Pfeiltasten dreht.")]
        [Range(10.0f, 300.0f)]
        public float m_TankTopRotationSpeed = 100.0f;

        // --- SOUND-VARIABLEN FÜR DAS GRUNDLEGENDE MOTORENGERÄUSCH ---
        [Tooltip("Das Soundfile für das konstante Grund-Motorengeräusch des Panzers.")]
        public AudioClip m_EngineSoundClip;
        private AudioSource m_EngineAudioSource;

        [Tooltip("Die Lautstärke des konstanten Grund-Motorengeräuschs.")]
        [Range(0.0f, 1.0f)]
        public float m_EngineVolume = 0.5f;

        // --- SOUND-VARIABLEN FÜR DEN GESCHWINDIGKEITSABHÄNGIGEN SOUND (PITCH-ÄNDERUNG) ---
        [Tooltip("Das Soundfile, dessen Tonhöhe sich mit der Geschwindigkeit des Panzers ändert (z.B. Turbopfeifen, Anfahrgeräusch).")]
        public AudioClip m_AccelerationSoundClip;
        private AudioSource m_AccelerationAudioSource;

        [Tooltip("Die minimale Tonhöhe des Beschleunigungssounds bei Stillstand oder sehr langsamer Fahrt.")]
        [Range(0.1f, 2.0f)]
        public float m_MinPitch = 0.8f;

        [Tooltip("Die maximale Tonhöhe des Beschleunigungssounds bei voller Geschwindigkeit.")]
        [Range(1.0f, 3.0f)]
        public float m_MaxPitch = 1.8f;

        [Tooltip("Die minimale Lautstärke des Beschleunigungssounds bei Stillstand.")]
        [Range(0.0f, 1.0f)]
        public float m_MinAccelerationVolume = 0.0f;

        [Tooltip("Die maximale Lautstärke des Beschleunigungssounds bei voller Geschwindigkeit.")]
        [Range(0.0f, 1.0f)]
        public float m_MaxAccelerationVolume = 1.0f;

        // --- KOLLISIONSSOUNDS ---
        [Tooltip("Eine Liste von Soundeffekten, die zufällig abgespielt werden, wenn der Panzer kollidiert.")]
        public List<AudioClip> m_CollisionSoundClips;
        [Tooltip("Die maximale Lautstärke der Kollisionssounds.")]
        [Range(0.0f, 1.0f)]
        public float m_CollisionSoundVolume = 0.7f;
        private AudioSource m_CollisionAudioSource;

        // --- TANKTOP-DREH-SOUND ---
        [Tooltip("Das Soundfile, das abgespielt wird, wenn der TankTop gedreht wird.")]
        public AudioClip m_TankTopRotationSoundClip;
        [Tooltip("Die Lautstärke des TankTop-Rotationssounds.")]
        [Range(0.0f, 1.0f)]
        public float m_TankTopRotationVolume = 0.8f;
        [Tooltip("Die Geschwindigkeit, mit der der TankTop-Rotationssound ein-/ausblendet.")]
        [Range(0.1f, 5.0f)]
        public float m_TankTopFadeSpeed = 2.0f;
        private AudioSource m_TankTopRotationAudioSource;

        // --- GESCHWINDIGKEITSBOOST ---
        [Tooltip("Der Multiplikator, um den die Geschwindigkeit während des Boosts erhöht wird.")]
        public float m_BoostSpeedMultiplier = 1.5f;

        private float m_BaseSpeedForce;
        private bool m_IsBoosting = false;

        // --- NETZWERK ---
        /// <summary>Startplatz 0..3, bestimmt Startposition und Farbe. Setzt der Host.</summary>
        public NetworkVariable<int> SpielerSlot = new NetworkVariable<int>(0);

        /// <summary>Ob der Spieler noch im Rennen ist. Setzt der Host.</summary>
        public NetworkVariable<bool> Lebt = new NetworkVariable<bool>(true);

        /// <summary>Gefahrene Runden, fuer die Anzeige bei allen. Schreibt der Besitzer.</summary>
        public NetworkVariable<int> Runden = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Ghost-Uebertragung: eine Runde wird in mehreren kleinen Paketen verschickt.
        private const float k_GhostIntervall = 0.05f;
        private const int k_GhostPaketGroesse = 80;
        private int m_GhostZaehler;
        private int m_EmpfangId = -1;
        private Vector3[] m_EmpfangPuffer;
        private int m_EmpfangAnzahl;

        // Checkpoints, die seit der letzten Zielueberquerung passiert wurden.
        private readonly HashSet<int> m_PassierteCheckpoints = new HashSet<int>();
        private bool m_ErsteUeberquerung = true;

        private CarPhysics m_CarPhysics;

        private Transform m_TankTop;
        private bool m_CollisionsIgnored = false;

        public override void OnNetworkSpawn()
        {
            if (!Alle.Contains(this)) Alle.Add(this);

            SpielerSlot.OnValueChanged += OnSlotGeaendert;
            Lebt.OnValueChanged += OnLebtGeaendert;
            FarbeAnwenden();

            if (IsOwner)
            {
                m_Current = this;

                var recorder = GetComponent<LapRecorder>();
                if (recorder == null) recorder = gameObject.AddComponent<LapRecorder>();
                if (GhostManager.Instance != null)
                {
                    GhostManager.Instance.playerRecorder = recorder;
                    GhostManager.Instance.OnLapStarted();
                }
            }
            else
            {
                // Fremde Panzer werden nur angezeigt: keine eigene Physik-Steuerung, keine Aufnahme.
                var physik = GetComponent<CarPhysics>();
                if (physik != null) physik.enabled = false;
                var recorder = GetComponent<LapRecorder>();
                if (recorder != null) recorder.enabled = false;
            }

            if (!Lebt.Value) SichtbarSetzen(false);

            if (GameControl.m_Current != null) GameControl.m_Current.KameraZielAktualisieren();
        }

        public override void OnNetworkDespawn()
        {
            SpielerSlot.OnValueChanged -= OnSlotGeaendert;
            Lebt.OnValueChanged -= OnLebtGeaendert;

            Alle.Remove(this);
            if (m_Current == this) m_Current = null;

            if (GhostManager.Instance != null) GhostManager.Instance.GhostsEntfernen(OwnerClientId);
            if (GameControl.m_Current != null) GameControl.m_Current.KameraZielAktualisieren();
        }

        void Start()
        {
            m_CurrentCheckpoint = 1;
            m_Speed = 80;

            m_TankTop = transform.Find("TankTop");

            if (m_TankTop == null)
            {
                Debug.LogError("TankTop-Objekt nicht gefunden! Stelle sicher, dass ein Kindobjekt mit dem Namen 'TankTop' existiert.", this);
            }

            m_CarPhysics = GetComponent<CarPhysics>();
            if (m_CarPhysics == null)
            {
                Debug.LogError("CarPhysics-Komponente nicht gefunden! Kann Geschwindigkeit nicht anpassen.", this);
            }
            else
            {
                m_BaseSpeedForce = m_CarPhysics.m_SpeedForce;
            }

            // Steuerung und Motorsounds gibt es nur fuer den eigenen Panzer.
            if (!IsOwner) return;

            m_Control = true;

            m_EngineAudioSource = GetComponent<AudioSource>();
            if (m_EngineAudioSource == null)
            {
                m_EngineAudioSource = gameObject.AddComponent<AudioSource>();
            }

            if (m_EngineSoundClip != null)
            {
                m_EngineAudioSource.clip = m_EngineSoundClip;
                m_EngineAudioSource.loop = true;
                m_EngineAudioSource.playOnAwake = false;
                m_EngineAudioSource.volume = m_EngineVolume;
                m_EngineAudioSource.Play();
            }
            else
            {
                Debug.LogWarning("Kein GRUND-Motorengeräusch-Clip zugewiesen! Bitte weise einen im Inspector zu.", this);
            }

            m_AccelerationAudioSource = gameObject.AddComponent<AudioSource>();
            if (m_AccelerationSoundClip != null)
            {
                m_AccelerationAudioSource.clip = m_AccelerationSoundClip;
                m_AccelerationAudioSource.loop = true;
                m_AccelerationAudioSource.playOnAwake = false;
                m_AccelerationAudioSource.volume = m_MinAccelerationVolume;
                m_AccelerationAudioSource.pitch = m_MinPitch;
                m_AccelerationAudioSource.Play();
            }
            else
            {
                Debug.LogWarning("Kein BESCHLEUNIGUNGS-Sound-Clip zugewiesen! Bitte weise einen im Inspector zu.", this);
            }

            m_CollisionAudioSource = gameObject.AddComponent<AudioSource>();
            m_CollisionAudioSource.loop = false;
            m_CollisionAudioSource.playOnAwake = false;
            m_CollisionAudioSource.volume = m_CollisionSoundVolume;

            m_TankTopRotationAudioSource = gameObject.AddComponent<AudioSource>();
            if (m_TankTopRotationSoundClip != null)
            {
                m_TankTopRotationAudioSource.clip = m_TankTopRotationSoundClip;
                m_TankTopRotationAudioSource.loop = true;
                m_TankTopRotationAudioSource.playOnAwake = false;
                m_TankTopRotationAudioSource.volume = 0.0f;
                m_TankTopRotationAudioSource.Play();
            }
            else
            {
                Debug.LogWarning("Kein TankTop-Rotations-Sound-Clip zugewiesen! Bitte weise einen im Inspector zu.", this);
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            // Jeder Client prueft nur den eigenen Panzer.
            if (!IsOwner || !m_Control || !Lebt.Value) return;
            if (!collision.CompareTag("Ghost") || m_CollisionsIgnored) return;
            if (HasTagInHierarchy(collision.gameObject, "CollisionIgnorer")) return;
            if (GameControl.m_Current == null) return;

            var ghost = collision.GetComponentInParent<GhostReplay>();
            if (ghost != null && !GameControl.m_Current.IstGhostToedlich(ghost.BesitzerId)) return;

            Debug.Log("Kollision mit Ghost! Spieler scheidet aus.");
            GameControl.m_Current.LokalAusscheiden(AusscheideGrund.Phantom);
        }

        bool HasTagInHierarchy(GameObject obj, string tag)
        {
            if (obj.CompareTag(tag)) return true;
            foreach (Transform child in obj.transform)
            {
                if (HasTagInHierarchy(child.gameObject, tag))
                    return true;
            }
            return false;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (m_CollisionSoundClips != null && m_CollisionSoundClips.Count > 0 && m_CollisionAudioSource != null)
            {
                int randomIndex = Random.Range(0, m_CollisionSoundClips.Count);
                AudioClip clipToPlay = m_CollisionSoundClips[randomIndex];
                m_CollisionAudioSource.PlayOneShot(clipToPlay, m_CollisionSoundVolume);
            }
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (collision.CompareTag("CollisionIgnorer"))
            {
                m_CollisionsIgnored = true;
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("CollisionIgnorer"))
            {
                m_CollisionsIgnored = false;
            }
        }

        void Update()
        {
            if (!IsOwner || m_CarPhysics == null) return;

            float verticalInput = Input.GetAxisRaw("Vertical");
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            float tankTopRotationInput = Input.GetAxisRaw("TankTopHorizontal");

            if (GameControl.m_Current != null && GameControl.m_Current.m_StartRace)
            {
                if (m_Control)
                {
                    m_CarPhysics.m_InputAccelerate = verticalInput;

                    if (Mathf.Abs(verticalInput) > 0.01f)
                    {
                        m_CarPhysics.m_InputSteer = -horizontalInput * m_RotationSpeed * (verticalInput > 0 ? 1 : -1);
                    }
                    else
                    {
                        m_CarPhysics.m_InputSteer = -horizontalInput * m_RotationSpeed;
                    }

                    bool spacePressed = Input.GetKey(KeyCode.Space);

                    if (spacePressed)
                    {
                        // UI einblenden, wenn die Space-Taste gedrückt wird
                        if (FuelMechanic.Instance != null)
                        {
                            FuelMechanic.Instance.SetFuelUIActive(true);
                        }

                        if (FuelMechanic.Instance != null && FuelMechanic.Instance.CurrentFuel > 0)
                        {
                            if (!m_IsBoosting)
                            {
                                m_CarPhysics.m_SpeedForce = m_BaseSpeedForce * m_BoostSpeedMultiplier;
                                m_IsBoosting = true;
                            }

                            // Kraftstoff abziehen
                            FuelMechanic.Instance.DrainFuel();
                        }
                        else // Kein Kraftstoff mehr, Boost beenden
                        {
                            if (m_IsBoosting)
                            {
                                m_CarPhysics.m_SpeedForce = m_BaseSpeedForce;
                                m_IsBoosting = false;
                            }
                        }
                    }
                    else
                    {
                        // UI ausblenden, wenn die Space-Taste losgelassen wird
                        if (FuelMechanic.Instance != null)
                        {
                            FuelMechanic.Instance.SetFuelUIActive(false);
                        }

                        if (m_IsBoosting)
                        {
                            m_CarPhysics.m_SpeedForce = m_BaseSpeedForce;
                            m_IsBoosting = false;
                        }

                        if (FuelMechanic.Instance != null)
                        {
                            FuelMechanic.Instance.RefillFuel();
                        }
                    }
                }
            }

            if (m_TankTop != null)
            {
                m_TankTop.Rotate(0, 0, -tankTopRotationInput * m_TankTopRotationSpeed * Time.deltaTime);

                if (m_TankTopRotationAudioSource != null)
                {
                    if (Mathf.Abs(tankTopRotationInput) > 0.01f && m_Control)
                    {
                        m_TankTopRotationAudioSource.volume = Mathf.MoveTowards(m_TankTopRotationAudioSource.volume, m_TankTopRotationVolume, m_TankTopFadeSpeed * Time.deltaTime);
                    }
                    else
                    {
                        m_TankTopRotationAudioSource.volume = Mathf.MoveTowards(m_TankTopRotationAudioSource.volume, 0.0f, m_TankTopFadeSpeed * Time.deltaTime);
                    }
                }
            }

            if (m_AccelerationAudioSource != null && m_CarPhysics.m_Body != null)
            {
                float currentSpeed = m_CarPhysics.m_Body.linearVelocity.magnitude;
                float speedNormalized = 0f;
                if (m_Speed > 0)
                {
                    speedNormalized = Mathf.Clamp01(Mathf.Abs(currentSpeed) / m_Speed);
                }
                m_AccelerationAudioSource.pitch = Mathf.Lerp(m_MinPitch, m_MaxPitch, speedNormalized);
                m_AccelerationAudioSource.volume = Mathf.Lerp(m_MinAccelerationVolume, m_MaxAccelerationVolume, speedNormalized);
            }
        }

        // ------------------------------------------------------------------
        // Runden / Checkpoints
        // ------------------------------------------------------------------

        public void CheckpointPassiert(int id)
        {
            m_PassierteCheckpoints.Add(id);
            m_CurrentCheckpoint = id + 1;
        }

        /// <summary>
        /// Eine Zielueberquerung zaehlt nur, wenn seit der letzten alle Zwischen-Checkpoints
        /// passiert wurden. Die allererste Ueberquerung direkt nach dem Start zaehlt immer.
        /// </summary>
        public bool DarfRundeBeenden(int benoetigteCheckpoints)
        {
            return m_ErsteUeberquerung || m_PassierteCheckpoints.Count >= benoetigteCheckpoints;
        }

        public void RundeBeendet(int gefahreneRunden)
        {
            m_ErsteUeberquerung = false;
            m_PassierteCheckpoints.Clear();
            m_CurrentCheckpoint = 1;
            Runden.Value = gefahreneRunden;
        }

        // ------------------------------------------------------------------
        // Ghosts
        // ------------------------------------------------------------------

        /// <summary>
        /// Schickt eine aufgezeichnete Runde an alle Spieler. Jeder Client spielt sie dann lokal
        /// als Ghost ab; es werden also keine Ghost-Positionen pro Frame uebertragen.
        /// </summary>
        public void RundeAlsGhostSenden(LapData runde, float verzoegerung)
        {
            if (!IsOwner || !IsSpawned || !Lebt.Value) return;
            if (runde == null || runde.frames.Count < 2) return;

            // Von 50 Hz Aufnahme auf 20 Hz reduzieren, die Wiedergabe interpoliert ohnehin.
            float dauer = runde.frames[runde.frames.Count - 1].t;
            int anzahl = Mathf.Max(2, Mathf.CeilToInt(dauer / k_GhostIntervall) + 1);
            var punkte = new Vector3[anzahl];
            int index = 1;
            for (int n = 0; n < anzahl; n++)
            {
                float t = Mathf.Min(n * k_GhostIntervall, dauer);
                while (index < runde.frames.Count - 1 && runde.frames[index].t < t) index++;
                var a = runde.frames[index - 1];
                var b = runde.frames[index];
                float anteil = Mathf.InverseLerp(a.t, b.t, t);
                Vector2 pos = Vector2.Lerp(a.pos, b.pos, anteil);
                punkte[n] = new Vector3(pos.x, pos.y, Mathf.LerpAngle(a.rotZ, b.rotZ, anteil));
            }

            int id = ++m_GhostZaehler;
            double startZeit = NetworkManager.ServerTime.Time + verzoegerung;
            for (int offset = 0; offset < anzahl; offset += k_GhostPaketGroesse)
            {
                int laenge = Mathf.Min(k_GhostPaketGroesse, anzahl - offset);
                var teil = new Vector3[laenge];
                System.Array.Copy(punkte, offset, teil, 0, laenge);
                GhostTeilRpc(id, offset, anzahl, startZeit, teil);
            }
        }

        [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Owner)]
        private void GhostTeilRpc(int ghostId, int offset, int gesamt, double startZeit, Vector3[] teil)
        {
            if (teil == null || gesamt < 2 || gesamt > 20000) return;

            if (ghostId != m_EmpfangId || m_EmpfangPuffer == null || m_EmpfangPuffer.Length != gesamt)
            {
                m_EmpfangId = ghostId;
                m_EmpfangPuffer = new Vector3[gesamt];
                m_EmpfangAnzahl = 0;
            }

            if (offset < 0 || offset + teil.Length > gesamt) return;
            System.Array.Copy(teil, 0, m_EmpfangPuffer, offset, teil.Length);
            m_EmpfangAnzahl += teil.Length;

            if (m_EmpfangAnzahl < gesamt) return;

            var punkte = m_EmpfangPuffer;
            m_EmpfangPuffer = null;
            m_EmpfangId = -1;

            if (Lebt.Value && GhostManager.Instance != null)
                GhostManager.Instance.GhostStarten(OwnerClientId, SpielerSlot.Value, punkte, k_GhostIntervall, startZeit);
        }

        // ------------------------------------------------------------------
        // Anzeige
        // ------------------------------------------------------------------

        private void OnSlotGeaendert(int alt, int neu) => FarbeAnwenden();

        private void OnLebtGeaendert(bool alt, bool neu)
        {
            if (neu) return;

            m_Control = false;
            SichtbarSetzen(false);

            if (GhostManager.Instance != null) GhostManager.Instance.GhostsEntfernen(OwnerClientId);
            if (GameControl.m_Current != null) GameControl.m_Current.KameraZielAktualisieren();
        }

        private void FarbeAnwenden()
        {
            Color farbe = SpielerFarben.Farbe(SpielerSlot.Value);
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.CompareTag("TankBody") || sr.CompareTag("TankTop"))
                    sr.color = new Color(farbe.r, farbe.g, farbe.b, sr.color.a);
            }
        }

        /// <summary>Blendet einen ausgeschiedenen Panzer bei allen aus, ohne ihn zu despawnen.</summary>
        private void SichtbarSetzen(bool sichtbar)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = sichtbar;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = sichtbar;
            foreach (var a in GetComponents<AudioSource>()) a.mute = !sichtbar;

            if (IsOwner)
            {
                var body = GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                }
            }
        }
    }
}
