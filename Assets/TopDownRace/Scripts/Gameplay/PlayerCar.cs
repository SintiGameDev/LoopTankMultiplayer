using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TopDownRace
{
    public class PlayerCar : MonoBehaviour
    {
        [HideInInspector]
        public float m_Speed;

        [HideInInspector]
        public int m_CurrentCheckpoint;

        [HideInInspector]
        public bool m_Control = false;

        public static PlayerCar m_Current;

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

        // --- NEUE VARIABLEN FÜR KOLLISIONSSOUNDS ---
        [Tooltip("Eine Liste von Soundeffekten, die zufällig abgespielt werden, wenn der Panzer kollidiert.")]
        public List<AudioClip> m_CollisionSoundClips;
        [Tooltip("Die maximale Lautstärke der Kollisionssounds.")]
        [Range(0.0f, 1.0f)]
        public float m_CollisionSoundVolume = 0.7f;
        private AudioSource m_CollisionAudioSource;
        // ------------------------------------------

        // --- NEUE VARIABLEN FÜR TANKTOP-DREH-SOUND ---
        [Tooltip("Das Soundfile, das abgespielt wird, wenn der TankTop gedreht wird.")]
        public AudioClip m_TankTopRotationSoundClip;
        [Tooltip("Die Lautstärke des TankTop-Rotationssounds.")]
        [Range(0.0f, 1.0f)]
        public float m_TankTopRotationVolume = 0.8f;
        [Tooltip("Die Geschwindigkeit, mit der der TankTop-Rotationssound ein-/ausblendet.")]
        [Range(0.1f, 5.0f)]
        public float m_TankTopFadeSpeed = 2.0f;
        private AudioSource m_TankTopRotationAudioSource;
        // ---------------------------------------------

        // --- NEUE VARIABLEN FÜR DEN GESCHWINDIGKEITSBOOST ---
        [Tooltip("Der Multiplikator, um den die Geschwindigkeit während des Boosts erhöht wird.")]
        public float m_BoostSpeedMultiplier = 1.5f;

        private float m_BaseSpeedForce;
        private bool m_IsBoosting = false;
        // ---------------------------------------------------

        private CarPhysics m_CarPhysics;

        private Transform m_TankTop;
        private bool m_CollisionsIgnored = false;

        void Awake()
        {
            m_Current = this;
        }

        void Start()
        {
            m_CurrentCheckpoint = 1;
            m_Control = true;
            m_Speed = 80;

            m_TankTop = transform.Find("TankTop");

            if (m_TankTop == null)
            {
                Debug.LogError("TankTop-Objekt nicht gefunden! Stelle sicher, dass ein Kindobjekt mit dem Namen 'TankTop' existiert.", this);
            }

            if (GetComponent<Rigidbody2D>() == null)
            {
                gameObject.AddComponent<Rigidbody2D>().isKinematic = true;
            }

            if (GetComponent<Collider2D>() == null)
            {
                gameObject.AddComponent<CapsuleCollider2D>().isTrigger = true;
            }

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

            m_CarPhysics = GetComponent<CarPhysics>();
            if (m_CarPhysics == null)
            {
                Debug.LogError("CarPhysics-Komponente nicht gefunden! Kann Geschwindigkeit nicht anpassen.", this);
            }
            else
            {
                m_BaseSpeedForce = m_CarPhysics.m_SpeedForce;
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (
                collision.CompareTag("Ghost")
                && !m_CollisionsIgnored
                && !HasTagInHierarchy(collision.gameObject, "CollisionIgnorer")
            )
            {
                Debug.Log("Kollision mit Ghost! Kontrolle deaktiviert und Spiel verloren.");
                m_Control = false;
                UISystem.ShowUI("lose-ui");

                var scoreText = GameObject.FindGameObjectsWithTag("Score");
                scoreText[0].GetComponent<UnityEngine.UI.Text>().text = GameControl.m_Current.m_FinishedLaps.ToString();
                GhostManager.Instance.ClearAllGhosts();

                if (m_EngineAudioSource != null && m_EngineAudioSource.isPlaying)
                    m_EngineAudioSource.Stop();

                if (m_AccelerationAudioSource != null && m_AccelerationAudioSource.isPlaying)
                    m_AccelerationAudioSource.Stop();

                if (m_TankTopRotationAudioSource != null && m_TankTopRotationAudioSource.isPlaying)
                    m_TankTopRotationAudioSource.Stop();
            }
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
            float verticalInput = Input.GetAxisRaw("Vertical");
            float horizontalInput = Input.GetAxisRaw("Horizontal");
            float tankTopRotationInput = Input.GetAxisRaw("TankTopHorizontal");

            if (GameControl.m_Current != null && GameControl.m_Current.m_StartRace)
            {
                if (m_Control)
                {
                    GetComponent<CarPhysics>().m_InputAccelerate = verticalInput;

                    if (Mathf.Abs(verticalInput) > 0.01f)
                    {
                        GetComponent<CarPhysics>().m_InputSteer = -horizontalInput * m_RotationSpeed * (verticalInput > 0 ? 1 : -1);
                    }
                    else
                    {
                        GetComponent<CarPhysics>().m_InputSteer = -horizontalInput * m_RotationSpeed;
                    }

                    // NEUE Logik für Fuel-Boost und UI-Anzeige
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

                        // Wenn der Boost nicht aktiv ist, Kraftstoff auffüllen
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
                    if (Mathf.Abs(tankTopRotationInput) > 0.01f)
                    {
                        m_TankTopRotationAudioSource.volume = Mathf.MoveTowards(m_TankTopRotationAudioSource.volume, m_TankTopRotationVolume, m_TankTopFadeSpeed * Time.deltaTime);
                    }
                    else
                    {
                        m_TankTopRotationAudioSource.volume = Mathf.MoveTowards(m_TankTopRotationAudioSource.volume, 0.0f, m_TankTopFadeSpeed * Time.deltaTime);
                    }
                }
            }

            if (m_AccelerationAudioSource != null && m_CarPhysics != null)
            {
                float currentSpeed = m_CarPhysics.GetComponent<Rigidbody2D>().linearVelocity.magnitude;
                float speedNormalized = 0f;
                if (m_Speed > 0)
                {
                    speedNormalized = Mathf.Clamp01(Mathf.Abs(currentSpeed) / m_Speed);
                }
                m_AccelerationAudioSource.pitch = Mathf.Lerp(m_MinPitch, m_MaxPitch, speedNormalized);
                m_AccelerationAudioSource.volume = Mathf.Lerp(m_MinAccelerationVolume, m_MaxAccelerationVolume, speedNormalized);
            }
        }
    }
}