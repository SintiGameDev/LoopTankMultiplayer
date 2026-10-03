using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Components;
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
        /// <summary>Startplatz 0..3. Setzt der Host.</summary>
        public NetworkVariable<int> SpielerSlot = new NetworkVariable<int>(0);

        /// <summary>Gewaehlte Farbe (Nummer in SpielerFarben). Setzt der Host aus dem Profil der Lobby.</summary>
        public NetworkVariable<int> FarbIndex = new NetworkVariable<int>(0);

        /// <summary>Gewaehlter Anzeigename. Setzt der Host aus dem Profil der Lobby.</summary>
        public NetworkVariable<FixedString64Bytes> SpielerName = new NetworkVariable<FixedString64Bytes>();

        /// <summary>Anzeigename, mit Rueckfall auf "Spieler N".</summary>
        public string AnzeigeName => SpielerName.Value.Length > 0 ? SpielerName.Value.ToString() : SpielerFarben.StandardName(SpielerSlot.Value);

        /// <summary>Startplatz dieses Panzers. Setzt der Host; StartGesetzt kommt zuletzt und loest das Aufstellen aus.</summary>
        public NetworkVariable<Vector3> StartPosition = new NetworkVariable<Vector3>();
        public NetworkVariable<Quaternion> StartDrehung = new NetworkVariable<Quaternion>(Quaternion.identity);
        public NetworkVariable<bool> StartGesetzt = new NetworkVariable<bool>(false);

        /// <summary>Ob der Spieler noch im Rennen ist. Setzt der Host.</summary>
        public NetworkVariable<bool> Lebt = new NetworkVariable<bool>(true);

        /// <summary>Gefahrene Runden, fuer die Anzeige bei allen. Schreibt der Besitzer.</summary>
        /// <summary>Blickrichtung des Turms (Welt-Drehung um Y), nur in 3D benutzt. Schreibt der Besitzer.</summary>
        public NetworkVariable<float> TurmWinkel = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

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
        private int m_ZwischenIndex;
        private bool m_ErsteUeberquerung = true;

        private FahrPhysik m_CarPhysics;

        private Transform m_TankTop;
        private PanzerTurm3D m_Turm;
        private float m_NaechsteTurmSendung;
        private float m_LetzteSchutzZeit = -10f;

        public override void OnNetworkSpawn()
        {
            if (!Alle.Contains(this)) Alle.Add(this);

            FarbIndex.OnValueChanged += OnSlotGeaendert;
            StartGesetzt.OnValueChanged += OnStartGesetzt;
            Lebt.OnValueChanged += OnLebtGeaendert;
            FarbeAnwenden();

            if (IsOwner)
            {
                m_Current = this;
                AufStartplatzSetzen(true);   // falls der Startplatz schon mit dem Spawn ankam

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
                var physik = GetComponent<FahrPhysik>();
                if (physik != null) physik.enabled = false;
                var recorder = GetComponent<LapRecorder>();
                if (recorder != null) recorder.enabled = false;
            }

            if (!Lebt.Value) SichtbarSetzen(false);

            if (GameControl.m_Current != null) GameControl.m_Current.KameraZielAktualisieren();
        }

        public override void OnNetworkDespawn()
        {
            FarbIndex.OnValueChanged -= OnSlotGeaendert;
            StartGesetzt.OnValueChanged -= OnStartGesetzt;
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

            // Direktes Kind (2D-Prefab) oder tiefer in der Hierarchie (3D: unter dem Optik-Objekt).
            m_TankTop = transform.Find("TankTop");
            if (m_TankTop == null)
            {
                foreach (Transform kind in GetComponentsInChildren<Transform>(true))
                {
                    if (kind.name == "TankTop") { m_TankTop = kind; break; }
                }
            }

            m_Turm = GetComponent<PanzerTurm3D>();

            if (m_TankTop == null)
            {
                Debug.LogError("TankTop-Objekt nicht gefunden! Stelle sicher, dass ein Kindobjekt mit dem Namen 'TankTop' existiert.", this);
            }

            m_CarPhysics = GetComponent<FahrPhysik>();
            if (m_CarPhysics == null)
            {
                Debug.LogError("FahrPhysik-Komponente (CarPhysics oder CarPhysics3D) nicht gefunden! Kann Geschwindigkeit nicht anpassen.", this);
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

        // Die Unity-Callbacks gibt es doppelt (2D- und 3D-Physik); beide landen in derselben Logik,
        // damit der Panzer in der 2D- und in der 3D-Rennszene gleich funktioniert.
        //
        // Geprueft wird beim Eintreten UND in jedem Physikschritt, solange sich etwas ueberlappt.
        // Frueher zaehlte nur das Eintreten: Wer in diesem Moment geschuetzt war (Startzone oder
        // frisch erschienener Ghost), wurde danach nie wieder geprueft und fuhr unbeschadet hindurch.
        private void OnTriggerEnter2D(Collider2D collision) => TriggerBeruehrt(collision.gameObject);
        private void OnTriggerEnter(Collider collision) => TriggerBeruehrt(collision.gameObject);
        private void OnTriggerStay2D(Collider2D collision) => TriggerBeruehrt(collision.gameObject);
        private void OnTriggerStay(Collider collision) => TriggerBeruehrt(collision.gameObject);
        private void OnCollisionEnter2D(Collision2D collision) => KollisionsSound();
        private void OnCollisionEnter(Collision collision) => KollisionsSound();

        /// <summary>True, solange der Panzer in einer Schutzzone steht (Tag "CollisionIgnorer").</summary>
        private bool InSchutzzone => Time.time - m_LetzteSchutzZeit < 0.12f;

        private void TriggerBeruehrt(GameObject anderes)
        {
            // Schutzzone: Zeitstempel statt Ein/Aus-Schalter. Ein Schalter bliebe haengen, wenn
            // die Zone verschwindet, waehrend man drinsteht (dann kommt kein Exit-Ereignis mehr).
            if (anderes.CompareTag("CollisionIgnorer"))
            {
                m_LetzteSchutzZeit = Time.time;
                return;
            }

            // Jeder Client prueft nur den eigenen Panzer.
            if (!IsOwner || !m_Control || !Lebt.Value) return;
            if (!anderes.CompareTag("Ghost") || InSchutzzone) return;
            if (HasTagInHierarchy(anderes, "CollisionIgnorer")) return;   // Ghost ist gerade erst erschienen
            if (GameControl.m_Current == null || !GameControl.m_Current.m_StartRace) return;

            var ghost = anderes.GetComponentInParent<GhostReplay>();
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

        private void KollisionsSound()
        {
            if (m_CollisionSoundClips != null && m_CollisionSoundClips.Count > 0 && m_CollisionAudioSource != null)
            {
                int randomIndex = Random.Range(0, m_CollisionSoundClips.Count);
                AudioClip clipToPlay = m_CollisionSoundClips[randomIndex];
                m_CollisionAudioSource.PlayOneShot(clipToPlay, m_CollisionSoundVolume);
            }
        }

        void Update()
        {
            if (!IsOwner)
            {
                // Fremde Panzer: Turm auf den uebertragenen Winkel ausrichten.
                if (m_Turm != null) m_Turm.ZielWinkel = TurmWinkel.Value;
                return;
            }
            if (m_CarPhysics == null) return;

            float verticalInput = Input.GetAxisRaw("Vertical");
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            float tankTopRotationInput = Input.GetAxisRaw("TankTopHorizontal");

            // Rohe Eingabe fuer die 3D-Fahrphysik. Ohne Kontrolle (Countdown, ausgeschieden) steht sie auf 0.
            bool darfFahren = m_Control && GameControl.m_Current != null && GameControl.m_Current.m_StartRace;
            m_CarPhysics.m_Gas = darfFahren ? verticalInput : 0f;
            m_CarPhysics.m_Lenkung = darfFahren ? horizontalInput : 0f;
            m_CarPhysics.m_Drift = darfFahren && m_CarPhysics.KannDriften && Input.GetKey(KeyCode.Space);

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

                    // Kann die Physik driften (3D), liegt der Drift auf der Leertaste und der Boost auf Shift.
                    bool spacePressed = Input.GetKey(m_CarPhysics.KannDriften ? KeyCode.LeftShift : KeyCode.Space);

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
                // In 3D richtet PanzerTurm3D den Turm nach der Kamera aus; die Pfeiltasten gelten nur fuer 2D.
                if (m_Turm == null)
                    m_TankTop.Rotate(m_CarPhysics.TurmDrehung(tankTopRotationInput * m_TankTopRotationSpeed * Time.deltaTime));
                else
                    TurmWinkelSenden();

                bool turmDreht = m_Turm != null ? m_Turm.DrehAnteil > 0.08f : Mathf.Abs(tankTopRotationInput) > 0.01f;

                if (m_TankTopRotationAudioSource != null)
                {
                    if (turmDreht && m_Control)
                    {
                        m_TankTopRotationAudioSource.volume = Mathf.MoveTowards(m_TankTopRotationAudioSource.volume, m_TankTopRotationVolume, m_TankTopFadeSpeed * Time.deltaTime);
                    }
                    else
                    {
                        m_TankTopRotationAudioSource.volume = Mathf.MoveTowards(m_TankTopRotationAudioSource.volume, 0.0f, m_TankTopFadeSpeed * Time.deltaTime);
                    }
                }
            }

            if (m_AccelerationAudioSource != null)
            {
                float currentSpeed = m_CarPhysics.Tempo;
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
        // Startplatz
        // ------------------------------------------------------------------

        /// <summary>
        /// Stellt den eigenen Panzer auf seinen Startplatz. Der Host legt den Platz fest, aber nur
        /// der Besitzer darf seinen Panzer bewegen. Deshalb setzt sich jeder Client selbst dorthin.
        /// </summary>
        /// <param name="sprung">True: allen anderen mitteilen, dass der Panzer springt, statt die Strecke zu interpolieren.</param>
        private void AufStartplatzSetzen(bool sprung)
        {
            if (!IsOwner || !StartGesetzt.Value) return;

            var physik = GetComponent<FahrPhysik>();
            if (physik != null) physik.Anhalten();

            Vector3 position = StartPosition.Value;
            Quaternion drehung = StartDrehung.Value;

            var body = GetComponent<Rigidbody>();
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.position = position;
                body.rotation = drehung;
            }
            var body2D = GetComponent<Rigidbody2D>();
            if (body2D != null)
            {
                body2D.linearVelocity = Vector2.zero;
                body2D.angularVelocity = 0f;
                body2D.position = position;
                body2D.rotation = drehung.eulerAngles.z;
            }
            transform.SetPositionAndRotation(position, drehung);

            if (sprung)
            {
                var netz = GetComponent<NetworkTransform>();
                // Teleport ist nur erlaubt, sobald die Netzwerk-Komponente weiss, dass wir bestimmen duerfen.
                if (netz != null && netz.CanCommitToTransform) netz.Teleport(position, drehung, transform.localScale);
                Debug.Log("[Start] " + AnzeigeName + " (Client " + OwnerClientId + ", Platz " + (SpielerSlot.Value + 1) + ") stellt sich auf " + position);
            }
        }

        private bool m_StartGemeldet;

        private void LateUpdate()
        {
            if (!IsSpawned || !StartGesetzt.Value || GameControl.m_Current == null) return;
            bool gestartet = GameControl.m_Current.m_StartRace;

            if (!IsOwner)
            {
                // Fremde Panzer stehen bis zum Start auf ihrem Platz, egal was die Netzwerk-Position
                // gerade meldet. Der Platz ist allen bekannt (StartPosition), deshalb sieht jeder
                // dieselbe saubere Aufstellung, auch wenn die erste Positionsmeldung noch fehlt.
                if (!gestartet) transform.SetPositionAndRotation(StartPosition.Value, StartDrehung.Value);
                return;
            }

            // Eigener Panzer: im Moment des Starts die Position noch einmal als Sprung an alle
            // melden. Danach gilt fuer alle sicher derselbe Ausgangspunkt.
            if (gestartet && !m_StartGemeldet)
            {
                m_StartGemeldet = true;
                var netz = GetComponent<NetworkTransform>();
                if (netz != null && netz.CanCommitToTransform) netz.Teleport(transform.position, transform.rotation, transform.localScale);
            }
        }

        private void FixedUpdate()
        {
            // Bis zum Start haelt jeder seinen Panzer auf dem Startplatz fest. Ohne das kann er
            // schon vor dem Countdown verrutschen: durch die Physik, durch einen Nachbarn oder
            // weil die Position beim Spawnen und die erste Netzwerk-Meldung nicht zusammenpassen.
            if (!IsOwner || !IsSpawned || !StartGesetzt.Value) return;
            if (GameControl.m_Current == null || GameControl.m_Current.m_StartRace) return;

            bool verrutscht = (transform.position - StartPosition.Value).sqrMagnitude > 0.0004f
                || Quaternion.Angle(transform.rotation, StartDrehung.Value) > 0.2f;
            if (verrutscht) AufStartplatzSetzen(false);
        }

        // ------------------------------------------------------------------
        // Runden / Checkpoints
        // ------------------------------------------------------------------
        /// <summary>Schickt die Turmrichtung hoechstens 15-mal pro Sekunde und nur bei spuerbarer Aenderung.</summary>
        private void TurmWinkelSenden()
        {
            if (Time.unscaledTime < m_NaechsteTurmSendung) return;
            if (Mathf.Abs(Mathf.DeltaAngle(TurmWinkel.Value, m_Turm.ZielWinkel)) < 1f) return;

            m_NaechsteTurmSendung = Time.unscaledTime + 1f / 15f;
            TurmWinkel.Value = m_Turm.ZielWinkel;
        }


        /// <summary>Wie viele Zwischen-Checkpoints in dieser Runde schon in der richtigen Reihenfolge durchfahren sind.</summary>
        public int ZwischenIndex => m_ZwischenIndex;

        /// <summary>True, solange die allererste Zielueberquerung nach dem Start noch aussteht.</summary>
        public bool VorErsterUeberquerung => m_ErsteUeberquerung;

        /// <summary>
        /// Zaehlt einen Zwischen-Checkpoint nur, wenn er als naechster an der Reihe ist.
        /// Alle anderen (ausgelassen, doppelt, rueckwaerts) werden ignoriert.
        /// </summary>
        public bool CheckpointPassiert(int id)
        {
            int[] reihenfolge = RaceTrackControl.m_Main.ZwischenIds;
            if (m_ZwischenIndex >= reihenfolge.Length || reihenfolge[m_ZwischenIndex] != id) return false;

            m_ZwischenIndex++;
            m_CurrentCheckpoint = id + 1;
            return true;
        }

        /// <summary>
        /// Eine Zielueberquerung zaehlt nur, wenn seit der letzten alle Zwischen-Checkpoints an der
        /// Reihe waren. Die allererste Ueberquerung direkt nach dem Start zaehlt immer.
        /// </summary>
        public bool DarfRundeBeenden()
        {
            return m_ErsteUeberquerung || m_ZwischenIndex >= RaceTrackControl.m_Main.BenoetigteCheckpoints;
        }

        public void RundeBeendet(int gefahreneRunden)
        {
            m_ErsteUeberquerung = false;
            m_ZwischenIndex = 0;
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
                GhostManager.Instance.GhostStarten(OwnerClientId, FarbIndex.Value, punkte, k_GhostIntervall, startZeit);
        }

        // ------------------------------------------------------------------
        // Anzeige
        // ------------------------------------------------------------------

        private void OnSlotGeaendert(int alt, int neu) => FarbeAnwenden();

        private void OnStartGesetzt(bool alt, bool neu) => AufStartplatzSetzen(true);

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
            SpielerFarben.Einfaerben(gameObject, SpielerFarben.Farbe(FarbIndex.Value), false);
        }

        /// <summary>Blendet einen ausgeschiedenen Panzer bei allen aus, ohne ihn zu despawnen.</summary>
        private void SichtbarSetzen(bool sichtbar)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = sichtbar;
            foreach (var c in GetComponentsInChildren<Collider2D>(true)) c.enabled = sichtbar;
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = sichtbar;
            foreach (var a in GetComponents<AudioSource>()) a.mute = !sichtbar;

            if (IsOwner)
            {
                var physik = GetComponent<FahrPhysik>();
                if (physik != null) physik.Anhalten();
            }
        }
    }
}
