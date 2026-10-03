using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Turm und Kanone des 3D-Panzers.
    ///
    /// 1. Zielen: Der Turm dreht sich schwerfaellig auf den Punkt, auf den das Fadenkreuz zeigt
    ///    (eigener Panzer) bzw. auf den uebertragenen Winkel (fremde Panzer). Die Kanone hebt
    ///    und senkt sich passend.
    /// 2. Gewicht: Beim Anfahren, Bremsen und in Kurven schwingen Turm und Kanone der Wanne
    ///    hinterher, jeweils mit eigener Feder. Die Kanone ist weicher gefedert als der Turm,
    ///    dadurch bewegen sich Wanne, Turm und Kanone sichtbar versetzt zueinander.
    /// 3. Kein Durchdringen: Kippt der Turm, hebt er sich so weit an, dass seine Kanten nicht in
    ///    die Wanne tauchen. Die Kanone senkt sich nie tiefer, als das Wannendeck unter ihr erlaubt.
    ///
    /// Alles hier ist Darstellung. Collider und Fahrphysik bleiben unberuehrt.
    /// Laeuft nach der Kamera, damit der Zielpunkt desselben Bildes benutzt wird.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class PanzerTurm3D : MonoBehaviour
    {
        [Header("Zielen")]
        [Tooltip("Höchste Drehgeschwindigkeit des Turms in Grad pro Sekunde.")]
        public float m_DrehTempo = 170f;
        [Tooltip("Trägheit des Turms in Sekunden. Höher = er kommt später in Gang und läuft länger nach.")]
        public float m_DrehTraegheit = 0.14f;
        [Tooltip("Wie weit sich die Kanone heben lässt (Grad).")]
        public float m_RohrHoch = 14f;
        [Tooltip("Wie weit sich die Kanone senken lässt (Grad). Über der Wanne wird zusätzlich automatisch begrenzt.")]
        public float m_RohrRunter = 6f;
        [Tooltip("Trägheit der Kanone beim Heben und Senken in Sekunden.")]
        public float m_RohrTraegheit = 0.18f;

        [Header("Gewicht: Turm")]
        [Tooltip("Beschleunigung der Wanne, bei der die Ausschläge unten voll erreicht werden (Einheiten pro Sekunde²).")]
        public float m_ReferenzBeschleunigung = 180f;
        [Tooltip("Wie weit der Turm beim Anfahren und Bremsen kippt (Grad).")]
        public float m_TurmKippen = 2.5f;
        [Tooltip("Wie weit der Turm gegen die Bewegung verrutscht, als Anteil der Kanonenlänge.")]
        public float m_TurmVersatz = 0.04f;
        [Tooltip("Schwingungen pro Sekunde. Niedriger = schwerer.")]
        public float m_TurmFrequenz = 2.2f;
        [Tooltip("0 = schwingt lange nach, 1 = kommt ohne Nachschwingen zur Ruhe.")]
        [Range(0.05f, 1.5f)]
        public float m_TurmDaempfung = 0.55f;

        [Header("Gewicht: Kanone")]
        [Tooltip("Wie weit die Kanone beim Anfahren und Bremsen wippt (Grad).")]
        public float m_RohrWippen = 5f;
        [Tooltip("Schwingungen pro Sekunde. Niedriger als beim Turm, damit die Kanone später reagiert.")]
        public float m_RohrFrequenz = 1.5f;
        [Tooltip("0 = wippt lange nach, 1 = kommt ohne Nachwippen zur Ruhe.")]
        [Range(0.05f, 1.5f)]
        public float m_RohrDaempfung = 0.35f;

        [Header("Ruhe")]
        [Tooltip("Wie stark die gemessene Beschleunigung geglättet wird (pro Sekunde). Niedriger = ruhiger, aber träger.")]
        public float m_BeschleunigungGlaetten = 8f;
        [Tooltip("Abstand, den die Kanone mindestens über dem Wannendeck hält.")]
        public float m_RohrLuft = 0.15f;

        /// <summary>Wohin der Turm zeigen soll (Welt-Drehung um Y in Grad).</summary>
        [HideInInspector]
        public float ZielWinkel;

        /// <summary>Punkt, auf den die Kanone gerade tatsaechlich zeigt (fuer die Markierung und spaeter zum Schiessen).</summary>
        public Vector3 RohrZielPunkt { get; private set; }

        /// <summary>0..1, wie schnell sich der Turm gerade dreht (fuer den Dreh-Sound).</summary>
        public float DrehAnteil { get; private set; }

        private struct Feder
        {
            public float Wert, Tempo;

            public void Schritt(float ziel, float frequenz, float daempfung, float dt)
            {
                float w = 2f * Mathf.PI * Mathf.Max(0.1f, frequenz);
                Tempo += (w * w * (ziel - Wert) - 2f * daempfung * w * Tempo) * dt;
                Wert += Tempo * dt;
            }
        }

        private Transform m_Turm;
        private Transform m_RohrGelenk;
        private Rigidbody m_Body;
        private PlayerCar m_Panzer;

        // Abmessungen, einmal beim Start gelesen (alle im Raum der Wanne)
        private Vector3 m_TurmBasisPosition;
        private Vector3 m_GelenkImTurm;
        private Vector2 m_TurmHalb = new Vector2(1f, 1f);       // halbe Breite, halbe Laenge des Turmkoerpers
        private Vector2 m_WanneHalb = new Vector2(1f, 1f);      // halbe Breite, halbe Laenge der Wanne
        private Vector2 m_WanneMitte;
        private float m_RohrLaenge = 1f;
        private float m_RohrRadius = 0.1f;

        private float m_Winkel;
        private float m_WinkelTempo;
        private float m_Erhoehung;
        private float m_ErhoehungTempo;
        private float m_ZielEntfernung = 100f;

        // Beschleunigung: im Physik-Takt gemessen, fuer die Darstellung geglaettet
        private Vector3 m_LetztePhysikPosition;
        private Vector3 m_LetztesTempo;
        private Vector3 m_BeschleunigungPhysik;
        private Vector3 m_Beschleunigung;
        private bool m_Erstes = true;

        private Feder m_KippenVor, m_KippenSeite, m_VersatzVor, m_VersatzSeite, m_Wippen;

        private void Start()
        {
            m_Panzer = GetComponent<PlayerCar>();
            m_Body = GetComponent<Rigidbody>();

            Transform wanne = null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (m_Turm == null && t.name == "TankTop") m_Turm = t;
                if (wanne == null && t.name == "TankBody") wanne = t;
            }
            if (m_Turm == null)
            {
                Debug.LogWarning("[PanzerTurm3D] Kein Kind namens 'TankTop' gefunden, Turmsteuerung ist aus.", this);
                enabled = false;
                return;
            }
            m_TurmBasisPosition = m_Turm.localPosition;

            if (wanne != null)
            {
                m_WanneHalb = new Vector2(wanne.localScale.x, wanne.localScale.z) * 0.5f;
                m_WanneMitte = new Vector2(wanne.localPosition.x, wanne.localPosition.z);
            }

            Transform koerper = m_Turm.Find("Turm");
            if (koerper != null) m_TurmHalb = new Vector2(koerper.localScale.x, koerper.localScale.z) * 0.5f;

            // Die Kanone bekommt ein Gelenk an ihrem hinteren Ende, damit sie um den Turm kippt
            // und nicht um ihre eigene Mitte.
            Transform rohr = m_Turm.Find("Rohr");
            if (rohr != null)
            {
                m_RohrLaenge = Mathf.Max(0.1f, rohr.localScale.z);
                m_RohrRadius = Mathf.Max(rohr.localScale.x, rohr.localScale.y) * 0.5f;
                m_GelenkImTurm = new Vector3(rohr.localPosition.x, rohr.localPosition.y, rohr.localPosition.z - m_RohrLaenge * 0.5f);

                m_RohrGelenk = new GameObject("RohrGelenk").transform;
                m_RohrGelenk.SetParent(m_Turm, false);
                m_RohrGelenk.localPosition = m_GelenkImTurm;
                rohr.SetParent(m_RohrGelenk, true);
            }

            m_Winkel = ZielWinkel = transform.eulerAngles.y;
        }

        /// <summary>
        /// Beschleunigung im Physik-Takt messen. Aus den gerenderten Positionen abgeleitet waere sie
        /// verrauscht, weil Bildrate und Physik-Takt nicht zusammenpassen: Turm und Kanone wuerden zittern.
        /// </summary>
        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector3 position = m_Body != null ? m_Body.position : transform.position;

            Vector3 tempo;
            if (m_Body != null && !m_Body.isKinematic)
            {
                tempo = m_Body.linearVelocity;                       // eigener Panzer
            }
            else
            {
                if (m_Erstes) m_LetztePhysikPosition = position;
                tempo = (position - m_LetztePhysikPosition) / dt;    // fremder Panzer, kommt ueber das Netz
                if (tempo.magnitude > 1000f) tempo = m_LetztesTempo; // Sprung (Spawn) nicht als Stoss werten
            }
            m_LetztePhysikPosition = position;

            if (m_Erstes)
            {
                m_LetztesTempo = tempo;
                m_Erstes = false;
            }

            Vector3 roh = (tempo - m_LetztesTempo) / dt;
            m_LetztesTempo = tempo;
            m_BeschleunigungPhysik = Vector3.Lerp(m_BeschleunigungPhysik, roh, 0.3f);
            m_BeschleunigungPhysik = Vector3.ClampMagnitude(m_BeschleunigungPhysik, m_ReferenzBeschleunigung * 2f);
        }

        private void LateUpdate()
        {
            if (m_Turm == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (dt <= 0f) return;

            m_Beschleunigung = Vector3.Lerp(m_Beschleunigung, m_BeschleunigungPhysik, 1f - Mathf.Exp(-m_BeschleunigungGlaetten * dt));

            Zielen(dt);
            GewichtAnwenden(dt);
        }

        /// <summary>
        /// Fester Bezugspunkt fuer das Zielen: dort, wo das Kanonengelenk ohne Federn waere.
        /// Wuerde man vom wippenden Gelenk aus zielen, schaukelte sich das Zittern selbst auf.
        /// </summary>
        private Vector3 ZielUrsprung()
        {
            Vector3 lokal = m_TurmBasisPosition + Quaternion.Euler(0f, Mathf.DeltaAngle(transform.eulerAngles.y, m_Winkel), 0f) * m_GelenkImTurm;
            return transform.position + Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * lokal;
        }

        private void Zielen(float dt)
        {
            // Eigener Panzer: auf den Punkt, auf den das Fadenkreuz zeigt. Fremde: ZielWinkel kommt von PlayerCar.
            var kamera = VerfolgerKamera.Aktiv;
            bool eigener = m_Panzer != null && m_Panzer.IsOwner;
            float erhoehungZiel = 0f;
            Vector3 ursprung = ZielUrsprung();

            if (eigener && kamera != null && kamera.Ziel == transform && m_Panzer.Lebt.Value)
            {
                Vector3 zum = kamera.ZielPunkt - ursprung;
                float flach = new Vector2(zum.x, zum.z).magnitude;

                // Die Entfernung springt, wenn der Blick von einer Wand auf den Boden wechselt.
                // Geglaettet bleibt die Kanonen-Markierung ruhig.
                m_ZielEntfernung = Mathf.Lerp(m_ZielEntfernung, Mathf.Max(m_RohrLaenge * 2f, zum.magnitude), 1f - Mathf.Exp(-10f * dt));

                // Sehr nah am Panzer wuerde der Turm bei jeder kleinen Bewegung herumschlagen.
                if (flach > m_RohrLaenge)
                {
                    ZielWinkel = Mathf.Atan2(zum.x, zum.z) * Mathf.Rad2Deg;
                    erhoehungZiel = Mathf.Clamp(Mathf.Atan2(zum.y, flach) * Mathf.Rad2Deg, -m_RohrRunter, m_RohrHoch);
                }
            }

            float vorher = m_Winkel;
            m_Winkel = Mathf.SmoothDampAngle(m_Winkel, ZielWinkel, ref m_WinkelTempo, m_DrehTraegheit, m_DrehTempo, dt);
            DrehAnteil = Mathf.Clamp01(Mathf.Abs(Mathf.DeltaAngle(vorher, m_Winkel)) / dt / Mathf.Max(1f, m_DrehTempo));

            m_Erhoehung = Mathf.SmoothDamp(m_Erhoehung, erhoehungZiel, ref m_ErhoehungTempo, m_RohrTraegheit, 90f, dt);

            // Wohin die Kanone tatsaechlich zeigt, in der Entfernung des anvisierten Punktes.
            // Ohne Federn, sonst wuerde die Markierung bei jedem Anfahren tanzen.
            Vector3 rohrRichtung = Quaternion.Euler(-m_Erhoehung, m_Winkel, 0f) * Vector3.forward;
            RohrZielPunkt = ZielUrsprung() + rohrRichtung * m_ZielEntfernung;
        }

        private void GewichtAnwenden(float dt)
        {
            // Beschleunigung aus Sicht des Turms: z = in Blickrichtung des Turms, x = nach rechts.
            Vector3 imTurm = Quaternion.Euler(0f, -m_Winkel, 0f) * m_Beschleunigung;
            float vor = Mathf.Clamp(imTurm.z / Mathf.Max(1f, m_ReferenzBeschleunigung), -1.5f, 1.5f);
            float seite = Mathf.Clamp(imTurm.x / Mathf.Max(1f, m_ReferenzBeschleunigung), -1.5f, 1.5f);

            // Masse bleibt zurueck: beim Anfahren kippt der Turm nach hinten, beim Bremsen nach vorn.
            m_KippenVor.Schritt(-vor * m_TurmKippen, m_TurmFrequenz, m_TurmDaempfung, dt);
            m_KippenSeite.Schritt(seite * m_TurmKippen, m_TurmFrequenz, m_TurmDaempfung, dt);
            m_VersatzVor.Schritt(-vor * m_TurmVersatz * m_RohrLaenge, m_TurmFrequenz, m_TurmDaempfung, dt);
            m_VersatzSeite.Schritt(-seite * m_TurmVersatz * m_RohrLaenge, m_TurmFrequenz, m_TurmDaempfung, dt);

            // Die Kanone haengt weicher: sie reagiert spaeter und wippt laenger nach.
            m_Wippen.Schritt(-vor * m_RohrWippen, m_RohrFrequenz, m_RohrDaempfung, dt);

            float lokalerWinkel = Mathf.DeltaAngle(transform.eulerAngles.y, m_Winkel);
            Quaternion drehung = Quaternion.Euler(0f, lokalerWinkel, 0f);

            // Ein gekippter Kasten taucht mit seiner unteren Kante ein. Um genau diesen Betrag
            // hebt sich der Turm, er rollt also auf seiner Kante ab statt in die Wanne zu sinken.
            float heben = m_TurmHalb.y * Mathf.Abs(Mathf.Sin(m_KippenVor.Wert * Mathf.Deg2Rad))
                        + m_TurmHalb.x * Mathf.Abs(Mathf.Sin(m_KippenSeite.Wert * Mathf.Deg2Rad));

            Vector3 versatz = drehung * new Vector3(m_VersatzSeite.Wert, 0f, m_VersatzVor.Wert);
            m_Turm.localRotation = drehung * Quaternion.Euler(m_KippenVor.Wert, 0f, m_KippenSeite.Wert);
            m_Turm.localPosition = m_TurmBasisPosition + versatz + Vector3.up * heben;

            if (m_RohrGelenk != null)
            {
                // Positive X-Drehung senkt die Muendung. Erlaubt ist nur, was ueber dem Deck Platz hat.
                float senken = -m_Erhoehung + m_Wippen.Wert;
                float erlaubt = MaxSenkung(lokalerWinkel, versatz, heben) - Mathf.Max(0f, m_KippenVor.Wert);
                m_RohrGelenk.localRotation = Quaternion.Euler(Mathf.Min(senken, Mathf.Max(0f, erlaubt)), 0f, 0f);
            }
        }

        /// <summary>
        /// Wie weit sich die Kanone in der aktuellen Turmstellung senken darf, ohne das Wannendeck
        /// zu beruehren. Zeigt sie ueber die Wannenkante hinaus, zaehlt nur das Stueck ueber dem Deck.
        /// </summary>
        private float MaxSenkung(float lokalerWinkel, Vector3 versatz, float heben)
        {
            // Hoehe der Kanonen-Unterseite ueber dem Deck (das Deck liegt auf Hoehe der Turmbasis).
            float luft = m_GelenkImTurm.y + heben - m_RohrRadius - m_RohrLuft;
            if (luft <= 0f) return 0f;

            // Gelenk und Richtung der Kanone in der Ebene der Wanne
            Quaternion drehung = Quaternion.Euler(0f, lokalerWinkel, 0f);
            Vector3 gelenk3 = m_TurmBasisPosition + versatz + drehung * m_GelenkImTurm;
            Vector2 gelenk = new Vector2(gelenk3.x, gelenk3.z) - m_WanneMitte;
            Vector3 richtung3 = drehung * Vector3.forward;
            Vector2 richtung = new Vector2(richtung3.x, richtung3.z);

            // Strecke vom Gelenk bis zur Wannenkante in Kanonenrichtung
            float bisKante = float.MaxValue;
            if (Mathf.Abs(richtung.x) > 0.0001f)
                bisKante = Mathf.Min(bisKante, (Mathf.Sign(richtung.x) * m_WanneHalb.x - gelenk.x) / richtung.x);
            if (Mathf.Abs(richtung.y) > 0.0001f)
                bisKante = Mathf.Min(bisKante, (Mathf.Sign(richtung.y) * m_WanneHalb.y - gelenk.y) / richtung.y);

            float ueberDeck = Mathf.Clamp(bisKante, 0.05f, m_RohrLaenge);
            return Mathf.Asin(Mathf.Clamp01(luft / ueberDeck)) * Mathf.Rad2Deg;
        }
    }
}
