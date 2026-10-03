using UnityEngine;

namespace TopDownRace
{
    /// <summary>Fahrphysik der 2D-Rennszene (Rigidbody2D, Ebene XY, vorwaerts = lokale X-Achse).</summary>
    public class CarPhysics : FahrPhysik
    {
        [HideInInspector]
        public Rigidbody2D m_Body;

        public GameObject m_TireTracks;
        public Transform m_T_TireMarkPoint;

        // --- REIFENSPUR-SOUND (LOOP MIT FADE) ---
        [Tooltip("Der Sound-Clip, der geloopt wird, wenn Reifenspuren erzeugt werden (z.B. ein konstantes Quietsch-/Rutschgeräusch).")]
        public AudioClip m_TireTrackLoopSoundClip;
        [Tooltip("Die minimale Tonhöhe des Reifenspur-Sounds bei geringem Drift.")]
        [Range(0.1f, 2.0f)]
        public float m_TireTrackMinPitch = 0.8f;
        [Tooltip("Die maximale Tonhöhe des Reifenspur-Sounds bei starkem Drift.")]
        [Range(1.0f, 3.0f)]
        public float m_TireTrackMaxPitch = 1.8f;
        [Tooltip("Die maximale Lautstärke für den loopenden Reifenspur-Soundeffekt.")]
        [Range(0.0f, 1.0f)]
        public float m_TireTrackMaxVolume = 0.7f;
        [Tooltip("Die Geschwindigkeit, mit der der Reifenspur-Sound ein- und ausblendet.")]
        [Range(0.1f, 5.0f)]
        public float m_TireTrackFadeSpeed = 2.0f;
        private AudioSource m_TireTrackLoopAudioSource;
        private bool m_IsTireSoundActive = false;

        public override float Tempo => m_Body != null ? m_Body.linearVelocity.magnitude : 0f;

        public override void Anhalten()
        {
            if (m_Body == null) return;
            m_Body.linearVelocity = Vector2.zero;
            m_Body.angularVelocity = 0f;
        }

        public override Vector3 TurmDrehung(float grad) => new Vector3(0, 0, -grad);

        void Awake()
        {
            m_Body = GetComponent<Rigidbody2D>();
        }

        void Start()
        {
            m_TireTrackLoopAudioSource = gameObject.AddComponent<AudioSource>();
            if (m_TireTrackLoopSoundClip != null)
            {
                m_TireTrackLoopAudioSource.clip = m_TireTrackLoopSoundClip;
                m_TireTrackLoopAudioSource.loop = true;
                m_TireTrackLoopAudioSource.playOnAwake = false;
                m_TireTrackLoopAudioSource.volume = 0.0f;
                m_TireTrackLoopAudioSource.pitch = m_TireTrackMinPitch;
                m_TireTrackLoopAudioSource.Play();
            }
            else
            {
                Debug.LogWarning("Kein loopender Reifenspur-Sound-Clip zugewiesen! Der Sound wird nicht abgespielt.", this);
            }
        }

        void Update()
        {
            Vector2 velocity = m_Body.linearVelocity;
            Vector2 forward = Helper.ToVector2(transform.right);
            float driftAngle = Vector2.SignedAngle(forward, velocity);

            bool shouldSpawnTireTracks = (velocity.magnitude > 10 && Mathf.Abs(driftAngle) > 20);

            if (shouldSpawnTireTracks && m_TireTracks != null && m_T_TireMarkPoint != null)
            {
                GameObject obj = Instantiate(m_TireTracks);
                obj.transform.position = m_T_TireMarkPoint.position;
                obj.transform.rotation = m_T_TireMarkPoint.rotation;
                Destroy(obj, 2);
            }
            m_IsTireSoundActive = shouldSpawnTireTracks;

            if (m_TireTrackLoopAudioSource != null && m_TireTrackLoopAudioSource.clip != null)
            {
                if (m_IsTireSoundActive)
                {
                    m_TireTrackLoopAudioSource.volume = Mathf.MoveTowards(m_TireTrackLoopAudioSource.volume, m_TireTrackMaxVolume, m_TireTrackFadeSpeed * Time.deltaTime);

                    float driftNormalized = Mathf.Clamp01(Mathf.Abs(driftAngle) / 90f);
                    m_TireTrackLoopAudioSource.pitch = Mathf.Lerp(m_TireTrackMinPitch, m_TireTrackMaxPitch, driftNormalized);
                }
                else
                {
                    m_TireTrackLoopAudioSource.volume = Mathf.MoveTowards(m_TireTrackLoopAudioSource.volume, 0.0f, m_TireTrackFadeSpeed * Time.deltaTime);
                }
            }
        }

        void FixedUpdate()
        {
            Vector3 forward = Quaternion.Euler(0, 0, m_Body.rotation) * Vector3.right;
            m_Body.AddForce(m_InputAccelerate * m_SpeedForce * Helper.ToVector2(forward), ForceMode2D.Impulse);

            // Seitliches Rutschen daempfen
            Vector3 right = Quaternion.Euler(0, 0, 90) * forward;
            Vector3 project1 = Vector3.Project(Helper.ToVector3(m_Body.linearVelocity), right);

            m_Body.linearVelocity -= .02f * Helper.ToVector2(project1);

            m_Body.angularVelocity += 40 * m_InputSteer;

            m_InputAccelerate = 0;
            m_InputSteer = 0;
        }
    }
}
