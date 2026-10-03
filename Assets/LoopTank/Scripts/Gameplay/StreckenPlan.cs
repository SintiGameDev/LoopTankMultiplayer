using System.Collections.Generic;
using UnityEngine;

namespace TopDownRace
{
    /// <summary>Grundform der Strecke. "Zufall" waehlt je Seed eine der anderen.</summary>
    public enum StreckenForm
    {
        Zufall = 0,
        /// <summary>Geschlossene Runde mit Einbuchtungen (Links- und Rechtskurven), ohne Kreuzung.</summary>
        Rundkurs = 1,
        /// <summary>Liegende Acht: Die Strecke kreuzt sich in der Mitte ueber eine Bruecke.</summary>
        Acht = 2,
        /// <summary>Grosse Runde mit einer Schleife nach innen, die unter bzw. ueber der Strecke durchfuehrt.</summary>
        Schleife = 3,
    }

    /// <summary>
    /// Eine Fahrspur der Strecke: die Hauptrunde (geschlossen) oder eine Abzweigung (offen, beginnt
    /// und endet auf der Hauptrunde). Wird in der Szene gespeichert, damit Panzer und Ghosts auch
    /// bei einer im Editor erzeugten Strecke die Hoehe der Fahrbahn abfragen koennen.
    /// </summary>
    [System.Serializable]
    public class StreckenBahn
    {
        public bool geschlossen;
        [Tooltip("Mittellinie. Y ist die Höhe der Fahrbahn.")]
        public Vector3[] mitte;
        [Tooltip("Fahrtrichtung in der Ebene (Y = 0).")]
        public Vector3[] richtung;
        public float[] breite;
        [Tooltip("True: Unter diesem Punkt fährt die Strecke hindurch (Brücke, unten offen).")]
        public bool[] bruecke;
        [Tooltip("Nur Abzweigung: Stelle der Hauptrunde, an der sie beginnt.")]
        public int vonStelle;
        [Tooltip("Nur Abzweigung: -1 = links der Hauptrunde, +1 = rechts.")]
        public int seite;

        public int Anzahl => mitte != null ? mitte.Length : 0;
        public int Stuecke => geschlossen ? Anzahl : Mathf.Max(0, Anzahl - 1);

        /// <summary>Zeigt in Fahrtrichtung nach rechts.</summary>
        public Vector3 Seite(int i) => Vector3.Cross(Vector3.up, richtung[i]);

        /// <summary>Punkt auf dem Fahrbahnrand. vorzeichen -1 = links, +1 = rechts.</summary>
        public Vector3 Kante(int i, float vorzeichen) => mitte[i] + Seite(i) * (vorzeichen * breite[i] * 0.5f);

        public bool IstBruecke(int i) => bruecke != null && i < bruecke.Length && bruecke[i];

        /// <summary>Abstand in der Ebene zur Mittellinie, dazu die halbe Breite an der naechsten Stelle.</summary>
        public float Abstand(Vector3 p, out float halbeBreite)
        {
            float beste = float.MaxValue;
            halbeBreite = 0f;
            int n = Anzahl, stuecke = Stuecke;
            for (int i = 0; i < stuecke; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = mitte[i], c = mitte[j];
                float dx = c.x - a.x, dz = c.z - a.z;
                float l2 = dx * dx + dz * dz;
                float t = l2 > 1e-6f ? Mathf.Clamp01(((p.x - a.x) * dx + (p.z - a.z) * dz) / l2) : 0f;
                float qx = a.x + dx * t - p.x, qz = a.z + dz * t - p.z;
                float d2 = qx * qx + qz * qz;
                if (d2 < beste)
                {
                    beste = d2;
                    halbeBreite = Mathf.Lerp(breite[i], breite[j], t) * 0.5f;
                }
            }
            return Mathf.Sqrt(beste);
        }

        /// <summary>
        /// Sucht die Fahrbahn unter (oder ueber) dem Punkt p. Wo zwei Ebenen uebereinander liegen
        /// (Bruecke), gewinnt die, deren Hoehe naeher an p.y liegt. Verbessert das bisher beste
        /// Ergebnis nur, wenn dieses Stueck besser passt.
        /// </summary>
        public void Boden(Vector3 p, ref float besteKosten, ref float hoehe, ref Vector3 normale)
        {
            int n = Anzahl, stuecke = Stuecke;
            int bestes = -1;
            for (int i = 0; i < stuecke; i++)
            {
                int j = (i + 1) % n;
                Vector3 a = mitte[i], c = mitte[j];
                float dx = c.x - a.x, dz = c.z - a.z;
                float l2 = dx * dx + dz * dz;
                float t = l2 > 1e-6f ? Mathf.Clamp01(((p.x - a.x) * dx + (p.z - a.z) * dz) / l2) : 0f;
                float qx = a.x + dx * t - p.x, qz = a.z + dz * t - p.z;
                float d2 = qx * qx + qz * qz;

                float halb = Mathf.Lerp(breite[i], breite[j], t) * 0.5f + 2f;
                float h = a.y + (c.y - a.y) * t;
                float kosten = Mathf.Abs(h - p.y);
                if (d2 > halb * halb) kosten += (Mathf.Sqrt(d2) - halb) * 3f;   // neben der Fahrbahn
                if (kosten < besteKosten)
                {
                    besteKosten = kosten;
                    hoehe = h;
                    bestes = i;
                }
            }

            if (bestes < 0) return;
            Vector3 entlang = mitte[(bestes + 1) % n] - mitte[bestes];
            Vector3 quer = Vector3.Cross(Vector3.up, entlang);
            Vector3 hoch = Vector3.Cross(entlang, quer);
            if (hoch.sqrMagnitude > 1e-8f) normale = hoch.normalized;
        }
    }

    /// <summary>
    /// Rechnet den Verlauf einer Strecke aus: Mittellinie, Breite, Hoehe, Bruecken und
    /// Abzweigungen. Reine Mathematik ohne Szenenobjekte; die Meshes baut der StreckenGenerator.
    ///
    /// Ablauf je Versuch: Kontrollpunkte der gewaehlten Form -> Kurve -> gleich lange Stuecke ->
    /// Startgerade -> Kreuzungen suchen -> Hoehenprofil (Bruecken, Huegel, Buckel) -> pruefen.
    /// Passt etwas nicht (Kurve zu eng, Strecken zu nah, Rampe zu steil), wird mit sanfterer Form
    /// neu gewuerfelt. Der Zufall kommt aus System.Random und ist auf jedem Rechner gleich.
    /// </summary>
    public class StreckenPlan
    {
        public struct Werte
        {
            public int seed;
            public StreckenForm form;
            public bool richtungZufaellig;
            public int kontrollpunkte;
            public float radius, unregelmaessig, stuecklaenge;
            public float breite, breitenVariation;
            public int breitenAbschnitte;
            public float wandDicke;
            public float startGerade, geradeNachZiel;
            public float hoehenunterschied, maxSteigung, huegelAbstand, brueckenHoehe;
            public int buckelAbschnitte;
            public float buckelHoehe, buckelLaenge;
            public int abzweigungen;
            public float abzweigLaenge, abzweigBreite, inselBreite;
        }

        public StreckenBahn Haupt;
        public readonly List<StreckenBahn> Abzweige = new List<StreckenBahn>();
        /// <summary>Tatsaechlich gebaute Form (nie "Zufall").</summary>
        public StreckenForm Form;
        public int Bruecken;
        /// <summary>True: Kein Versuch war befahrbar, es wurde ein flacher Kreis gebaut.</summary>
        public bool Notfall;
        public int Versuche;

        private const int VersucheMitForm = 12;
        private const int VersucheGesamt = 24;

        private Werte w;
        private readonly List<Vector3> m_Mitte = new List<Vector3>();
        private Vector3[] m_Richtung;
        private float[] m_Breite;
        private float[] m_Hoehe;
        private bool[] m_Bruecke;
        private int[] m_Belegt;          // -1 = frei, sonst Nummer des festen Hoehenbereichs (0 = Startgerade)
        private float m_Schritt;
        private int m_Fenster, m_NachZiel;
        private int m_Nachbarn;

        private struct Kreuzung { public int aStart, aLaenge, bStart, bLaenge; }
        private readonly List<Kreuzung> m_Kreuzungen = new List<Kreuzung>();

        private struct Schluessel
        {
            public int pos; public float hoehe; public bool fest; public bool luecke;
            public Schluessel(int pos, float hoehe, bool fest, bool luecke) { this.pos = pos; this.hoehe = hoehe; this.fest = fest; this.luecke = luecke; }
        }

        public static StreckenPlan Bauen(Werte werte)
        {
            StreckenForm form = werte.form;
            if (form == StreckenForm.Zufall)
            {
                double wahl = new System.Random(unchecked(werte.seed * 7919 + 13)).NextDouble();
                form = wahl < 0.4 ? StreckenForm.Rundkurs : wahl < 0.78 ? StreckenForm.Acht : StreckenForm.Schleife;
            }

            float staerke = 1f;
            for (int versuch = 0; versuch < VersucheGesamt; versuch++)
            {
                // Klappt die gewuenschte Form mehrfach nicht, bleibt der Rundkurs: Er gelingt fast immer.
                if (versuch == VersucheMitForm) { form = StreckenForm.Rundkurs; staerke = 1f; }

                var plan = new StreckenPlan { w = werte, Form = form, Versuche = versuch + 1 };
                var zufall = new System.Random(unchecked(werte.seed * 7919 + versuch * 104729));
                if (plan.Versuch(zufall, form, staerke, false)) return plan;
                staerke *= 0.86f;
            }

            // Letzter Ausweg, immer gueltig: ein flacher Kreis mit gleichbleibender Breite.
            var kreis = new StreckenPlan { w = werte, Form = StreckenForm.Rundkurs, Notfall = true, Versuche = VersucheGesamt + 1 };
            kreis.w.richtungZufaellig = false;
            kreis.Versuch(new System.Random(werte.seed), StreckenForm.Rundkurs, 0f, true);
            kreis.FertigStellen();
            return kreis;
        }

        // ------------------------------------------------------------------
        // Ein Versuch
        // ------------------------------------------------------------------

        private bool Versuch(System.Random zufall, StreckenForm form, float staerke, bool flach)
        {
            Vector3[] punkte = Kontrollpunkte(zufall, form, staerke);
            if (w.richtungZufaellig && zufall.Next(2) == 0) System.Array.Reverse(punkte);

            // Breite je Abschnitt. Um Start und Ziel herum nie schmaler als der Mittelwert,
            // damit die Startaufstellung Platz hat.
            int abschnitte = Mathf.Max(3, w.breitenAbschnitte);
            var faktoren = new float[abschnitte];
            for (int i = 0; i < abschnitte; i++)
                faktoren[i] = 1f + ((float)zufall.NextDouble() * 2f - 1f) * (flach ? 0f : w.breitenVariation);
            faktoren[0] = Mathf.Max(1f, faktoren[0]);
            faktoren[abschnitte - 1] = Mathf.Max(1f, faktoren[abschnitte - 1]);

            Abtasten(punkte);
            int n = m_Mitte.Count;

            // Die Startgerade darf nicht in einer Kreuzung oder auf einer Brueckenrampe liegen.
            List<int> roh = Schnittpunkte();
            if (roh.Count > 6) return false;   // mehr als drei Kreuzungen: zu wirr
            float rampe = 1.5f * w.brueckenHoehe / Mathf.Max(0.02f, w.maxSteigung);
            int sperre = Mathf.CeilToInt((rampe + w.breite * 1.5f) / m_Schritt) + 2;
            var gesperrt = new bool[n];
            foreach (int stelle in roh)
                for (int d = -sperre; d <= sperre + 1; d++) gesperrt[((stelle + d) % n + n) % n] = true;
            if (!StartGeradeEinrichten(gesperrt)) return false;

            m_Richtung = new Vector3[n];
            m_Breite = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector3 richtung = m_Mitte[(i + 1) % n] - m_Mitte[(i - 1 + n) % n];
                richtung.y = 0f;
                m_Richtung[i] = richtung.normalized;

                // Breite: weich zwischen den Abschnittswerten ueberblenden
                float lage = i / (float)n * abschnitte;
                int a = Mathf.FloorToInt(lage) % abschnitte, b = (a + 1) % abschnitte;
                float t = Mathf.SmoothStep(0f, 1f, lage - Mathf.Floor(lage));
                m_Breite[i] = w.breite * Mathf.Lerp(faktoren[a], faktoren[b], t);
            }

            // Der Notfall-Kreis (flach) wird nicht geprueft: Er ist das, was bleibt, wenn nichts anderes passt.
            if (!flach)
            {
                if (!KantenGueltig(m_Mitte, m_Richtung, m_Breite, true)) return false;
                if (!KreuzungenFinden()) return false;
            }
            if (!HoehenBauen(zufall, flach)) return false;

            if (flach) return true;
            FertigStellen();
            AbzweigeBauen(zufall);
            return true;
        }

        private void FertigStellen()
        {
            int n = m_Mitte.Count;
            var mitte = new Vector3[n];
            for (int i = 0; i < n; i++) mitte[i] = new Vector3(m_Mitte[i].x, m_Hoehe[i], m_Mitte[i].z);
            Haupt = new StreckenBahn { geschlossen = true, mitte = mitte, richtung = m_Richtung, breite = m_Breite, bruecke = m_Bruecke };
            Bruecken = m_Kreuzungen.Count;
        }

        // ------------------------------------------------------------------
        // Formen
        // ------------------------------------------------------------------

        private static float Streuung(System.Random zufall) => (float)zufall.NextDouble() * 2f - 1f;

        /// <summary>1 bei Winkelabstand 0, faellt mit der Weite (im Bogenmass) weich auf 0.</summary>
        private static float Glocke(float winkelAbstand, float weite)
        {
            float d = Mathf.Abs(winkelAbstand) % (Mathf.PI * 2f);
            if (d > Mathf.PI) d = Mathf.PI * 2f - d;
            return Mathf.Exp(-(d * d) / (weite * weite));
        }

        private Vector3[] Kontrollpunkte(System.Random zufall, StreckenForm form, float staerke)
        {
            float unregelmaessig = w.unregelmaessig * staerke;

            if (form == StreckenForm.Acht)
            {
                // Liegende Acht: x = a sin t, z = b sin 2t. Sie kreuzt sich in der Mitte fast im rechten Winkel.
                int k = Mathf.Max(8, w.kontrollpunkte);
                float a = w.radius * 1.2f, b = w.radius * 0.56f;
                float schlaufe = 1f + Streuung(zufall) * 0.22f * staerke;   // eine Schlaufe groesser als die andere
                var punkte = new Vector3[k];
                for (int i = 0; i < k; i++)
                {
                    float t = (i + 0.5f + Streuung(zufall) * 0.15f * staerke) / k * Mathf.PI * 2f;
                    float faktor = 1f + Streuung(zufall) * unregelmaessig * 0.55f;
                    var p = new Vector3(a * Mathf.Sin(t), 0f, b * Mathf.Sin(2f * t)) * faktor;
                    punkte[i] = p.x > 0f ? p * schlaufe : p / schlaufe;
                }
                return punkte;
            }

            if (form == StreckenForm.Schleife)
            {
                // Pascalsche Schnecke r = c + cos(winkel): eine grosse Runde mit einer Schleife nach innen.
                int k = Mathf.Max(10, w.kontrollpunkte);
                float c = 0.36f + Streuung(zufall) * 0.04f * staerke;
                float groesse = w.radius * 1.05f;
                var punkte = new Vector3[k];
                for (int i = 0; i < k; i++)
                {
                    float winkel = (i + 0.5f) / k * Mathf.PI * 2f;
                    float radius = groesse * (c + Mathf.Cos(winkel)) * (1f + Streuung(zufall) * unregelmaessig * 0.35f);
                    punkte[i] = new Vector3(Mathf.Cos(winkel) * radius, 0f, Mathf.Sin(winkel) * radius);
                }
                return punkte;
            }

            // Rundkurs: Punkte reihum um die Mitte mit zufaelligem Abstand. Ein oder zwei breite
            // Einbuchtungen ziehen die Runde nach innen. Dort dreht die Strecke in die Gegenrichtung.
            {
                int k = Mathf.Max(5, w.kontrollpunkte);
                float streckung = 1f + (float)zufall.NextDouble() * 0.3f * staerke;
                int einbuchtungen = zufall.Next(1, 3);
                float ersteLage = (float)zufall.NextDouble() * Mathf.PI * 2f;
                float zweiteLage = ersteLage + Mathf.PI * (0.7f + (float)zufall.NextDouble() * 0.6f);
                float ersteTiefe = (0.5f + (float)zufall.NextDouble() * 0.22f) * staerke;
                float zweiteTiefe = einbuchtungen >= 2 ? (0.4f + (float)zufall.NextDouble() * 0.3f) * staerke : 0f;
                float ersteWeite = 0.36f + (float)zufall.NextDouble() * 0.22f;
                float zweiteWeite = 0.36f + (float)zufall.NextDouble() * 0.22f;

                var punkte = new Vector3[k];
                for (int i = 0; i < k; i++)
                {
                    float winkel = (i + Streuung(zufall) * 0.2f * staerke) / k * Mathf.PI * 2f;
                    float radius = w.radius * 1.08f * (1f + Streuung(zufall) * unregelmaessig * 0.7f);
                    radius *= 1f - ersteTiefe * Glocke(winkel - ersteLage, ersteWeite) - zweiteTiefe * Glocke(winkel - zweiteLage, zweiteWeite);
                    punkte[i] = new Vector3(Mathf.Cos(winkel) * radius * streckung, 0f, Mathf.Sin(winkel) * radius / streckung);
                }
                return punkte;
            }
        }

        // ------------------------------------------------------------------
        // Mittellinie
        // ------------------------------------------------------------------

        /// <summary>Kurve durch die Kontrollpunkte (Catmull-Rom, geschlossen), in gleich lange Stuecke geteilt.</summary>
        private void Abtasten(Vector3[] punkte)
        {
            int k = punkte.Length;
            var dicht = new List<Vector3>();
            const int Teile = 24;
            for (int i = 0; i < k; i++)
            {
                Vector3 p0 = punkte[(i - 1 + k) % k], p1 = punkte[i], p2 = punkte[(i + 1) % k], p3 = punkte[(i + 2) % k];
                for (int s = 0; s < Teile; s++) dicht.Add(CatmullRom(p0, p1, p2, p3, s / (float)Teile));
            }

            float laenge = 0f;
            for (int i = 0; i < dicht.Count; i++) laenge += Vector3.Distance(dicht[i], dicht[(i + 1) % dicht.Count]);
            int anzahl = Mathf.Max(12, Mathf.RoundToInt(laenge / Mathf.Max(2f, w.stuecklaenge)));
            m_Schritt = laenge / anzahl;

            m_Mitte.Clear();
            float gelaufen = 0f, naechster = 0f;
            for (int i = 0; i < dicht.Count && m_Mitte.Count < anzahl; i++)
            {
                Vector3 a = dicht[i], b = dicht[(i + 1) % dicht.Count];
                float stueck = Vector3.Distance(a, b);
                while (naechster <= gelaufen + stueck && m_Mitte.Count < anzahl)
                {
                    m_Mitte.Add(Vector3.Lerp(a, b, stueck > 0f ? (naechster - gelaufen) / stueck : 0f));
                    naechster += m_Schritt;
                }
                gelaufen += stueck;
            }
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        /// <summary>
        /// Sorgt fuer eine echte Gerade an Start und Ziel: sucht das geradeste erlaubte Stueck der
        /// Runde, zieht es ganz gerade und legt die Ziellinie (Stelle 0) kurz vor sein Ende.
        /// </summary>
        private bool StartGeradeEinrichten(bool[] gesperrt)
        {
            int n = m_Mitte.Count;
            int fenster = Mathf.Clamp(Mathf.RoundToInt(w.startGerade / Mathf.Max(0.01f, m_Schritt)), 3, n / 3);
            int nachZiel = Mathf.Clamp(Mathf.RoundToInt(w.geradeNachZiel / Mathf.Max(0.01f, m_Schritt)), 1, fenster - 2);

            // Geradestes Fenster: dort ist die Summe der Richtungsaenderungen am kleinsten.
            int bester = -1;
            float kleinste = float.MaxValue;
            for (int s = 0; s < n; s++)
            {
                bool frei = true;
                for (int j = 0; j <= fenster && frei; j++) frei = !gesperrt[(s + j) % n];
                if (!frei) continue;

                float biegung = 0f;
                for (int j = 0; j < fenster - 1; j++)
                {
                    Vector3 a = m_Mitte[(s + j + 1) % n] - m_Mitte[(s + j) % n];
                    Vector3 b = m_Mitte[(s + j + 2) % n] - m_Mitte[(s + j + 1) % n];
                    biegung += Vector3.Angle(a, b);
                }
                if (biegung < kleinste)
                {
                    kleinste = biegung;
                    bester = s;
                }
            }
            if (bester < 0) return false;

            // Ganz gerade ziehen: alle Punkte des Fensters auf die Linie zwischen seinen Enden.
            Vector3 anfang = m_Mitte[bester], ende = m_Mitte[(bester + fenster) % n];
            for (int j = 1; j < fenster; j++)
                m_Mitte[(bester + j) % n] = Vector3.Lerp(anfang, ende, j / (float)fenster);

            // Liste so drehen, dass die Ziellinie (Stelle 0) kurz vor dem Ende der Geraden liegt.
            int ziel = (bester + fenster - nachZiel) % n;
            var gedreht = new List<Vector3>(n);
            for (int i = 0; i < n; i++) gedreht.Add(m_Mitte[(ziel + i) % n]);
            m_Mitte.Clear();
            m_Mitte.AddRange(gedreht);

            m_Fenster = fenster;
            m_NachZiel = nachZiel;
            return true;
        }

        // ------------------------------------------------------------------
        // Pruefungen
        // ------------------------------------------------------------------

        /// <summary>
        /// Die Fahrbahnraender duerfen in einer Kurve nicht stehen bleiben oder rueckwaerts laufen.
        /// Sonst ist die Kurve zu eng fuer die Breite und die Waende ueberschneiden sich.
        /// </summary>
        private static bool KantenGueltig(IList<Vector3> mitte, Vector3[] richtung, float[] breite, bool geschlossen)
        {
            int n = mitte.Count;
            int stuecke = geschlossen ? n : n - 1;
            for (int i = 0; i < stuecke; i++)
            {
                int j = (i + 1) % n;
                Vector3 seiteI = Vector3.Cross(Vector3.up, richtung[i]) * (breite[i] * 0.5f);
                Vector3 seiteJ = Vector3.Cross(Vector3.up, richtung[j]) * (breite[j] * 0.5f);
                Vector3 fahrt = mitte[j] - mitte[i];
                fahrt.y = 0f;
                float grenze = fahrt.sqrMagnitude * 0.25f;
                if (grenze < 1e-4f) return false;
                if (Vector3.Dot(fahrt - seiteJ + seiteI, fahrt) <= grenze) return false;   // linker Rand
                if (Vector3.Dot(fahrt + seiteJ - seiteI, fahrt) <= grenze) return false;   // rechter Rand
            }
            return true;
        }

        private int Zyklisch(int i, int j)
        {
            int n = m_Mitte.Count;
            int abstand = Mathf.Abs(i - j) % n;
            return Mathf.Min(abstand, n - abstand);
        }

        private int Stelle(int i)
        {
            int n = m_Mitte.Count;
            return (i % n + n) % n;
        }

        /// <summary>Mindestabstand zweier Mittellinien, damit beide Fahrbahnen samt Waenden nebeneinander passen.</summary>
        private float Noetig(float breiteA, float breiteB) => (breiteA + breiteB) * 0.5f + w.wandDicke * 2f + 6f;

        private static float AbstandQuadrat(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        private bool Nah(int i, int j)
        {
            float noetig = Noetig(m_Breite[i], m_Breite[j]);
            return AbstandQuadrat(m_Mitte[i], m_Mitte[j]) < noetig * noetig;
        }

        /// <summary>Alle Stellen, an denen sich die Mittellinie selbst schneidet. Je Schnitt zwei Eintraege (beide Stuecke).</summary>
        private List<int> Schnittpunkte()
        {
            var ergebnis = new List<int>();
            int n = m_Mitte.Count;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 2; j < n; j++)
                {
                    if (i == 0 && j == n - 1) continue;   // Nachbarn ueber das Listenende hinweg
                    if (Schneiden(m_Mitte[i], m_Mitte[i + 1], m_Mitte[j], m_Mitte[(j + 1) % n]))
                    {
                        ergebnis.Add(i);
                        ergebnis.Add(j);
                    }
                }
            }
            return ergebnis;
        }

        private static float Drehsinn(Vector3 a, Vector3 b, Vector3 c) => (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);

        private static bool Schneiden(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float d1 = Drehsinn(c, d, a), d2 = Drehsinn(c, d, b), d3 = Drehsinn(a, b, c), d4 = Drehsinn(a, b, d);
            return ((d1 > 0f) != (d2 > 0f)) && ((d3 > 0f) != (d4 > 0f));
        }

        /// <summary>
        /// Sucht die Kreuzungen und um jede herum den Bereich, in dem sich beide Fahrbahnen
        /// ueberdecken. Dort muss spaeter eine ueber der anderen liegen. Kommt sich die Strecke
        /// irgendwo sonst zu nah, ist der Versuch ungueltig.
        /// </summary>
        private bool KreuzungenFinden()
        {
            int n = m_Mitte.Count;
            m_Kreuzungen.Clear();
            var zone = new int[n];   // 0 = keine, sonst 2k+1 (erste Fahrbahn der Kreuzung k) oder 2k+2 (zweite)

            List<int> schnitte = Schnittpunkte();
            for (int s = 0; s < schnitte.Count; s += 2)
            {
                int i0 = schnitte[s], j0 = schnitte[s + 1];
                int fenster = Mathf.Min(26, Zyklisch(i0, j0) / 2 - 2);
                if (fenster < 3) return false;

                int aMin = 0, aMax = 1, bMin = 0, bMax = 1;
                for (int da = -fenster; da <= fenster + 1; da++)
                {
                    for (int db = -fenster; db <= fenster + 1; db++)
                    {
                        if (!Nah(Stelle(i0 + da), Stelle(j0 + db))) continue;
                        aMin = Mathf.Min(aMin, da); aMax = Mathf.Max(aMax, da);
                        bMin = Mathf.Min(bMin, db); bMax = Mathf.Max(bMax, db);
                    }
                }
                // Reicht die Ueberdeckung bis an den Rand des Suchfensters, kreuzen sich die Bahnen zu flach.
                if (aMin <= -fenster || aMax >= fenster + 1 || bMin <= -fenster || bMax >= fenster + 1) return false;

                var kreuzung = new Kreuzung
                {
                    aStart = Stelle(i0 + aMin - 1), aLaenge = aMax - aMin + 3,
                    bStart = Stelle(j0 + bMin - 1), bLaenge = bMax - bMin + 3,
                };
                int nummer = m_Kreuzungen.Count;
                for (int d = 0; d < kreuzung.aLaenge; d++)
                {
                    int stelle = Stelle(kreuzung.aStart + d);
                    if (zone[stelle] != 0) return false;
                    zone[stelle] = nummer * 2 + 1;
                }
                for (int d = 0; d < kreuzung.bLaenge; d++)
                {
                    int stelle = Stelle(kreuzung.bStart + d);
                    if (zone[stelle] != 0) return false;
                    zone[stelle] = nummer * 2 + 2;
                }
                m_Kreuzungen.Add(kreuzung);
            }

            float breiteste = 0f;
            for (int i = 0; i < n; i++) breiteste = Mathf.Max(breiteste, m_Breite[i]);
            m_Nachbarn = Mathf.CeilToInt(Noetig(breiteste, breiteste) / m_Schritt) + 2;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (Zyklisch(i, j) <= m_Nachbarn || !Nah(i, j)) continue;
                    // Nah beieinander ist nur in einer Kreuzung erlaubt, und nur zwischen ihren beiden Fahrbahnen.
                    if (zone[i] == 0 || zone[j] == 0 || zone[i] == zone[j] || (zone[i] - 1) / 2 != (zone[j] - 1) / 2) return false;
                }
            }
            return true;
        }

        // ------------------------------------------------------------------
        // Hoehe
        // ------------------------------------------------------------------

        private bool Belegen(int start, int laenge, int nummer)
        {
            for (int d = 0; d < laenge; d++)
            {
                int stelle = Stelle(start + d);
                if (m_Belegt[stelle] >= 0) return false;
                m_Belegt[stelle] = nummer;
            }
            return true;
        }

        /// <summary>
        /// Hoehenprofil der Runde. Fest sind die Startgerade (Hoehe 0) und jede Kreuzung (eine
        /// Fahrbahn unten, die andere um die Brueckenhoehe darueber). Dazwischen liegen zufaellige
        /// Huegel und Senken, weich verbunden und nie steiler als die erlaubte Steigung.
        /// Zum Schluss kommen kurze Buckelstrecken dazu.
        /// </summary>
        private bool HoehenBauen(System.Random zufall, bool flach)
        {
            int n = m_Mitte.Count;
            m_Hoehe = new float[n];
            m_Bruecke = new bool[n];
            m_Belegt = new int[n];
            for (int i = 0; i < n; i++) m_Belegt[i] = -1;
            if (flach) return m_Kreuzungen.Count == 0;

            // Bereich 0: die Startgerade. Sie reicht von (Ziel - vor) bis (Ziel + nachZiel).
            var bereichHoehe = new List<float> { 0f };
            int vor = m_Fenster - m_NachZiel;
            if (!Belegen(n - vor, m_Fenster + 1, 0)) return false;

            foreach (Kreuzung kreuzung in m_Kreuzungen)
            {
                bool aOben = zufall.Next(2) == 0;
                if (!Belegen(kreuzung.aStart, kreuzung.aLaenge, bereichHoehe.Count)) return false;
                bereichHoehe.Add(aOben ? w.brueckenHoehe : 0f);
                if (!Belegen(kreuzung.bStart, kreuzung.bLaenge, bereichHoehe.Count)) return false;
                bereichHoehe.Add(aOben ? 0f : w.brueckenHoehe);

                int obenStart = aOben ? kreuzung.aStart : kreuzung.bStart;
                int obenLaenge = aOben ? kreuzung.aLaenge : kreuzung.bLaenge;
                for (int d = 0; d < obenLaenge; d++) m_Bruecke[Stelle(obenStart + d)] = true;
            }

            // Schluesselstellen einmal um die Runde, gezaehlt ab dem Ende der Startgeraden.
            int anfang = m_NachZiel;
            var fest = new List<Schluessel> { new Schluessel(0, 0f, true, true) };
            int lauf = 1;
            while (lauf < n)
            {
                int nummer = m_Belegt[(anfang + lauf) % n];
                if (nummer < 0) { lauf++; continue; }
                if (nummer == 0) break;   // wieder an der Startgeraden
                int ende = lauf;
                while (ende + 1 < n && m_Belegt[(anfang + ende + 1) % n] == nummer) ende++;
                fest.Add(new Schluessel(lauf, bereichHoehe[nummer], true, ende == lauf));
                if (ende > lauf) fest.Add(new Schluessel(ende, bereichHoehe[nummer], true, true));
                lauf = ende + 1;
            }
            fest.Add(new Schluessel(n - m_Fenster, 0f, true, false));

            // Freie Huegel und Senken in den Luecken zwischen den festen Bereichen
            float hoechste = Mathf.Max(0f, w.hoehenunterschied);
            float abstand = Mathf.Max(m_Schritt * 4f, w.huegelAbstand);
            bool hoch = zufall.Next(2) == 0;
            var alle = new List<Schluessel>();
            for (int k = 0; k < fest.Count - 1; k++)
            {
                alle.Add(fest[k]);
                if (!fest[k].luecke) continue;

                int laenge = fest[k + 1].pos - fest[k].pos;
                int anzahl = hoechste > 0f ? Mathf.Max(0, Mathf.FloorToInt(laenge * m_Schritt / abstand) - 1) : 0;
                for (int c = 0; c < anzahl; c++)
                {
                    float lage = (c + 1f + Streuung(zufall) * 0.15f) / (anzahl + 1f);
                    int pos = fest[k].pos + Mathf.Clamp(Mathf.RoundToInt(laenge * lage), 1, laenge - 1);
                    hoch = !hoch;
                    float hoehe = hoechste * (hoch ? 0.55f + 0.45f * (float)zufall.NextDouble() : 0.3f * (float)zufall.NextDouble());
                    alle.Add(new Schluessel(pos, hoehe, false, true));
                }
            }
            alle.Add(fest[fest.Count - 1]);

            // Freie Hoehen so begrenzen, dass keine Rampe zu steil wird. (Die weiche Ueberblendung
            // ist in der Mitte 1,5-mal so steil wie der Durchschnitt.)
            float jeStueck = m_Schritt * w.maxSteigung / 1.5f;
            for (int runde = 0; runde < 6; runde++)
            {
                for (int k = 1; k < alle.Count - 1; k++)
                {
                    if (alle[k].fest) continue;
                    float links = (alle[k].pos - alle[k - 1].pos) * jeStueck;
                    float rechts = (alle[k + 1].pos - alle[k].pos) * jeStueck;
                    float unten = Mathf.Max(0f, Mathf.Max(alle[k - 1].hoehe - links, alle[k + 1].hoehe - rechts));
                    float oben = Mathf.Min(alle[k - 1].hoehe + links, alle[k + 1].hoehe + rechts);
                    Schluessel s = alle[k];
                    s.hoehe = unten > oben ? (unten + oben) * 0.5f : Mathf.Clamp(s.hoehe, unten, oben);
                    alle[k] = s;
                }
            }

            for (int k = 0; k < alle.Count - 1; k++)
            {
                Schluessel a = alle[k], b = alle[k + 1];
                for (int pos = a.pos; pos <= b.pos; pos++)
                {
                    float t = b.pos > a.pos ? (pos - a.pos) / (float)(b.pos - a.pos) : 0f;
                    m_Hoehe[(anfang + pos) % n] = Mathf.Lerp(a.hoehe, b.hoehe, Mathf.SmoothStep(0f, 1f, t));
                }
            }

            // Zwischen zwei festen Bereichen (z. B. Bruecke kurz nach dem Start) kann die Rampe zu steil sein.
            for (int i = 0; i < n; i++)
            {
                if (Mathf.Abs(m_Hoehe[(i + 1) % n] - m_Hoehe[i]) > m_Schritt * w.maxSteigung * 1.2f) return false;
            }

            BuckelBauen(zufall);
            return true;
        }

        /// <summary>Kurze Abschnitte mit Bodenwellen. Bei Tempo hebt der Panzer dort kurz ab.</summary>
        private void BuckelBauen(System.Random zufall)
        {
            if (w.buckelAbschnitte <= 0 || w.buckelHoehe <= 0f) return;

            int n = m_Mitte.Count;
            var vergeben = new bool[n];
            float periode = Mathf.Max(2f, w.buckelLaenge / m_Schritt);
            for (int abschnitt = 0; abschnitt < w.buckelAbschnitte; abschnitt++)
            {
                for (int versuch = 0; versuch < 10; versuch++)
                {
                    int laenge = Mathf.RoundToInt(periode * zufall.Next(2, 5));
                    int start = zufall.Next(n);
                    if (laenge + 6 >= n) break;

                    bool frei = true;
                    for (int d = -3; d <= laenge + 3 && frei; d++)
                    {
                        int stelle = Stelle(start + d);
                        // Nur auf fast ebenen Stuecken: Buckel auf einer Rampe waeren zusammen zu steil.
                        bool eben = Mathf.Abs(m_Hoehe[Stelle(stelle + 1)] - m_Hoehe[stelle]) < m_Schritt * 0.05f;
                        frei = m_Belegt[stelle] < 0 && !vergeben[stelle] && eben;
                    }
                    if (!frei) continue;

                    for (int d = -3; d <= laenge + 3; d++) vergeben[Stelle(start + d)] = true;
                    for (int d = 0; d <= laenge; d++)
                    {
                        float welle = 0.5f - 0.5f * Mathf.Cos(d / periode * Mathf.PI * 2f);
                        float rand = Mathf.Min(1f, Mathf.Min(d, laenge - d));   // an beiden Enden auf 0
                        m_Hoehe[Stelle(start + d)] += w.buckelHoehe * welle * rand;
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------
        // Abzweigungen
        // ------------------------------------------------------------------

        /// <summary>
        /// Eine Abzweigung loest sich seitlich von der Hauptrunde, laeuft hinter einer Insel
        /// parallel und muendet wieder ein. Sie ist schmaler als die Hauptrunde. Passt sie an einer
        /// Stelle nicht (zu enge Kurve, andere Fahrbahn im Weg), wird eine andere Stelle probiert.
        /// </summary>
        private void AbzweigeBauen(System.Random zufall)
        {
            int n = m_Mitte.Count;
            if (w.abzweigungen <= 0) return;

            int laenge = Mathf.Min(Mathf.RoundToInt(w.abzweigLaenge / m_Schritt), n / 4);
            if (laenge < 10) return;
            var vergeben = new bool[n];

            for (int nummer = 0; nummer < w.abzweigungen; nummer++)
            {
                for (int versuch = 0; versuch < 16; versuch++)
                {
                    int start = zufall.Next(n);
                    int seite = zufall.Next(2) == 0 ? -1 : 1;
                    float huegel = Mathf.Max(0f, w.hoehenunterschied) * 0.3f * (float)zufall.NextDouble();

                    bool frei = true;
                    for (int d = -4; d <= laenge + 4 && frei; d++)
                    {
                        int stelle = Stelle(start + d);
                        frei = m_Belegt[stelle] < 0 && !vergeben[stelle];
                    }
                    if (!frei) continue;

                    StreckenBahn bahn = AbzweigVersuch(start, laenge, seite, huegel);
                    if (bahn == null) continue;

                    for (int d = -4; d <= laenge + 4; d++) vergeben[Stelle(start + d)] = true;
                    Abzweige.Add(bahn);
                    break;
                }
            }
        }

        private StreckenBahn AbzweigVersuch(int start, int laenge, int seite, float huegel)
        {
            const float Rampe = 0.36f;   // Anteil der Laenge, ueber den sich die Abzweigung loest bzw. einmuendet
            int anzahl = laenge + 1;
            var mitte = new Vector3[anzahl];
            var breite = new float[anzahl];
            var richtung = new Vector3[anzahl];
            var versatz = new float[anzahl];

            for (int k = 0; k < anzahl; k++)
            {
                int stelle = Stelle(start + k);
                float u = k / (float)laenge;
                float gewicht = u < Rampe ? Mathf.SmoothStep(0f, 1f, u / Rampe)
                    : u > 1f - Rampe ? Mathf.SmoothStep(0f, 1f, (1f - u) / Rampe) : 1f;

                breite[k] = Mathf.Max(w.breite * 0.3f, m_Breite[stelle] * w.abzweigBreite);
                versatz[k] = (m_Breite[stelle] * 0.5f + breite[k] * 0.5f + w.wandDicke * 2f + w.inselBreite) * gewicht;
                Vector3 quer = Vector3.Cross(Vector3.up, m_Richtung[stelle]) * (seite * versatz[k]);
                float welle = Mathf.Sin(u * Mathf.PI);
                mitte[k] = new Vector3(m_Mitte[stelle].x + quer.x, m_Hoehe[stelle] + huegel * welle * welle, m_Mitte[stelle].z + quer.z);
            }

            for (int k = 0; k < anzahl; k++)
            {
                // An beiden Enden laeuft die Abzweigung genau in Richtung der Hauptrunde.
                Vector3 r = k == 0 || k == anzahl - 1 ? m_Richtung[Stelle(start + k)] : mitte[k + 1] - mitte[k - 1];
                r.y = 0f;
                richtung[k] = r.normalized;
            }

            for (int k = 0; k < anzahl - 1; k++)
            {
                Vector3 schritt = mitte[k + 1] - mitte[k];
                float steigung = Mathf.Abs(schritt.y);
                schritt.y = 0f;
                // Auf der Innenseite einer Kurve staucht sich die versetzte Linie. Zu stark: Kurve zu eng.
                if (schritt.magnitude < m_Schritt * 0.4f) return null;
                if (Vector3.Dot(schritt, m_Richtung[Stelle(start + k)]) <= 0f) return null;
                if (steigung > schritt.magnitude * w.maxSteigung * 1.4f) return null;
            }
            if (!KantenGueltig(mitte, richtung, breite, false)) return null;

            int n = m_Mitte.Count;
            for (int k = 0; k < anzahl; k++)
            {
                int stelle = Stelle(start + k);

                // Abstand zu allen anderen Teilen der Hauptrunde
                for (int j = 0; j < n; j++)
                {
                    if (Zyklisch(j, stelle) <= m_Nachbarn) continue;
                    float noetig = Noetig(breite[k], m_Breite[j]);
                    if (AbstandQuadrat(mitte[k], m_Mitte[j]) < noetig * noetig) return null;
                }

                // Die Insel zur eigenen Hauptrunde darf in Kurven nicht verschwinden.
                for (int d = -m_Nachbarn; d <= m_Nachbarn; d++)
                {
                    float mindestens = versatz[k] - w.inselBreite * 0.75f;
                    if (mindestens > 0f && AbstandQuadrat(mitte[k], m_Mitte[Stelle(stelle + d)]) < mindestens * mindestens) return null;
                }

                foreach (StreckenBahn andere in Abzweige)
                {
                    for (int j = 0; j < andere.Anzahl; j++)
                    {
                        float noetig = Noetig(breite[k], andere.breite[j]);
                        if (AbstandQuadrat(mitte[k], andere.mitte[j]) < noetig * noetig) return null;
                    }
                }
            }

            return new StreckenBahn
            {
                geschlossen = false, mitte = mitte, richtung = richtung, breite = breite,
                bruecke = new bool[anzahl], vonStelle = Stelle(start), seite = seite,
            };
        }
    }
}
