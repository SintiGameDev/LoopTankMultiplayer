using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Third-Person-Kamera der 3D-Rennszene nach dem Muster von World of Tanks / War Thunder:
    ///
    /// - Die Blickrichtung gehoert allein der Maus. Die Kamera folgt der Position des Panzers,
    ///   aber nicht seiner Drehung. Lenken, Driften und Rammen verstellen den Blick nicht.
    /// - Sie kreist um einen Punkt UEBER dem Panzer. Die Bildmitte schaut dadurch ueber den
    ///   eigenen Panzer hinweg und nie auf ihn.
    /// - Zwei Markierungen: das Fadenkreuz in der Bildmitte (dorthin schaue ich) und ein Ring
    ///   (dorthin zeigt die Kanone gerade). Der Ring laeuft dem Fadenkreuz hinterher, weil der
    ///   Turm traege ist. Liegen beide uebereinander, ist die Kanone ausgerichtet.
    /// - Tempo wird spuerbar gemacht, ohne die Blickrichtung anzufassen: weiterer Blickwinkel,
    ///   mehr Abstand, leichte Schraeglage im Drift, feines Ruetteln und der Bildeffekt TempoEffekt.
    ///
    /// Erbt von CameraFollow, damit die Rennleitung (GameControl) sie genauso ansprechen kann
    /// wie die 2D-Kamera, auch beim Zuschauen nach dem Ausscheiden.
    /// Laeuft vor PanzerTurm3D, damit der Turm den Zielpunkt desselben Bildes bekommt.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class VerfolgerKamera : CameraFollow
    {
        /// <summary>Die Kamera der laufenden 3D-Szene, falls es eine gibt.</summary>
        public static VerfolgerKamera Aktiv { get; private set; }

        [Header("Third Person")]
        [Tooltip("Abstand hinter dem Panzer (waagerecht, in der Grundstellung).")]
        public float m_Abstand = 22f;
        [Tooltip("Höhe über dem Panzer (in der Grundstellung).")]
        public float m_Hoehe = 11f;
        [Tooltip("Höhe des Punktes über dem Panzer, um den die Kamera kreist und durch den die Bildmitte schaut. Muss über dem Turm liegen, sonst zeigt das Fadenkreuz auf den eigenen Panzer. 0 = automatisch.")]
        public float m_DrehpunktHoehe = 0f;

        [Header("Maus")]
        [Tooltip("Grad pro Mausbewegung, waagerecht.")]
        public float m_MausTempoX = 2.2f;
        [Tooltip("Grad pro Mausbewegung, senkrecht.")]
        public float m_MausTempoY = 1.6f;
        [Tooltip("Maus hoch/runter vertauschen.")]
        public bool m_MausYUmkehren = false;
        [Tooltip("Glättung der Mausbewegung in Sekunden. Nimmt das Zittern aus dem Blick. 0 = roh, über 0,08 fühlt es sich schwammig an.")]
        [Range(0f, 0.15f)]
        public float m_MausGlaetten = 0.04f;
        [Tooltip("Neigung nach unten beim Start und nach dem Zurücksetzen (Grad).")]
        public float m_StartNeigung = 14f;
        [Tooltip("Am weitesten nach oben (negative Werte schauen über den Horizont).")]
        public float m_NeigungOben = -12f;
        [Tooltip("Am weitesten nach unten. Zu groß, und die Bildmitte trifft wieder den eigenen Panzer.")]
        public float m_NeigungUnten = 26f;
        [Tooltip("Solange diese Taste gehalten wird, steuert die Maus den Blick frei (umsehen und zielen). Losgelassen schwenkt die Kamera wieder hinter die Wanne.")]
        public KeyCode m_FreiTaste = KeyCode.Mouse1;
        [Tooltip("Wie schnell die Kamera der Wanne folgt bzw. nach dem Loslassen zurückschwenkt. Höher = direkter.")]
        public float m_WannenFolgen = 7f;

        [Header("Gefühl")]
        [Tooltip("Trägheit der Position in Sekunden. Beim Anfahren zieht der Panzer dadurch kurz davon, beim Bremsen holt die Kamera auf. 0 = starr.")]
        public float m_PositionTraegheit = 0.09f;

        [Header("Tempo")]
        [Tooltip("Höchsttempo, falls der verfolgte Panzer keines meldet (fremde Panzer beim Zuschauen).")]
        public float m_TempoReferenz = 80f;
        [Tooltip("Zusätzlicher Blickwinkel in Grad bei Höchsttempo.")]
        public float m_TempoBlickwinkel = 10f;
        [Tooltip("Zusätzlicher Blickwinkel in Grad, solange der Boost aktiv ist.")]
        public float m_BoostBlickwinkel = 7f;
        [Tooltip("Zusätzlicher Abstand bei Höchsttempo, als Anteil des normalen Abstands.")]
        public float m_TempoAbstand = 0.10f;
        [Tooltip("Zusätzlicher Abstand im Boost.")]
        public float m_BoostAbstand = 0.07f;
        [Tooltip("Schräglage der Kamera im Drift in Grad. Die Blickrichtung bleibt dabei gleich.")]
        public float m_DriftSchraeglage = 2.5f;
        [Tooltip("Feines Rütteln der Kamera bei hohem Tempo, in Einheiten. Verschiebt nur die Position, nie die Blickrichtung.")]
        public float m_Ruetteln = 0.12f;

        [Header("Markierungen")]
        public bool m_FadenkreuzZeigen = true;
        public Color m_FadenkreuzFarbe = new Color(1f, 1f, 1f, 0.9f);
        [Tooltip("Farbe des Rings, der zeigt, wohin die Kanone gerade zeigt.")]
        public Color m_RohrMarkeFarbe = new Color(0.10f, 0.83f, 0.85f, 0.95f);
        [Tooltip("Farbe des Rings, sobald die Kanone auf das Fadenkreuz ausgerichtet ist.")]
        public Color m_RohrMarkeBereitFarbe = new Color(0.45f, 1f, 0.5f, 0.95f);

        /// <summary>Der Panzer, dem die Kamera gerade folgt.</summary>
        public Transform Ziel => m_Target;

        /// <summary>Der Punkt in der Welt, auf den die Bildmitte zeigt. Dorthin richtet sich die Kanone.</summary>
        public Vector3 ZielPunkt { get; private set; }

        private Camera m_Kamera;
        private TempoEffekt m_Effekt;
        private CarPhysics3D m_Physik;     // Fahrphysik des verfolgten Panzers, falls es der eigene ist
        private PanzerTurm3D m_Turm;
        private PlayerCar m_Panzer;

        private float m_BasisBlickwinkel;
        private Vector3 m_DrehpunktGlatt;
        private Vector3 m_DrehpunktTempo;
        private Vector3 m_LetztePosition;
        private bool m_Springen = true;

        private float m_TempoAnteil;       // 0 = steht, 1 = Hoechsttempo, darueber im Boost
        private float m_BoostAnteil;
        private float m_Schraeglage;

        private float m_GierZiel, m_Gier, m_GierTempo;                // Blickrichtung waagerecht, Welt
        private float m_NeigungZiel, m_Neigung, m_NeigungTempo;       // senkrecht, positiv = nach unten
        /// <summary>True, solange der Spieler frei umsieht (Taste gehalten).</summary>
        public bool IstFrei { get; private set; }
        private bool m_MausFrei;                                      // Spieler hat den Cursor mit Esc freigegeben

        private static Texture2D s_Ring;
        private static readonly RaycastHit[] s_Treffer = new RaycastHit[16];

        protected override void Awake()
        {
            base.Awake();
            Aktiv = this;
            m_Kamera = GetComponent<Camera>();
            if (m_Kamera != null) m_BasisBlickwinkel = m_Kamera.fieldOfView;
            m_Neigung = m_NeigungZiel = m_StartNeigung;

            // Aeltere Szenen haben den Bildeffekt noch nicht an der Kamera.
            m_Effekt = GetComponent<TempoEffekt>();
            if (m_Effekt == null && m_Kamera != null) m_Effekt = gameObject.AddComponent<TempoEffekt>();
        }

        private void OnEnable()
        {
            Aktiv = this;
        }

        private void OnDisable()
        {
            // Im Menue und in den Ergebnis-Bildschirmen braucht man den Cursor wieder.
            CursorFangen(false);
            if (Aktiv == this) Aktiv = null;
        }

        public override void SetTarget(Transform newTarget)
        {
            if (m_Target != newTarget) m_Springen = true;   // bei neuem Ziel hinter dessen Wanne beginnen
            base.SetTarget(newTarget);

            m_Physik = newTarget != null ? newTarget.GetComponent<CarPhysics3D>() : null;
            m_Turm = newTarget != null ? newTarget.GetComponent<PanzerTurm3D>() : null;
            m_Panzer = newTarget != null ? newTarget.GetComponent<PlayerCar>() : null;
        }

        // Die 2D-Logik der Basisklasse wird hier bewusst nicht ausgefuehrt.
        protected override void FixedUpdate() { }

        private float DrehpunktHoehe => m_DrehpunktHoehe > 0f ? m_DrehpunktHoehe : m_Hoehe * 0.65f;

        private void LateUpdate()
        {
            MausVerwalten();
            if (m_Target == null) return;

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);

            TempoMessen(dt);
            BlickAktualisieren(dt);

            Quaternion blick = Quaternion.Euler(m_Neigung, m_Gier, 0f);

            // --- Position: im Abstand hinter einem Punkt ueber dem Panzer ---
            Vector3 drehpunkt = m_Target.position + Vector3.up * DrehpunktHoehe;
            m_DrehpunktGlatt = (m_Springen || m_PositionTraegheit <= 0f)
                ? drehpunkt
                : Vector3.SmoothDamp(m_DrehpunktGlatt, drehpunkt, ref m_DrehpunktTempo, m_PositionTraegheit);

            float tempo01 = Mathf.Clamp01(m_TempoAnteil);
            float strecke = Mathf.Sqrt(m_Abstand * m_Abstand + m_Hoehe * m_Hoehe);
            strecke *= 1f + m_TempoAbstand * Mathf.SmoothStep(0f, 1f, tempo01) + m_BoostAbstand * m_BoostAnteil;

            // Erst die ruhige Stellung einnehmen und damit zielen ...
            transform.SetPositionAndRotation(m_DrehpunktGlatt - blick * Vector3.forward * strecke, blick);
            ZielPunktBestimmen();

            // ... dann die Effekte, die die Blickrichtung nicht veraendern: Schraeglage dreht nur
            // um die Blickachse, das Ruetteln verschiebt nur die Position.
            float schlupf = m_Physik != null && m_Physik.isActiveAndEnabled ? m_Physik.Schlupf : 0f;
            float zielSchraeglage = Mathf.Clamp(-schlupf * 4f, -1f, 1f) * m_DriftSchraeglage;
            m_Schraeglage = m_Springen ? 0f : Mathf.Lerp(m_Schraeglage, zielSchraeglage, 1f - Mathf.Exp(-5f * dt));

            float ruetteln = m_Ruetteln * (Mathf.InverseLerp(0.7f, 1.1f, m_TempoAnteil) + 0.8f * m_BoostAnteil);
            Vector3 versatz = Vector3.zero;
            if (ruetteln > 0.0001f)
            {
                float z = Time.time * 23f;
                versatz = blick * new Vector3(Mathf.PerlinNoise(z, 0.3f) - 0.5f, Mathf.PerlinNoise(0.7f, z) - 0.5f, 0f) * (2f * ruetteln);
            }
            transform.SetPositionAndRotation(transform.position + versatz, blick * Quaternion.Euler(0f, 0f, m_Schraeglage));

            if (m_Kamera != null)
            {
                m_Kamera.fieldOfView = m_BasisBlickwinkel
                    + m_TempoBlickwinkel * Mathf.SmoothStep(0f, 1f, tempo01)
                    + m_BoostBlickwinkel * m_BoostAnteil;
            }

            if (m_Effekt != null)
            {
                m_Effekt.TempoAnteil = m_TempoAnteil;
                m_Effekt.BoostAnteil = m_BoostAnteil;
                m_Effekt.DriftAnteil = m_Physik != null && m_Physik.isActiveAndEnabled ? m_Physik.DriftAnteil : 0f;
            }

            m_Springen = false;
        }

        private void TempoMessen(float dt)
        {
            float roh;
            bool boost = false;
            if (m_Physik != null && m_Physik.isActiveAndEnabled)
            {
                // Eigener Panzer: sauberer Wert direkt aus der Fahrphysik.
                roh = m_Physik.Tempo / Mathf.Max(1f, m_Physik.m_Hoechsttempo);
                boost = m_Physik.BoostAktiv && roh > 0.3f;
            }
            else
            {
                // Fremder Panzer (Zuschauen): aus der Bewegung ableiten.
                float tempo = m_Springen ? 0f : Vector3.Distance(m_Target.position, m_LetztePosition) / dt;
                roh = tempo / Mathf.Max(1f, m_TempoReferenz);
            }
            m_LetztePosition = m_Target.position;
            roh = Mathf.Clamp(roh, 0f, 1.6f);

            if (m_Springen)
            {
                m_TempoAnteil = 0f;
                m_BoostAnteil = 0f;
                return;
            }
            m_TempoAnteil = Mathf.Lerp(m_TempoAnteil, roh, 1f - Mathf.Exp(-5f * dt));
            m_BoostAnteil = Mathf.MoveTowards(m_BoostAnteil, boost ? 1f : 0f, dt / (boost ? 0.25f : 0.6f));
        }

        /// <summary>Blickrichtung: nur die Maus, leicht geglaettet.</summary>
        private void BlickAktualisieren(float dt)
        {
            if (m_Springen)
            {
                m_Gier = m_GierZiel = m_Target.eulerAngles.y;
                m_Neigung = m_NeigungZiel = m_StartNeigung;
                m_GierTempo = m_NeigungTempo = 0f;
            }

            // Frei umsehen und zielen nur, solange die Taste (rechte Maustaste) gehalten wird.
            // Sonst bleibt die Kamera hinter der Wanne und dreht in Kurven mit.
            IstFrei = Cursor.lockState == CursorLockMode.Locked && Input.GetKey(m_FreiTaste);

            if (IstFrei)
            {
                float mx = Input.GetAxis("Mouse X") * m_MausTempoX;
                float my = Input.GetAxis("Mouse Y") * m_MausTempoY * (m_MausYUmkehren ? 1f : -1f);
                m_GierZiel += mx;
                m_NeigungZiel = Mathf.Clamp(m_NeigungZiel + my, m_NeigungOben, m_NeigungUnten);
            }
            else
            {
                // Zurueck hinter die Wanne: nach dem Loslassen zuegig, beim normalen Fahren weich mit der Lenkung.
                float anteil = 1f - Mathf.Exp(-m_WannenFolgen * dt);
                m_GierZiel = Mathf.LerpAngle(m_GierZiel, m_Target.eulerAngles.y, anteil);
                m_NeigungZiel = Mathf.Lerp(m_NeigungZiel, m_StartNeigung, anteil);
            }

            if (m_MausGlaetten <= 0f)
            {
                m_Gier = m_GierZiel;
                m_Neigung = m_NeigungZiel;
            }
            else
            {
                m_Gier = Mathf.SmoothDampAngle(m_Gier, m_GierZiel, ref m_GierTempo, m_MausGlaetten, Mathf.Infinity, dt);
                m_Neigung = Mathf.SmoothDamp(m_Neigung, m_NeigungZiel, ref m_NeigungTempo, m_MausGlaetten, Mathf.Infinity, dt);
            }
        }

        /// <summary>
        /// Sucht den Punkt, auf den die Bildmitte zeigt: die erste Wand oder der erste Panzer in
        /// Blickrichtung, sonst der Boden, sonst ein weit entfernter Punkt.
        /// </summary>
        private void ZielPunktBestimmen()
        {
            Vector3 ursprung = transform.position;
            Vector3 richtung = transform.forward;
            const float Weit = 1500f;

            float naechster = Weit;

            // Boden: die Ebene, auf der der Panzer faehrt (der Boden selbst hat keinen Collider).
            if (richtung.y < -0.01f)
            {
                float bisBoden = (m_Target.position.y - ursprung.y) / richtung.y;
                if (bisBoden > 0f) naechster = Mathf.Min(naechster, bisBoden);
            }

            int anzahl = Physics.RaycastNonAlloc(ursprung, richtung, s_Treffer, naechster, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < anzahl; i++)
            {
                // Der eigene Panzer zaehlt nicht als Ziel.
                if (s_Treffer[i].transform.IsChildOf(m_Target)) continue;
                if (s_Treffer[i].distance < naechster) naechster = s_Treffer[i].distance;
            }

            ZielPunkt = ursprung + richtung * naechster;
        }

        // ------------------------------------------------------------------
        // Cursor
        // ------------------------------------------------------------------

        private void MausVerwalten()
        {
            // Gefangen wird der Cursor nur waehrend Countdown und Rennen. Davor und danach
            // (Ergebnis-Bildschirm mit Knoepfen) ist er frei.
            var leitung = GameControl.m_Current;
            bool imRennen = leitung != null && leitung.IsSpawned
                && (leitung.Phase.Value == RennPhase.Countdown || leitung.Phase.Value == RennPhase.Rennen);

            if (!imRennen) m_MausFrei = false;
            else if (Input.GetKeyDown(KeyCode.Escape)) m_MausFrei = true;
            else if (m_MausFrei && Input.GetMouseButtonDown(0)) m_MausFrei = false;

            CursorFangen(imRennen && !m_MausFrei && Application.isFocused);
        }

        private static void CursorFangen(bool fangen)
        {
            CursorLockMode modus = fangen ? CursorLockMode.Locked : CursorLockMode.None;
            if (Cursor.lockState != modus) Cursor.lockState = modus;
            if (Cursor.visible == fangen) Cursor.visible = !fangen;
        }

        // ------------------------------------------------------------------
        // Markierungen
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!m_FadenkreuzZeigen || m_Kamera == null || Event.current.type != EventType.Repaint) return;
            if (Cursor.lockState != CursorLockMode.Locked) return;

            float mitteX = Screen.width * 0.5f, mitteY = Screen.height * 0.5f;
            float einheit = Mathf.Max(1f, Screen.height / 1080f);
            Color vorher = GUI.color;

            // Ring: wohin die Kanone des eigenen Panzers gerade zeigt.
            if (m_Turm != null && m_Panzer != null && m_Panzer.IsOwner && m_Panzer.Lebt.Value)
            {
                Vector3 schirm = m_Kamera.WorldToScreenPoint(m_Turm.RohrZielPunkt);
                if (schirm.z > 0f)
                {
                    float gross = 34f * einheit;
                    float abstand = Vector2.Distance(new Vector2(schirm.x, schirm.y), new Vector2(mitteX, mitteY));
                    GUI.color = abstand < 10f * einheit ? m_RohrMarkeBereitFarbe : m_RohrMarkeFarbe;
                    GUI.DrawTexture(new Rect(schirm.x - gross * 0.5f, Screen.height - schirm.y - gross * 0.5f, gross, gross), Ring());
                }
            }

            // Fadenkreuz: dorthin schaue ich.
            float lang = 9f * einheit, dick = Mathf.Max(2f, 2f * einheit), luecke = 5f * einheit;
            GUI.color = m_FadenkreuzFarbe;
            GUI.DrawTexture(new Rect(mitteX - luecke - lang, mitteY - dick * 0.5f, lang, dick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(mitteX + luecke, mitteY - dick * 0.5f, lang, dick), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(mitteX - dick * 0.5f, mitteY - luecke - lang, dick, lang), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(mitteX - dick * 0.5f, mitteY + luecke, dick, lang), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(mitteX - dick * 0.5f, mitteY - dick * 0.5f, dick, dick), Texture2D.whiteTexture);
            GUI.color = vorher;
        }

        /// <summary>Kleine Ring-Textur, einmal erzeugt.</summary>
        private static Texture2D Ring()
        {
            if (s_Ring != null) return s_Ring;

            const int groesse = 64;
            s_Ring = new Texture2D(groesse, groesse, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            float mitte = (groesse - 1) * 0.5f, aussen = groesse * 0.47f, innen = groesse * 0.38f;
            for (int y = 0; y < groesse; y++)
            {
                for (int x = 0; x < groesse; x++)
                {
                    float r = Mathf.Sqrt((x - mitte) * (x - mitte) + (y - mitte) * (y - mitte));
                    float alpha = Mathf.Clamp01(aussen - r) * Mathf.Clamp01(r - innen);
                    s_Ring.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            s_Ring.Apply();
            return s_Ring;
        }
    }
}
