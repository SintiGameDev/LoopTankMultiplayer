using Unity.Netcode.Components;
using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Fahrphysik der 3D-Rennszene: Arcade-Steuerung fuer die Third-Person-Ansicht.
    /// Der Panzer faehrt auf der Ebene XZ (Y ist oben, vorwaerts = lokale Z-Achse).
    ///
    /// Statt Kraefte gegen Daempfung arbeiten zu lassen (wie die 2D-Version), wird die
    /// Geschwindigkeit direkt gefuehrt: Zielgeschwindigkeit und Ziel-Drehrate werden mit festen
    /// Raten angefahren. Das macht das Verhalten vorhersehbar und jede Zahl einzeln einstellbar.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarPhysics3D : FahrPhysik
    {
        [HideInInspector]
        public Rigidbody m_Body;

        [Header("Tempo")]
        [Tooltip("Höchstgeschwindigkeit vorwärts in Einheiten pro Sekunde.")]
        public float m_Hoechsttempo = 80f;
        [Tooltip("Höchstgeschwindigkeit rückwärts.")]
        public float m_Rueckwaertstempo = 40f;
        [Tooltip("Beschleunigung beim Gasgeben. Höher = der Panzer ist schneller auf Tempo.")]
        public float m_Beschleunigung = 110f;
        [Tooltip("Verzögerung, wenn man gegen die Fahrtrichtung drückt. Höher = härteres Bremsen und schnelleres Umkehren.")]
        public float m_Bremskraft = 260f;
        [Tooltip("Verzögerung beim Loslassen der Tasten.")]
        public float m_Ausrollen = 70f;
        [Tooltip("Faktor auf Höchsttempo und Beschleunigung, solange der Boost aktiv ist.")]
        public float m_BoostFaktor = 1.4f;

        [Header("Lenkung")]
        [Tooltip("Drehrate im Stand in Grad pro Sekunde. Ein Panzer kann auf der Stelle drehen.")]
        public float m_DrehrateStand = 130f;
        [Tooltip("Drehrate bei Höchsttempo. Kleiner als im Stand, damit der Panzer bei Tempo ruhig bleibt.")]
        public float m_DrehrateSchnell = 85f;
        [Tooltip("Wie schnell die Lenkung anspricht (Grad pro Sekunde²). Höher = direkter, niedriger = weicher.")]
        public float m_LenkAnsprechen = 700f;
        [Tooltip("An: Beim Rückwärtsfahren lenkt der Panzer wie ein Auto (links/rechts vertauscht). Aus: Die Drehrichtung bleibt immer gleich.")]
        public bool m_RueckwaertsWieAuto = true;

        [Header("Haftung")]
        [Tooltip("Wie schnell seitliches Rutschen abgebaut wird (pro Sekunde). Hoch = der Panzer fährt wie auf Schienen, niedrig = er driftet.")]
        public float m_Seitenhalt = 9f;

        [Header("Drift (Leertaste halten)")]
        [Tooltip("Seitenhalt während des Drifts. Kleiner = der Panzer rutscht länger in die alte Richtung.")]
        public float m_DriftSeitenhalt = 1.3f;
        [Tooltip("Faktor auf die Drehrate während des Drifts. Damit dreht die Nase schneller in die Kurve.")]
        public float m_DriftDrehFaktor = 1.55f;
        [Tooltip("Sekunden, bis der Drift nach dem Drücken voll wirkt. Verhindert einen Ruck.")]
        public float m_DriftEinblenden = 0.18f;
        [Tooltip("Sekunden, bis nach dem Loslassen der volle Halt zurück ist. Länger = weicheres Abfangen.")]
        public float m_DriftAusblenden = 0.45f;
        [Tooltip("Größter Winkel zwischen Fahrtrichtung und Nase in Grad. Begrenzt den Drift, damit er kontrollierbar bleibt.")]
        public float m_DriftMaxWinkel = 50f;
        [Tooltip("Anteil des Tempos, der pro Sekunde im vollen Drift verloren geht.")]
        public float m_DriftTempoVerlust = 0.12f;

        [Header("Höhe (nur auf erzeugten Strecken)")]
        [Tooltip("Fallbeschleunigung in der Luft. Höher = kürzere, flachere Sprünge über Kuppen und Buckel.")]
        public float m_Schwerkraft = 80f;

        [Header("Optik")]
        [Tooltip("Wie schnell sich die Wanne der Neigung der Fahrbahn anpasst (Rampen, Buckel).")]
        public float m_HangTempo = 12f;
        [Tooltip("Maximale Seitenneigung in Kurven in Grad. Rein optisch.")]
        public float m_NeigungKurve = 3f;
        [Tooltip("Maximales Nicken beim Beschleunigen und Bremsen in Grad. Rein optisch.")]
        public float m_NeigungGas = 2.5f;
        [Tooltip("Wie schnell die Neigung der Bewegung folgt.")]
        public float m_NeigungTempo = 7f;

        private float m_BasisSpeedForce;
        private float m_DrehrateAktuell;     // Grad pro Sekunde, positiv = im Uhrzeigersinn von oben
        private float m_LaengsBeschleunigung; // fuer das Nicken
        private float m_DriftAnteil;          // 0 = voller Halt, 1 = voller Drift
        private Transform m_Optik;
        private float m_Rollen;
        private float m_Nicken;
        private float m_Steigen;              // Geschwindigkeit nach oben, solange der Panzer in der Luft ist
        private float m_HangNicken;
        private float m_HangRollen;

        /// <summary>False, solange der Panzer nach einer Kuppe oder einem Buckel in der Luft ist.</summary>
        public bool AmBoden { get; private set; } = true;

        public override float Tempo => m_Body != null ? m_Body.linearVelocity.magnitude : 0f;

        public override bool KannDriften => true;

        /// <summary>0..1, wie stark der Panzer gerade driftet (fuer Effekte).</summary>
        public float DriftAnteil => m_DriftAnteil;

        /// <summary>True, solange der Boost wirkt (fuer Kamera und Effekte).</summary>
        public bool BoostAktiv => m_SpeedForce > m_BasisSpeedForce * 1.01f;

        /// <summary>Seitliches Rutschen als Anteil des Hoechsttempos, positiv = nach rechts (fuer Kamera und Effekte).</summary>
        public float Schlupf { get; private set; }

        /// <summary>True, wenn der Panzer gerade etwas beruehrt (Wand oder anderer Panzer). Fuer die Diagnose-Anzeige.</summary>
        public bool HatKontakt => Time.time - m_LetzterKontakt < 0.15f;
        private float m_LetzterKontakt = -10f;

        private void OnCollisionStay(Collision kollision)
        {
            m_LetzterKontakt = Time.time;
        }

        public override void Anhalten()
        {
            m_DrehrateAktuell = 0f;
            m_DriftAnteil = 0f;
            m_Steigen = 0f;
            if (m_Body == null || m_Body.isKinematic) return;
            m_Body.linearVelocity = Vector3.zero;
            m_Body.angularVelocity = Vector3.zero;
        }

        public override Vector3 TurmDrehung(float grad) => new Vector3(0, grad, 0);

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_BasisSpeedForce = m_SpeedForce;

            // Tempo und Drehung fuehrt dieses Script selbst. Daempfung des Rigidbody wuerde dagegen arbeiten.
            m_Body.linearDamping = 0f;
            m_Body.angularDamping = 0f;

            // Nie einschlafen: Ein schlafender Panzer bekommt keine Trigger-Meldungen mehr, ein Ghost
            // koennte dann durch einen stehenden Panzer fahren, ohne dass es jemand merkt.
            m_Body.sleepThreshold = 0f;

            // Ohne Reibung gleitet der Panzer an Waenden entlang, statt an ihnen haengen zu bleiben.
            var gleiten = new PhysicsMaterial("PanzerGleiten")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0.05f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            foreach (var collider in GetComponents<Collider>()) collider.sharedMaterial = gleiten;

            // Erzeugte Strecken haben Hoehenunterschiede. Die Hoehe muss deshalb auch ueber das Netz
            // gehen (aeltere Prefabs uebertragen nur X und Z). Hier, vor dem Spawnen, gilt es fuer alle gleich.
            var netz = GetComponent<NetworkTransform>();
            if (netz != null) netz.SyncPositionY = true;

            OptikVorbereiten();

            // Turm und Kanone (Zielen, Gewicht). Aeltere Prefabs haben die Komponente noch nicht.
            if (GetComponent<PanzerTurm3D>() == null) gameObject.AddComponent<PanzerTurm3D>();
        }

        /// <summary>
        /// Haengt alle sichtbaren Teile unter ein gemeinsames Kind, das geneigt werden kann,
        /// ohne Collider und Fahrtrichtung zu beeinflussen.
        /// </summary>
        private void OptikVorbereiten()
        {
            var kinder = new Transform[transform.childCount];
            for (int i = 0; i < kinder.Length; i++) kinder[i] = transform.GetChild(i);

            m_Optik = new GameObject("Optik").transform;
            m_Optik.SetParent(transform, false);
            foreach (Transform kind in kinder) kind.SetParent(m_Optik, false);
        }

        void FixedUpdate()
        {
            // Fremde Panzer sind kinematisch und werden nur ueber das Netzwerk bewegt.
            if (m_Body.isKinematic) return;

            float dt = Time.fixedDeltaTime;
            float gas = Mathf.Clamp(m_Gas, -1f, 1f);
            float lenkung = Mathf.Clamp(m_Lenkung, -1f, 1f);
            bool boost = m_SpeedForce > m_BasisSpeedForce * 1.01f;
            float boostFaktor = boost ? m_BoostFaktor : 1f;

            // Drift weich ein- und ausblenden, damit der Halt nie schlagartig wechselt.
            float driftZiel = m_Drift ? 1f : 0f;
            float driftRate = 1f / Mathf.Max(0.01f, m_Drift ? m_DriftEinblenden : m_DriftAusblenden);
            m_DriftAnteil = Mathf.MoveTowards(m_DriftAnteil, driftZiel, driftRate * dt);
            float drift = Mathf.SmoothStep(0f, 1f, m_DriftAnteil);

            Vector3 vorwaerts = transform.forward;
            vorwaerts.y = 0f;
            vorwaerts.Normalize();
            Vector3 rechts = Vector3.Cross(Vector3.up, vorwaerts);

            // Aktuelle Geschwindigkeit (auch nach Stoessen) in Laengs- und Queranteil zerlegen
            Vector3 v = m_Body.linearVelocity;
            float laengs = Vector3.Dot(v, vorwaerts);
            float quer = Vector3.Dot(v, rechts);

            // --- Laengs: Zieltempo mit passender Rate anfahren ---
            float hoechst = m_Hoechsttempo * boostFaktor;
            float ziel = gas >= 0f ? gas * hoechst : gas * m_Rueckwaertstempo;
            float rate;
            if (Mathf.Abs(gas) < 0.01f) rate = m_Ausrollen;
            else if (Mathf.Abs(laengs) > 1f && Mathf.Sign(ziel) != Mathf.Sign(laengs)) rate = m_Bremskraft;
            else if (Mathf.Abs(laengs) > Mathf.Abs(ziel)) rate = m_Ausrollen;   // z. B. nach dem Boost
            else rate = m_Beschleunigung * boostFaktor;

            float laengsNeu = Mathf.MoveTowards(laengs, ziel, rate * dt);
            m_LaengsBeschleunigung = (laengsNeu - laengs) / dt;

            // --- Quer: Rutschen abbauen. Im Drift bleibt die alte Richtung laenger erhalten. ---
            float halt = Mathf.Lerp(m_Seitenhalt, m_DriftSeitenhalt, drift);
            float querNeu = quer * Mathf.Exp(-halt * dt);

            // Driftwinkel begrenzen: so weit quer wie erlaubt, nie unkontrolliert seitwaerts.
            float maxQuer = Mathf.Abs(laengsNeu) * Mathf.Tan(Mathf.Clamp(m_DriftMaxWinkel, 1f, 85f) * Mathf.Deg2Rad);
            if (drift > 0f && Mathf.Abs(laengsNeu) > 1f) querNeu = Mathf.Clamp(querNeu, -maxQuer, maxQuer);

            Vector3 neu = vorwaerts * laengsNeu + rechts * querNeu;

            // Seitenhalt soll die Fahrtrichtung zur Nase drehen, nicht Tempo vernichten. Ohne das
            // kostet jede Kurve und vor allem das Ende eines Drifts spuerbar Geschwindigkeit,
            // weil der Queranteil einfach wegfaellt. Wer Gas gibt, behaelt deshalb sein Tempo.
            if (gas > 0.01f && laengs > 0f && laengsNeu > 0f)
            {
                float vorherTempo = Mathf.Sqrt(laengs * laengs + quer * quer);
                float soll = Mathf.Min(vorherTempo, Mathf.Max(hoechst, Mathf.Abs(laengs)));
                float ist = neu.magnitude;
                if (ist > 0.01f && ist < soll) neu *= soll / ist;
            }

            // Gesamttempo deckeln und im Drift leicht abbauen (laengs + quer koennten sonst ueber dem Hoechsttempo liegen).
            float tempo = neu.magnitude;
            float deckel = Mathf.Max(hoechst, Mathf.Abs(laengs));
            if (drift > 0f && tempo > 0.01f)
            {
                float verlust = 1f - m_DriftTempoVerlust * drift * dt * Mathf.Clamp01(Mathf.Abs(querNeu) / Mathf.Max(1f, tempo) * 3f);
                neu *= verlust;
                tempo *= verlust;
            }
            if (tempo > deckel) neu *= deckel / tempo;

            neu.y = HoeheFolgen(neu, dt);
            m_Body.linearVelocity = neu;
            Schlupf = Vector3.Dot(neu, rechts) / Mathf.Max(1f, m_Hoechsttempo);

            // --- Drehung: im Stand wendig, bei Tempo ruhiger, im Drift deutlich schneller ---
            float tempoAnteil = Mathf.Clamp01(Mathf.Abs(laengsNeu) / Mathf.Max(1f, m_Hoechsttempo));
            float drehrate = Mathf.Lerp(m_DrehrateStand, m_DrehrateSchnell, tempoAnteil);
            drehrate *= Mathf.Lerp(1f, m_DriftDrehFaktor, drift * tempoAnteil);
            float richtung = (m_RueckwaertsWieAuto && laengsNeu < -1f && gas < 0f) ? -1f : 1f;
            float zielDrehrate = lenkung * drehrate * richtung;
            m_DrehrateAktuell = Mathf.MoveTowards(m_DrehrateAktuell, zielDrehrate, m_LenkAnsprechen * dt);
            m_Body.angularVelocity = new Vector3(0f, m_DrehrateAktuell * Mathf.Deg2Rad, 0f);
        }

        /// <summary>
        /// Senkrechte Geschwindigkeit fuer diesen Physikschritt. Die Fahrbahn hat keinen Collider;
        /// ihre Hoehe kommt vom StreckenGenerator. Am Boden folgt der Panzer ihr genau. Faellt sie
        /// schneller ab, als er fallen kann (Kuppe, Buckel bei Tempo), fliegt er, bis er wieder aufsetzt.
        /// In Szenen ohne erzeugte Strecke bleibt alles wie bisher: Der Panzer haelt seine Hoehe.
        /// </summary>
        private float HoeheFolgen(Vector3 flach, float dt)
        {
            StreckenGenerator strecke = StreckenGenerator.Aktiv;
            Vector3 position = m_Body.position;
            // Gefragt wird fuer die Stelle, an der der Panzer nach diesem Schritt steht. Sonst haengt er auf Rampen hinterher.
            if (strecke == null || !strecke.BodenHoehe(position + flach * dt, out float boden, out Vector3 normale))
            {
                AmBoden = true;
                m_Steigen = 0f;
                return 0f;
            }

            // Das Prefab haelt den Panzer auf seiner Hoehe fest. Auf einer Strecke mit Hoehen darf er sich heben und senken.
            if ((m_Body.constraints & RigidbodyConstraints.FreezePositionY) != 0)
                m_Body.constraints &= ~RigidbodyConstraints.FreezePositionY;

            m_Steigen -= m_Schwerkraft * dt;
            if (position.y + m_Steigen * dt > boden + 0.01f)
            {
                AmBoden = false;
                return m_Steigen;
            }

            // Aufgesetzt: genau auf die Fahrbahn, und so schnell steigen oder sinken wie sie selbst.
            // Mit diesem Wert hebt der Panzer im naechsten Schritt ab, falls die Fahrbahn wegknickt.
            AmBoden = true;
            m_Steigen = -(normale.x * flach.x + normale.z * flach.z) / Mathf.Max(0.2f, normale.y);
            return Mathf.Clamp((boden - position.y) / dt, -400f, 400f);
        }

        void Update()
        {
            if (m_Optik == null || m_Body == null) return;

            HangNeigung();

            // Fremde Panzer kommen ueber das Netz: nur der Hang, kein Federn.
            if (m_Body.isKinematic)
            {
                m_Optik.localRotation = Quaternion.Euler(m_HangNicken, 0f, m_HangRollen);
                return;
            }

            // Wanne legt sich in Kurven leicht nach aussen und nickt beim Gasgeben und Bremsen.
            float tempoAnteil = Mathf.Clamp01(Tempo / Mathf.Max(1f, m_Hoechsttempo));
            float kurve = m_NeigungKurve * (1f + m_DriftAnteil);   // im Drift legt er sich staerker in die Federn
            float zielRollen = Mathf.Clamp(m_DrehrateAktuell / Mathf.Max(1f, m_DrehrateSchnell), -1.5f, 1.5f) * tempoAnteil * kurve;
            float zielNicken = -Mathf.Clamp(m_LaengsBeschleunigung / Mathf.Max(1f, m_Bremskraft), -1f, 1f) * m_NeigungGas * 2f;
            zielNicken = Mathf.Clamp(zielNicken, -m_NeigungGas, m_NeigungGas);

            float folgen = 1f - Mathf.Exp(-m_NeigungTempo * Time.deltaTime);
            m_Rollen = Mathf.Lerp(m_Rollen, zielRollen, folgen);
            m_Nicken = Mathf.Lerp(m_Nicken, zielNicken, folgen);
            m_Optik.localRotation = Quaternion.Euler(m_Nicken + m_HangNicken, 0f, m_Rollen + m_HangRollen);
        }

        /// <summary>
        /// Legt die sichtbare Wanne auf die Neigung der Fahrbahn (Rampen, Buckel). Der Collider
        /// bleibt waagerecht. In der Luft bleibt die letzte Neigung stehen.
        /// </summary>
        private void HangNeigung()
        {
            StreckenGenerator strecke = StreckenGenerator.Aktiv;
            float zielNicken = 0f, zielRollen = 0f;
            if (strecke != null && strecke.BodenHoehe(transform.position, out float boden, out Vector3 normale))
            {
                if (transform.position.y - boden > 0.5f) return;   // in der Luft

                Vector3 vorwaerts = transform.forward;
                vorwaerts.y = 0f;
                vorwaerts.Normalize();
                Vector3 rechts = Vector3.Cross(Vector3.up, vorwaerts);
                // Steigt die Fahrbahn nach vorn an, zeigt ihre Normale nach hinten: Nase hoch = negative Drehung um X.
                zielNicken = Mathf.Atan2(Vector3.Dot(normale, vorwaerts), normale.y) * Mathf.Rad2Deg;
                zielRollen = -Mathf.Atan2(Vector3.Dot(normale, rechts), normale.y) * Mathf.Rad2Deg;
            }

            float folgen = 1f - Mathf.Exp(-m_HangTempo * Time.deltaTime);
            m_HangNicken = Mathf.Lerp(m_HangNicken, zielNicken, folgen);
            m_HangRollen = Mathf.Lerp(m_HangRollen, zielRollen, folgen);
        }
    }
}
