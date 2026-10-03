using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TopDownRace
{
    /// <summary>
    /// Erzeugt eine geschlossene Rennstrecke fuer die 3D-Szene: Fahrbahn, Waende, Checkpoints,
    /// Ziellinie, Startaufstellung, Schutzzone am Start und Treibstoff-Kanister.
    ///
    /// Aus demselben Seed entsteht immer dieselbe Strecke. Das ist die Grundlage fuer den
    /// Multiplayer: Der Host waehlt den Seed, alle Clients bauen daraus dieselbe Strecke
    /// (siehe GameControl).
    ///
    /// Benutzung:
    /// - Im Editor: Knopf "Strecke erzeugen" im Inspector oder Menue LoopTank > Strecke generieren (3D).
    /// - Zur Laufzeit: "Zur Laufzeit erzeugen" anhaken. Die Rennleitung ruft dann Erzeugen(seed) auf.
    ///
    /// Den Verlauf rechnet StreckenPlan aus: Grundform (Rundkurs mit Einbuchtungen, Acht,
    /// Schleife), wechselnde Breite, Hoehenprofil mit Huegeln und Buckeln, Bruecken an
    /// Kreuzungen und Abzweigungen. Diese Klasse baut daraus die Meshes und beantwortet zur
    /// Laufzeit die Frage, wie hoch die Fahrbahn an einer Stelle liegt (BodenHoehe). Danach
    /// richten sich Panzer und Ghosts; die Fahrbahn selbst hat keinen Collider.
    ///
    /// Leistung: Die ganze Strecke besteht aus wenigen grossen Meshes statt vieler Einzelobjekte.
    /// Alle Waende sind EIN Mesh mit EINEM MeshCollider, die Fahrbahn ist ein Mesh, alle
    /// Markierungen einer Sorte sind je ein Mesh. Das ergibt rund sieben Batches fuer die
    /// Strecke, unabhaengig von ihrer Laenge. Die Kanister teilen sich ein Mesh und ein Material
    /// mit GPU-Instancing.
    /// </summary>
    public class StreckenGenerator : MonoBehaviour
    {
        [Header("Form")]
        [Tooltip("Gleicher Seed = gleiche Strecke.")]
        public int m_Seed = 2026;
        [Tooltip("Grundform. Zufall = je Seed eine andere. Rundkurs: Runde mit Einbuchtungen. Acht: kreuzt sich über eine Brücke. Schleife: große Runde mit einer Schleife nach innen (braucht Platz, also einen großen Radius).")]
        public StreckenForm m_Form = StreckenForm.Zufall;
        [Tooltip("An: Je nach Seed wird die Runde links- oder rechtsherum gefahren.")]
        public bool m_RichtungZufaellig = true;
        [Tooltip("Anzahl der Punkte, durch die die Strecke läuft. Mehr = mehr Kurven.")]
        [Range(5, 24)]
        public int m_Kontrollpunkte = 11;
        [Tooltip("Mittlerer Abstand der Strecke von der Mitte. Bestimmt die Rundenlänge.")]
        public float m_Radius = 260f;
        [Tooltip("Wie stark die Strecke von der Grundform abweicht. 0 = glatt, 0,5 = sehr verwinkelt.")]
        [Range(0f, 0.6f)]
        public float m_Unregelmaessigkeit = 0.32f;
        [Tooltip("Länge eines Wandstücks. Kleiner = rundere Kurven und feinere Buckel, aber mehr Dreiecke.")]
        public float m_Stuecklaenge = 14f;

        [Header("Breite")]
        [Tooltip("Mittlere Breite der Fahrbahn zwischen den Wänden.")]
        public float m_Streckenbreite = 60f;
        [Tooltip("Wie stark die Breite schwankt. 0,3 = Abschnitte zwischen 70 % und 130 % der mittleren Breite.")]
        [Range(0f, 0.5f)]
        public float m_BreitenVariation = 0.3f;
        [Tooltip("In wie viele Abschnitte die Runde für die Breite geteilt wird. Weniger = längere Engstellen und längere breite Passagen.")]
        [Range(3, 12)]
        public int m_BreitenAbschnitte = 5;

        [Header("Höhe")]
        [Tooltip("Höhe der höchsten Hügel über dem Start. 0 = flach (Brücken gibt es trotzdem).")]
        public float m_Hoehenunterschied = 22f;
        [Tooltip("Steilste erlaubte Rampe. 0,16 = 16 Einheiten Höhe auf 100 Einheiten Strecke.")]
        [Range(0.04f, 0.35f)]
        public float m_MaxSteigung = 0.16f;
        [Tooltip("Ungefährer Abstand zwischen Hügel und Senke entlang der Strecke. Kleiner = es geht öfter auf und ab.")]
        public float m_HuegelAbstand = 220f;
        [Tooltip("Höhe der Brückenfahrbahn über der Strecke darunter. Muss über die Wände der unteren Strecke passen.")]
        public float m_BrueckenHoehe = 18f;
        [Tooltip("Dicke der Brückenplatte.")]
        public float m_BrueckenDicke = 2f;

        [Header("Unebenheiten")]
        [Tooltip("Anzahl der Abschnitte mit Bodenwellen. 0 = keine.")]
        [Range(0, 6)]
        public int m_BuckelAbschnitte = 2;
        [Tooltip("Höhe der Bodenwellen. Bei Tempo hebt der Panzer darüber kurz ab.")]
        public float m_BuckelHoehe = 3f;
        [Tooltip("Abstand von Welle zu Welle.")]
        public float m_BuckelLaenge = 45f;

        [Header("Abzweigungen")]
        [Tooltip("Anzahl der Abzweigungen. Die Strecke teilt sich dort um eine Insel in zwei Wege und führt wieder zusammen. 0 = keine.")]
        [Range(0, 4)]
        public int m_Abzweigungen = 1;
        [Tooltip("Länge einer Abzweigung entlang der Strecke.")]
        public float m_AbzweigLaenge = 400f;
        [Tooltip("Breite der Abzweigung als Anteil der Hauptstrecke.")]
        [Range(0.3f, 1f)]
        public float m_AbzweigBreite = 0.6f;
        [Tooltip("Breite der Insel zwischen Hauptstrecke und Abzweigung.")]
        public float m_InselBreite = 16f;

        [Header("Abgrenzung")]
        public float m_WandHoehe = 9f;
        public float m_WandDicke = 2.5f;

        [Header("Checkpoints")]
        [Tooltip("Anzahl der Tore einschließlich Ziellinie. Sie werden gleichmäßig über die Runde verteilt.")]
        [Range(2, 32)]
        public int m_CheckpointAnzahl = 8;
        [Tooltip("Zwischen-Checkpoints als farbige Streifen auf der Fahrbahn anzeigen.")]
        public bool m_CheckpointsSichtbar = true;
        [Tooltip("Tiefe der Tore in Fahrtrichtung. Zu dünn, und sehr schnelle Panzer könnten hindurchspringen.")]
        public float m_TorTiefe = 10f;
        public float m_TorHoehe = 30f;

        [Header("Start")]
        [Tooltip("Anzahl der Startplätze (zwei pro Reihe).")]
        [Range(1, 8)]
        public int m_Startplaetze = 4;
        [Tooltip("Abstand der ersten Startreihe vor der Ziellinie.")]
        public float m_StartAbstand = 30f;
        [Tooltip("Abstand zwischen den Startreihen.")]
        public float m_ReihenAbstand = 36f;
        [Tooltip("Länge des geraden, ebenen Stücks, auf dem Start und Ziel liegen. Der Generator sucht das geradeste Stück der Runde und zieht es ganz gerade.")]
        public float m_StartGerade = 190f;
        [Tooltip("Wie viel der Geraden hinter der Ziellinie liegt.")]
        public float m_GeradeNachZiel = 45f;

        [Header("Treibstoff-Kanister")]
        [Tooltip("Anzahl der Kanister auf der Runde. 0 = keine. Jede Abzweigung bekommt zusätzlich einen.")]
        [Range(0, 16)]
        public int m_KanisterAnzahl = 5;
        [Tooltip("Größe eines Kanisters.")]
        public float m_KanisterGroesse = 6f;
        [Tooltip("Treibstoff pro Kanister.")]
        public float m_KanisterMenge = 35f;
        [Tooltip("Sekunden, bis ein eingesammelter Kanister wieder erscheint.")]
        public float m_KanisterRespawn = 14f;

        [Header("Laufzeit")]
        [Tooltip("An: Die Strecke wird beim Rennstart neu gebaut (bei allen Spielern gleich). Aus: Es bleibt die im Editor erzeugte Strecke.")]
        public bool m_ZurLaufzeitErzeugen = false;
        [Tooltip("An: Jedes Rennen bekommt eine neue, zufällige Strecke. Aus: Es wird immer der Seed oben benutzt.")]
        public bool m_ZufaelligerSeed = true;

        [Header("Materialien (leer = Standardmaterial)")]
        public Material m_BodenMaterial;
        public Material m_StrassenMaterial;
        public Material m_WandMaterial;
        public Material m_ZielMaterial;
        public Material m_CheckpointMaterial;
        public Material m_StartMaterial;
        public Material m_KanisterMaterial;

        [Header("Verknüpfung")]
        [Tooltip("Bekommt Checkpoints und Startplätze eingetragen. Leer = wird gesucht oder angelegt.")]
        public RaceTrackControl m_Kontrolle;

        private const string WurzelName = "Erzeugt";
        private const float UntergrundHoehe = -0.05f;

        /// <summary>Der Generator der laufenden Rennszene. Null in Szenen ohne erzeugte Strecke.</summary>
        public static StreckenGenerator Aktiv { get; private set; }

        // Verlauf der zuletzt erzeugten Strecke. Gespeichert, damit die Hoehenabfrage auch bei
        // einer im Editor erzeugten Strecke funktioniert.
        [SerializeField, HideInInspector] private StreckenBahn m_Haupt = new StreckenBahn();
        [SerializeField, HideInInspector] private List<StreckenBahn> m_Abzweige = new List<StreckenBahn>();
        [SerializeField, HideInInspector] private StreckenForm m_GebauteForm = StreckenForm.Rundkurs;
        [SerializeField, HideInInspector] private int m_BrueckenAnzahl;
        [SerializeField, HideInInspector] private bool m_Notfall;

        private static readonly Vector3[] s_Leer = new Vector3[0];

        /// <summary>Mittellinie der Hauptrunde. Y ist die Hoehe der Fahrbahn.</summary>
        public IReadOnlyList<Vector3> Mittellinie => m_Haupt != null && m_Haupt.mitte != null ? m_Haupt.mitte : s_Leer;

        /// <summary>Form der zuletzt erzeugten Strecke (nie "Zufall").</summary>
        public StreckenForm GebauteForm => m_GebauteForm;
        public int BrueckenAnzahl => m_BrueckenAnzahl;
        public int AbzweigAnzahl => m_Abzweige != null ? m_Abzweige.Count : 0;
        /// <summary>True: Mit diesen Einstellungen war keine Strecke befahrbar, es wurde ein flacher Kreis gebaut.</summary>
        public bool IstNotfall => m_Notfall;

        // ------------------------------------------------------------------
        // Mesh-Baukasten: sammelt Vierecke und Quader und macht daraus ein einziges Mesh
        // ------------------------------------------------------------------

        private class MeshBau
        {
            private readonly List<Vector3> m_Ecken = new List<Vector3>();
            private readonly List<Vector3> m_Normalen = new List<Vector3>();
            private readonly List<Vector2> m_Uv = new List<Vector2>();
            private readonly List<int> m_Dreiecke = new List<int>();

            public bool Leer => m_Ecken.Count == 0;

            /// <summary>Viereck a-b-c-d (reihum). Die sichtbare Seite zeigt in Richtung der Normalen.</summary>
            public void Viereck(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normale)
            {
                // Unity zeigt die Seite, auf der die Ecken im Uhrzeigersinn laufen. Falsch herum: umdrehen.
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normale) < 0f)
                {
                    Vector3 tausch = b; b = d; d = tausch;
                }

                int start = m_Ecken.Count;
                m_Ecken.Add(a); m_Ecken.Add(b); m_Ecken.Add(c); m_Ecken.Add(d);
                for (int i = 0; i < 4; i++) m_Normalen.Add(normale);
                m_Uv.Add(new Vector2(0, 0)); m_Uv.Add(new Vector2(0, 1)); m_Uv.Add(new Vector2(1, 1)); m_Uv.Add(new Vector2(1, 0));
                m_Dreiecke.Add(start); m_Dreiecke.Add(start + 1); m_Dreiecke.Add(start + 2);
                m_Dreiecke.Add(start); m_Dreiecke.Add(start + 2); m_Dreiecke.Add(start + 3);
            }

            /// <summary>Viereck, das von oben sichtbar ist. Die Normale folgt der Neigung der Flaeche (Rampen, Buckel).</summary>
            public void ViereckOben(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Vector3 normale = Vector3.Cross(c - a, d - b);
                if (normale.sqrMagnitude < 1e-10f) return;
                normale.Normalize();
                if (normale.y < 0f) normale = -normale;
                Viereck(a, b, c, d, normale);
            }

            /// <summary>
            /// Rechteck auf der Fahrbahn, laengs in Richtung "vor". Hat "vor" eine Steigung, liegt
            /// das Rechteck entsprechend schraeg. "abheben" hebt es ueber die Flaeche darunter.
            /// </summary>
            public void BodenRechteck(Vector3 mitte, Vector3 vor, float breite, float laenge, float abheben)
            {
                Vector3 flach = new Vector3(vor.x, 0f, vor.z);
                Vector3 seite = Vector3.Cross(Vector3.up, flach).normalized * (breite * 0.5f);
                Vector3 v = vor.normalized * (laenge * 0.5f);
                Vector3 m = mitte + Vector3.up * abheben;
                ViereckOben(m - seite - v, m - seite + v, m + seite + v, m + seite - v);
            }

            /// <summary>Quader mit Mittelpunkt und Groesse (ohne Drehung).</summary>
            public void Quader(Vector3 mitte, Vector3 groesse)
            {
                Vector3 h = groesse * 0.5f;
                Vector3 p000 = mitte + new Vector3(-h.x, -h.y, -h.z), p100 = mitte + new Vector3(h.x, -h.y, -h.z);
                Vector3 p010 = mitte + new Vector3(-h.x, h.y, -h.z), p110 = mitte + new Vector3(h.x, h.y, -h.z);
                Vector3 p001 = mitte + new Vector3(-h.x, -h.y, h.z), p101 = mitte + new Vector3(h.x, -h.y, h.z);
                Vector3 p011 = mitte + new Vector3(-h.x, h.y, h.z), p111 = mitte + new Vector3(h.x, h.y, h.z);
                Viereck(p000, p010, p110, p100, Vector3.back);
                Viereck(p001, p011, p111, p101, Vector3.forward);
                Viereck(p000, p010, p011, p001, Vector3.left);
                Viereck(p100, p110, p111, p101, Vector3.right);
                Viereck(p010, p011, p111, p110, Vector3.up);
                Viereck(p000, p001, p101, p100, Vector3.down);
            }

            public Mesh Fertig(string name)
            {
                var mesh = new Mesh { name = name };
                if (m_Ecken.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(m_Ecken);
                mesh.SetNormals(m_Normalen);
                mesh.SetUVs(0, m_Uv);
                mesh.SetTriangles(m_Dreiecke, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        // ------------------------------------------------------------------

        private void OnEnable()
        {
            Aktiv = this;
        }

        private void OnDisable()
        {
            if (Aktiv == this) Aktiv = null;
        }

        public void Erzeugen()
        {
            Erzeugen(m_Seed);
        }

        /// <summary>Baut die Strecke neu. Alles vorher Erzeugte wird ersetzt.</summary>
        public void Erzeugen(int seed)
        {
            m_Seed = seed;
            Aufraeumen();

            Transform wurzel = new GameObject(WurzelName).transform;
            wurzel.SetParent(transform, false);

            StreckenPlan plan = StreckenPlan.Bauen(PlanWerte(seed));
            m_Haupt = plan.Haupt;
            m_Abzweige = new List<StreckenBahn>(plan.Abzweige);
            m_GebauteForm = plan.Form;
            m_BrueckenAnzahl = plan.Bruecken;
            m_Notfall = plan.Notfall;
            if (m_Notfall)
                Debug.LogWarning("[StreckenGenerator] Mit diesen Einstellungen passt keine Strecke (Radius zu klein für die Breite?). Es wurde ein flacher Kreis gebaut.");

            BodenUndFahrbahnBauen(wurzel);
            WaendeBauen(wurzel);
            Checkpoint[] checkpoints = CheckpointsBauen(wurzel);
            Transform[] startplaetze = StartBauen(wurzel);
            KanisterBauen(wurzel, seed);

            if (m_Kontrolle == null) m_Kontrolle = GetComponent<RaceTrackControl>();
            if (m_Kontrolle == null) m_Kontrolle = FindFirstObjectByType<RaceTrackControl>();
            if (m_Kontrolle == null) m_Kontrolle = gameObject.AddComponent<RaceTrackControl>();
            m_Kontrolle.m_Checkpoints = checkpoints;
            m_Kontrolle.m_StartPositions = startplaetze;
            m_Kontrolle.CheckpointsNeuEinlesen();
            RaceTrackControl.m_Main = m_Kontrolle;

            if (Application.isPlaying) Aktiv = this;
        }

        private StreckenPlan.Werte PlanWerte(int seed)
        {
            // Die Bruecke muss ueber die Waende der unteren Strecke passen, sonst ragen sie durch die Platte.
            float brueckenHoehe = Mathf.Max(m_BrueckenHoehe, m_WandHoehe + m_BrueckenDicke + 3f);
            return new StreckenPlan.Werte
            {
                seed = seed,
                form = m_Form,
                richtungZufaellig = m_RichtungZufaellig,
                kontrollpunkte = m_Kontrollpunkte,
                radius = m_Radius,
                unregelmaessig = m_Unregelmaessigkeit,
                stuecklaenge = m_Stuecklaenge,
                breite = m_Streckenbreite,
                breitenVariation = m_BreitenVariation,
                breitenAbschnitte = m_BreitenAbschnitte,
                wandDicke = m_WandDicke,
                startGerade = m_StartGerade,
                geradeNachZiel = m_GeradeNachZiel,
                hoehenunterschied = m_Hoehenunterschied,
                maxSteigung = m_MaxSteigung,
                huegelAbstand = m_HuegelAbstand,
                brueckenHoehe = brueckenHoehe,
                buckelAbschnitte = m_BuckelAbschnitte,
                buckelHoehe = m_BuckelHoehe,
                buckelLaenge = m_BuckelLaenge,
                abzweigungen = m_Abzweigungen,
                abzweigLaenge = m_AbzweigLaenge,
                abzweigBreite = m_AbzweigBreite,
                inselBreite = m_InselBreite,
            };
        }

        private void Aufraeumen()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform kind = transform.GetChild(i);
                if (kind.name != WurzelName) continue;

                if (Application.isPlaying)
                {
                    // Destroy wirkt erst am Ende des Bildes; bis dahin soll das alte Stueck nicht mehr mitspielen.
                    kind.gameObject.SetActive(false);
                    kind.SetParent(null);
                    Destroy(kind.gameObject);
                }
                else
                {
                    DestroyImmediate(kind.gameObject);
                }
            }
        }

        private GameObject MeshObjekt(string name, Transform eltern, Mesh mesh, Material material, bool schatten)
        {
            var objekt = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            objekt.transform.SetParent(eltern, false);
            objekt.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = objekt.GetComponent<MeshRenderer>();
            if (material != null) renderer.sharedMaterial = material;
            renderer.shadowCastingMode = schatten ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return objekt;
        }

        // ------------------------------------------------------------------
        // Hoehe der Fahrbahn (fuer Panzer und Ghosts)
        // ------------------------------------------------------------------

        /// <summary>True, wenn eine Strecke mit Hoehenangaben vorliegt.</summary>
        public bool HatVerlauf => m_Haupt != null && m_Haupt.Anzahl >= 2;

        /// <summary>
        /// Hoehe und Neigung der Fahrbahn an einer Stelle der Welt. Wo zwei Fahrbahnen
        /// uebereinander liegen (Bruecke), zaehlt die, die naeher an weltPosition.y liegt: Wer auf
        /// der Bruecke faehrt, bleibt oben, wer darunter faehrt, bleibt unten.
        /// Liefert false, wenn es keine erzeugte Strecke gibt (dann bleiben hoehe und normale unveraendert sinnvoll).
        /// </summary>
        public bool BodenHoehe(Vector3 weltPosition, out float hoehe, out Vector3 normale)
        {
            hoehe = weltPosition.y;
            normale = Vector3.up;
            if (!HatVerlauf) return false;

            // Der Generator steht ungedreht in der Szene; nur seine Position zaehlt.
            Vector3 versatz = transform.position;
            Vector3 p = weltPosition - versatz;
            float kosten = float.MaxValue;
            float h = p.y;
            m_Haupt.Boden(p, ref kosten, ref h, ref normale);
            if (m_Abzweige != null)
            {
                foreach (StreckenBahn abzweig in m_Abzweige) abzweig.Boden(p, ref kosten, ref h, ref normale);
            }
            hoehe = h + versatz.y;
            return true;
        }

        // ------------------------------------------------------------------
        // Bauteile
        // ------------------------------------------------------------------

        private void BodenUndFahrbahnBauen(Transform wurzel)
        {
            // Untergrund: ein einziges grosses Viereck auf Hoehe 0. Hoeher liegende Fahrbahn steht auf einem Sockel (siehe Waende).
            var grenzen = new Bounds(m_Haupt.Kante(0, -1f), Vector3.zero);
            for (int i = 0; i < m_Haupt.Anzahl; i++) { grenzen.Encapsulate(m_Haupt.Kante(i, -1f)); grenzen.Encapsulate(m_Haupt.Kante(i, 1f)); }
            foreach (StreckenBahn abzweig in m_Abzweige)
                for (int i = 0; i < abzweig.Anzahl; i++) { grenzen.Encapsulate(abzweig.Kante(i, -1f)); grenzen.Encapsulate(abzweig.Kante(i, 1f)); }

            var boden = new MeshBau();
            boden.BodenRechteck(new Vector3(grenzen.center.x, 0f, grenzen.center.z), Vector3.forward, grenzen.size.x + 200f, grenzen.size.z + 200f, UntergrundHoehe);
            MeshObjekt("Boden", wurzel, boden.Fertig("Boden"), m_BodenMaterial, false);

            // Fahrbahn: je Spur ein Band entlang der Mittellinie. Kein Collider: Die Panzer fragen die Hoehe ab.
            var fahrbahn = new MeshBau();
            FahrbahnBand(fahrbahn, m_Haupt, 0.02f);
            // Wo sich Abzweigung und Hauptrunde ueberdecken, liegt die Abzweigung einen Hauch hoeher. Sonst flimmert es.
            foreach (StreckenBahn abzweig in m_Abzweige) FahrbahnBand(fahrbahn, abzweig, 0.04f);
            MeshObjekt("Fahrbahn", wurzel, fahrbahn.Fertig("Fahrbahn"), m_StrassenMaterial, false);
        }

        private static void FahrbahnBand(MeshBau bau, StreckenBahn bahn, float abheben)
        {
            int n = bahn.Anzahl;
            Vector3 hoch = Vector3.up * abheben;
            for (int i = 0; i < bahn.Stuecke; i++)
            {
                int j = (i + 1) % n;
                bau.ViereckOben(bahn.Kante(i, -1f) + hoch, bahn.Kante(j, -1f) + hoch, bahn.Kante(j, 1f) + hoch, bahn.Kante(i, 1f) + hoch);
            }
        }

        /// <summary>
        /// Alle Waende als ein Mesh: je Stueck eine Innenflaeche, eine Oberseite und eine Aussenflaeche.
        /// Die Aussenflaeche reicht bis zum Untergrund: Hoeher liegende Fahrbahn steht so auf einem
        /// Sockel. Auf einer Bruecke endet sie an der Unterkante der Platte, darunter bleibt es offen.
        /// Dasselbe Mesh dient als Collider. Ein MeshCollider fuer die ganze Strecke ist fuer die
        /// Physik guenstiger als hunderte einzelner Box-Collider.
        /// </summary>
        private void WaendeBauen(Transform wurzel)
        {
            var bau = new MeshBau();

            // Wo eine Abzweigung in die Hauptrunde muendet, hat die Wand der Hauptrunde eine Oeffnung:
            // Alles, was in der Fahrbahn der Abzweigung liegt, entfaellt. Umgekehrt genauso.
            int n = m_Haupt.Anzahl;
            bool[] anAbzweig = null;
            if (m_Abzweige.Count > 0)
            {
                anAbzweig = new bool[n];
                foreach (StreckenBahn abzweig in m_Abzweige)
                    for (int d = -1; d <= abzweig.Anzahl; d++) anAbzweig[((abzweig.vonStelle + d) % n + n) % n] = true;
            }
            WandSeite(bau, m_Haupt, -1f, InAbzweig, anAbzweig);
            WandSeite(bau, m_Haupt, 1f, InAbzweig, anAbzweig);
            foreach (StreckenBahn abzweig in m_Abzweige)
            {
                WandSeite(bau, abzweig, -1f, InHauptrunde, null);
                WandSeite(bau, abzweig, 1f, InHauptrunde, null);
            }

            BrueckenUnterbau(bau);

            Mesh mesh = bau.Fertig("Waende");
            GameObject objekt = MeshObjekt("Waende", wurzel, mesh, m_WandMaterial, true);
            objekt.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        private bool InAbzweig(Vector3 p)
        {
            foreach (StreckenBahn abzweig in m_Abzweige)
            {
                if (abzweig.Abstand(p, out float halb) < halb - 0.05f) return true;
            }
            return false;
        }

        private bool InHauptrunde(Vector3 p)
        {
            return m_Haupt.Abstand(p, out float halb) < halb - 0.05f;
        }

        /// <param name="verdeckt">Liefert true fuer Punkte, an denen keine Wand stehen darf (dort faehrt man).</param>
        /// <param name="pruefen">Nur an diesen Stellen wird "verdeckt" gefragt. Null = ueberall.</param>
        private void WandSeite(MeshBau bau, StreckenBahn bahn, float seitenVorzeichen, System.Func<Vector3, bool> verdeckt, bool[] pruefen)
        {
            int n = bahn.Anzahl;
            Vector3 hoch = Vector3.up * m_WandHoehe;
            for (int i = 0; i < bahn.Stuecke; i++)
            {
                int j = (i + 1) % n;
                // "aussen" zeigt von der Fahrbahn weg
                Vector3 aussenI = bahn.Seite(i) * seitenVorzeichen;
                Vector3 aussenJ = bahn.Seite(j) * seitenVorzeichen;
                Vector3 kanteI = bahn.Kante(i, seitenVorzeichen), kanteJ = bahn.Kante(j, seitenVorzeichen);

                // Oeffnung: Das Wandstueck entfaellt ganz oder endet genau am Rand der anderen Fahrbahn.
                float von = 0f, bis = 1f;
                if (verdeckt != null && (pruefen == null || pruefen[i] || pruefen[j]))
                {
                    bool wegI = verdeckt(kanteI), wegJ = verdeckt(kanteJ);
                    if (wegI && wegJ) continue;
                    if (wegI != wegJ)
                    {
                        float drin = wegI ? 0f : 1f, frei = wegI ? 1f : 0f;
                        for (int s = 0; s < 10; s++)
                        {
                            float mitte = (drin + frei) * 0.5f;
                            if (verdeckt(Vector3.Lerp(kanteI, kanteJ, mitte))) drin = mitte; else frei = mitte;
                        }
                        if (wegI) von = frei; else bis = frei;
                    }
                }

                Vector3 innenA = Vector3.Lerp(kanteI, kanteJ, von), innenB = Vector3.Lerp(kanteI, kanteJ, bis);
                Vector3 rausA = Vector3.Lerp(aussenI, aussenJ, von).normalized, rausB = Vector3.Lerp(aussenI, aussenJ, bis).normalized;
                Vector3 aussenA = innenA + rausA * m_WandDicke, aussenB = innenB + rausB * m_WandDicke;
                Vector3 mittel = (rausA + rausB).normalized;

                bool bruecke = bahn.IstBruecke(i) && bahn.IstBruecke(j);
                Vector3 untenA = aussenA, untenB = aussenB;
                untenA.y = bruecke ? innenA.y - m_BrueckenDicke : Mathf.Min(UntergrundHoehe, innenA.y);
                untenB.y = bruecke ? innenB.y - m_BrueckenDicke : Mathf.Min(UntergrundHoehe, innenB.y);

                bau.Viereck(innenA, innenA + hoch, innenB + hoch, innenB, -mittel);       // zur Fahrbahn
                bau.Viereck(innenA + hoch, aussenA + hoch, aussenB + hoch, innenB + hoch, Vector3.up);
                bau.Viereck(untenA, aussenA + hoch, aussenB + hoch, untenB, mittel);      // nach aussen, bis zum Untergrund
            }
        }

        /// <summary>Unterseite der Brueckenplatten und die Stirnwaende, an denen der Sockel vor der Bruecke endet.</summary>
        private void BrueckenUnterbau(MeshBau bau)
        {
            int n = m_Haupt.Anzahl;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n, vorher = (i - 1 + n) % n;
                bool stueck = m_Haupt.IstBruecke(i) && m_Haupt.IstBruecke(j);
                bool stueckVorher = m_Haupt.IstBruecke(vorher) && m_Haupt.IstBruecke(i);

                Vector3 linksI = m_Haupt.Kante(i, -1f) - m_Haupt.Seite(i) * m_WandDicke;
                Vector3 rechtsI = m_Haupt.Kante(i, 1f) + m_Haupt.Seite(i) * m_WandDicke;
                Vector3 platte = Vector3.down * m_BrueckenDicke;

                if (stueck)
                {
                    Vector3 linksJ = m_Haupt.Kante(j, -1f) - m_Haupt.Seite(j) * m_WandDicke;
                    Vector3 rechtsJ = m_Haupt.Kante(j, 1f) + m_Haupt.Seite(j) * m_WandDicke;
                    bau.Viereck(linksI + platte, linksJ + platte, rechtsJ + platte, rechtsI + platte, Vector3.down);
                }

                if (stueck != stueckVorher)
                {
                    Vector3 linksUnten = new Vector3(linksI.x, UntergrundHoehe, linksI.z);
                    Vector3 rechtsUnten = new Vector3(rechtsI.x, UntergrundHoehe, rechtsI.z);
                    // Sichtbar von der offenen Seite, also von unter der Bruecke aus.
                    Vector3 zurBruecke = stueck ? m_Haupt.richtung[i] : -m_Haupt.richtung[i];
                    bau.Viereck(linksUnten, linksI + platte, rechtsI + platte, rechtsUnten, zurBruecke);
                }
            }
        }

        /// <summary>Fahrtrichtung an einer Stelle einschliesslich Steigung.</summary>
        private static Vector3 Gefaelle(StreckenBahn bahn, int stelle)
        {
            int n = bahn.Anzahl;
            int vor = bahn.geschlossen ? (stelle + 1) % n : Mathf.Min(stelle + 1, n - 1);
            int zurueck = bahn.geschlossen ? (stelle - 1 + n) % n : Mathf.Max(stelle - 1, 0);
            Vector3 richtung = bahn.mitte[vor] - bahn.mitte[zurueck];
            return richtung.sqrMagnitude > 1e-6f ? richtung.normalized : bahn.richtung[stelle];
        }

        /// <summary>Abzweigung, die an dieser Stelle der Hauptrunde neben ihr laeuft, und der zugehoerige Punkt. Sonst null.</summary>
        private StreckenBahn AbzweigAn(int stelle, out int punkt)
        {
            int n = m_Haupt.Anzahl;
            foreach (StreckenBahn abzweig in m_Abzweige)
            {
                punkt = ((stelle - abzweig.vonStelle) % n + n) % n;
                if (punkt < abzweig.Anzahl) return abzweig;
            }
            punkt = 0;
            return null;
        }

        private Checkpoint[] CheckpointsBauen(Transform wurzel)
        {
            Transform eltern = new GameObject("Checkpoints").transform;
            eltern.SetParent(wurzel, false);

            var zielMarke = new MeshBau();
            var torMarken = new MeshBau();

            // Unter einer Bruecke darf ein Tor nicht bis zur Fahrbahn darueber reichen, sonst loest es auch oben aus.
            float torHoehe = m_BrueckenAnzahl > 0 ? Mathf.Min(m_TorHoehe, m_WandHoehe + 3f) : m_TorHoehe;

            int n = m_Haupt.Anzahl;
            int anzahl = Mathf.Clamp(m_CheckpointAnzahl, 2, Mathf.Max(2, n / 2));
            var checkpoints = new Checkpoint[anzahl];
            for (int id = 0; id < anzahl; id++)
            {
                int stelle = Mathf.RoundToInt(id * (float)n / anzahl) % n;
                bool ziel = id == 0;
                Vector3 mitte = m_Haupt.mitte[stelle];
                float breite = m_Haupt.breite[stelle];

                // Das Tor selbst ist nur ein Trigger, die sichtbare Markierung liegt im gemeinsamen Mesh.
                var tor = new GameObject(ziel ? "Ziellinie" : "Checkpoint " + id);
                tor.transform.SetParent(eltern, false);
                tor.transform.localPosition = mitte;
                tor.transform.localRotation = Quaternion.LookRotation(m_Haupt.richtung[stelle], Vector3.up);

                // Etwas breiter als die Fahrbahn, damit man auch an der Wand entlang nicht vorbeikommt.
                var box = tor.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.center = new Vector3(0f, torHoehe * 0.5f, 0f);
                box.size = new Vector3(breite + m_WandDicke * 2f + 6f, torHoehe, m_TorTiefe);

                var checkpoint = tor.AddComponent<Checkpoint>();
                checkpoint.m_ID = id;               // 0 = Ziel, danach in Fahrtrichtung aufsteigend
                checkpoint.isFinishLine = ziel;
                checkpoints[id] = checkpoint;

                Vector3 gefaelle = Gefaelle(m_Haupt, stelle);
                if (ziel) zielMarke.BodenRechteck(mitte, gefaelle, breite, 5f, 0.09f);
                else if (m_CheckpointsSichtbar) torMarken.BodenRechteck(mitte, gefaelle, breite, 2.5f, 0.08f);

                // Laeuft hier eine Abzweigung neben der Hauptrunde, gilt das Tor auch dort: zweiter Trigger am selben Tor.
                StreckenBahn abzweig = AbzweigAn(stelle, out int punkt);
                if (abzweig == null || ziel) continue;

                Vector3 lokal = tor.transform.InverseTransformPoint(wurzel.TransformPoint(abzweig.mitte[punkt]));
                var zweite = tor.AddComponent<BoxCollider>();
                zweite.isTrigger = true;
                zweite.center = lokal + new Vector3(0f, torHoehe * 0.5f, 0f);
                // Die Abzweigung laeuft hier schraeg zum Tor; breiter und tiefer, damit niemand daran vorbeikommt.
                zweite.size = new Vector3(abzweig.breite[punkt] * 1.4f + m_WandDicke * 2f + 6f, torHoehe, m_TorTiefe * 1.5f);
                if (m_CheckpointsSichtbar)
                    torMarken.BodenRechteck(abzweig.mitte[punkt], Gefaelle(abzweig, punkt), abzweig.breite[punkt], 2.5f, 0.1f);
            }

            MeshObjekt("Ziellinie-Markierung", wurzel, zielMarke.Fertig("Ziellinie"), m_ZielMaterial, false);
            if (!torMarken.Leer) MeshObjekt("Checkpoint-Markierungen", wurzel, torMarken.Fertig("Checkpoints"), m_CheckpointMaterial, false);
            return checkpoints;
        }

        /// <summary>Stelle auf der Mittellinie, die um die angegebene Strecke vor der Ziellinie liegt.</summary>
        private int StelleVorZiel(float strecke)
        {
            int n = m_Haupt.Anzahl;
            float stueck = Mathf.Max(0.01f, Vector3.Distance(m_Haupt.mitte[0], m_Haupt.mitte[1]));
            return ((-Mathf.RoundToInt(strecke / stueck)) % n + n) % n;
        }

        private Transform[] StartBauen(Transform wurzel)
        {
            Transform eltern = new GameObject("Start").transform;
            eltern.SetParent(wurzel, false);

            var marken = new MeshBau();
            int anzahl = Mathf.Max(1, m_Startplaetze);
            var plaetze = new Transform[anzahl];
            Vector3 ziellinie = m_Haupt.mitte[0];
            Vector3 geradeaus = m_Haupt.richtung[0];   // Fahrtrichtung an der Ziellinie; davor ist die Strecke gerade und eben

            // Die Aufstellung steht in Fahrtrichtung VOR der Ziellinie. Die erste Ueberquerung
            // direkt nach dem Start beginnt dann Runde 1.
            float hinterstes = 0f;
            float breiteste = 0f;
            for (int i = 0; i < anzahl; i++)
            {
                int reihe = i / 2;
                float zurueck = m_StartAbstand + reihe * m_ReihenAbstand;
                hinterstes = Mathf.Max(hinterstes, zurueck);
                int stelle = StelleVorZiel(zurueck);
                float breite = m_Haupt.breite[stelle];
                breiteste = Mathf.Max(breiteste, breite);

                // Alle Plaetze liegen auf derselben Geraden durch die Ziellinie, mit deren Richtung.
                Vector3 seite = Vector3.Cross(Vector3.up, geradeaus);
                float versatz = anzahl == 1 ? 0f : (i % 2 == 0 ? -1f : 1f) * breite * 0.2f;

                var platz = new GameObject("StartPosition " + (i + 1));
                platz.transform.SetParent(eltern, false);
                platz.transform.localPosition = ziellinie - geradeaus * zurueck + seite * versatz;
                platz.transform.localRotation = Quaternion.LookRotation(geradeaus, Vector3.up);
                plaetze[i] = platz.transform;

                marken.BodenRechteck(platz.transform.localPosition, geradeaus, breite * 0.28f, m_ReihenAbstand * 0.8f, 0.07f);
            }
            MeshObjekt("Start-Markierungen", wurzel, marken.Fertig("Startplaetze"), m_StartMaterial, false);

            // Schutzzone ueber der Aufstellung: hier toeten Ghosts nicht (Tag "CollisionIgnorer", wie in der 2D-Szene).
            float laenge = hinterstes + m_ReihenAbstand;
            var zone = new GameObject("CollisionIgnorer");
            zone.tag = "CollisionIgnorer";
            zone.transform.SetParent(eltern, false);
            zone.transform.localPosition = ziellinie - geradeaus * (laenge * 0.5f);
            zone.transform.localRotation = Quaternion.LookRotation(geradeaus, Vector3.up);
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, m_TorHoehe * 0.5f, 0f);
            box.size = new Vector3(breiteste + m_WandDicke * 2f, m_TorHoehe, laenge);

            return plaetze;
        }

        /// <summary>
        /// Verteilt Treibstoff-Kanister ueber die Runde. Lage und Seite haengen nur vom Seed ab,
        /// sind also bei allen Spielern gleich. Eingesammelt wird lokal: Jeder Spieler hat seine eigenen.
        /// Jede Abzweigung bekommt in ihrer Mitte einen eigenen Kanister: der Lohn fuer den schmaleren Weg.
        /// </summary>
        private void KanisterBauen(Transform wurzel, int seed)
        {
            if (m_KanisterAnzahl <= 0) return;

            Transform eltern = new GameObject("Kanister").transform;
            eltern.SetParent(wurzel, false);

            Mesh mesh = KanisterMesh();
            var zufall = new System.Random(unchecked(seed * 31 + 17));
            int n = m_Haupt.Anzahl;
            int nummer = 0;
            for (int i = 0; i < m_KanisterAnzahl; i++)
            {
                // Gleichmaessig verteilt, um eine halbe Teilung versetzt: nie in der Startaufstellung.
                int stelle = Mathf.RoundToInt((i + 0.5f) * n / m_KanisterAnzahl) % n;
                float seitlich = ((float)zufall.NextDouble() * 2f - 1f) * m_Haupt.breite[stelle] * 0.3f;
                KanisterSetzen(eltern, mesh, m_Haupt.mitte[stelle] + m_Haupt.Seite(stelle) * seitlich, ++nummer);
            }
            foreach (StreckenBahn abzweig in m_Abzweige)
                KanisterSetzen(eltern, mesh, abzweig.mitte[abzweig.Anzahl / 2], ++nummer);
        }

        private void KanisterSetzen(Transform eltern, Mesh mesh, Vector3 position, int nummer)
        {
            var objekt = new GameObject("Kanister " + nummer);
            objekt.transform.SetParent(eltern, false);
            objekt.transform.localPosition = position;

            // Grosszuegiger Trigger: Einsammeln soll sich nie knapp verfehlt anfuehlen.
            // Nicht hoeher als die Waende, damit er unter einer Bruecke nicht bis zur Fahrbahn darueber reicht.
            float hoehe = Mathf.Min(m_KanisterGroesse * 3f, m_WandHoehe + 3f);
            var box = objekt.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, hoehe * 0.5f - 1f, 0f);
            box.size = new Vector3(m_KanisterGroesse * 2.2f, hoehe, m_KanisterGroesse * 2.2f);

            GameObject optik = MeshObjekt("Optik", objekt.transform, mesh, m_KanisterMaterial, true);
            optik.transform.localPosition = new Vector3(0f, m_KanisterGroesse * 0.9f, 0f);

            var kanister = objekt.AddComponent<TreibstoffKanister>();
            kanister.m_Menge = m_KanisterMenge;
            kanister.m_RespawnSekunden = m_KanisterRespawn;
        }

        /// <summary>Ein Kanister aus drei Quadern: Koerper, Griff und Ausguss. Alle Kanister teilen sich dieses Mesh.</summary>
        private Mesh KanisterMesh()
        {
            float g = m_KanisterGroesse;
            var bau = new MeshBau();
            bau.Quader(Vector3.zero, new Vector3(g * 0.8f, g, g * 0.38f));                               // Koerper
            bau.Quader(new Vector3(-g * 0.1f, g * 0.62f, 0f), new Vector3(g * 0.42f, g * 0.1f, g * 0.2f));   // Griff
            bau.Quader(new Vector3(-g * 0.31f, g * 0.55f, 0f), new Vector3(g * 0.08f, g * 0.14f, g * 0.2f)); // Griffstuetze
            bau.Quader(new Vector3(g * 0.11f, g * 0.55f, 0f), new Vector3(g * 0.08f, g * 0.14f, g * 0.2f));  // Griffstuetze
            bau.Quader(new Vector3(g * 0.29f, g * 0.6f, 0f), new Vector3(g * 0.14f, g * 0.22f, g * 0.14f));  // Ausguss
            return bau.Fertig("Kanister");
        }

        private void OnDrawGizmosSelected()
        {
            // Mittellinien im Editor anzeigen: Hauptrunde tuerkis, Bruecken orange, Abzweigungen gruen.
            if (!HatVerlauf) return;
            GizmoLinie(m_Haupt, new Color(0.1f, 0.83f, 0.85f), new Color(1f, 0.55f, 0.1f));
            if (m_Abzweige == null) return;
            foreach (StreckenBahn abzweig in m_Abzweige) GizmoLinie(abzweig, new Color(0.35f, 0.9f, 0.35f), Color.white);
        }

        private void GizmoLinie(StreckenBahn bahn, Color farbe, Color brueckenFarbe)
        {
            int n = bahn.Anzahl;
            for (int i = 0; i < bahn.Stuecke; i++)
            {
                int j = (i + 1) % n;
                Gizmos.color = bahn.IstBruecke(i) && bahn.IstBruecke(j) ? brueckenFarbe : farbe;
                Gizmos.DrawLine(transform.TransformPoint(bahn.mitte[i]) + Vector3.up, transform.TransformPoint(bahn.mitte[j]) + Vector3.up);
            }
        }
    }
}
