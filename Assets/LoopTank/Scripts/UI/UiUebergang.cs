using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TopDownRace
{
    /// <summary>
    /// Szenenuebergang und Ladebildschirm in einem. Zwei Flaechen wischen von rechts ueber das
    /// Bild (erst die Akzentfarbe, dicht dahinter die dunkle Ladeflaeche), bleiben waehrend des
    /// Ladens stehen und wischen danach nach links wieder hinaus.
    ///
    /// Lebt ueber Szenenwechsel hinweg und legt sich bei der ersten Benutzung selbst an:
    ///   UiUebergang.Zu("Lade Rennen ...", () => SzeneLaden());   // abdecken, dann laden
    ///   UiUebergang.Auf();                                        // aufdecken, sobald alles bereit ist
    /// </summary>
    public class UiUebergang : MonoBehaviour
    {
        private static UiUebergang s_Instanz;

        private const float WischDauer = 0.42f;
        private const float MindestAnzeige = 0.7f;   // ein Ladebildschirm, der nur aufblitzt, wirkt wie ein Fehler
        private const float Draussen = 2.2f;         // Stand, bei dem auch die nachlaufende Akzentflaeche draussen ist

        private static readonly string[] Tipps =
        {
            "Die Ghosts deiner Gegner sind tödlich. Deine eigenen im Multiplayer nicht.",
            "Leertaste halten: Drift. Damit kommst du um enge Kurven.",
            "Kanister füllen den Treibstoff für den Boost (Shift) wieder auf.",
            "Jede Runde bringt Zeit. Wer stehen bleibt, verliert.",
            "Rechte Maustaste halten: frei umsehen und zielen.",
            "Erst alle Checkpoints in Reihenfolge, dann zählt die Ziellinie.",
        };

        private Canvas m_Canvas;
        private RectTransform m_Wurzel;
        private RectTransform m_AkzentFlaeche;
        private RectTransform m_LadeFlaeche;
        private CanvasGroup m_Inhalt;
        private TextMeshProUGUI m_Meldung;
        private TextMeshProUGUI m_Tipp;
        private RectTransform m_BalkenLaeufer;
        private RectTransform m_BalkenSpur;

        // 0 = rechts ausserhalb, 1 = deckt das Bild, 2 = links ausserhalb
        private float m_Stand;
        private float m_Ziel;
        private float m_ZuSeit;
        private System.Action m_WennZu;

        public static bool IstZu => s_Instanz != null && s_Instanz.m_Stand >= 1f && s_Instanz.m_Ziel == 1f;

        private static UiUebergang Instanz
        {
            get
            {
                if (s_Instanz == null)
                {
                    var objekt = new GameObject("UiUebergang");
                    DontDestroyOnLoad(objekt);
                    s_Instanz = objekt.AddComponent<UiUebergang>();
                    s_Instanz.Aufbauen();
                }
                return s_Instanz;
            }
        }

        /// <summary>Deckt das Bild ab und zeigt den Ladebildschirm. wennZu laeuft, sobald nichts mehr zu sehen ist.</summary>
        public static void Zu(string meldung, System.Action wennZu = null)
        {
            UiUebergang u = Instanz;
            u.m_Meldung.text = meldung;

            if (u.m_Stand >= 1f && u.m_Ziel == 1f)
            {
                // Schon abgedeckt (z. B. Neustart direkt aus dem Ladebildschirm)
                wennZu?.Invoke();
                return;
            }

            if (u.m_Ziel != 1f) u.m_Tipp.text = Tipps[Random.Range(0, Tipps.Length)];
            if (u.m_Stand > 1f) u.m_Stand = 0f;   // war gerade beim Hinauswischen: von vorn beginnen
            u.m_Ziel = 1f;
            u.m_WennZu += wennZu;
            u.m_Canvas.enabled = true;
        }

        /// <summary>Gibt das Bild wieder frei. Ohne vorheriges Zu passiert nichts.</summary>
        public static void Auf()
        {
            if (s_Instanz == null || s_Instanz.m_Ziel != 1f) return;
            s_Instanz.m_Ziel = Draussen;
        }

        private void Aufbauen()
        {
            m_Canvas = UiBau.Leinwand("Canvas", 1000, transform);
            m_Wurzel = (RectTransform)m_Canvas.transform;

            // Akzentflaeche: laeuft voraus
            Image akzent = UiBau.Bild("Akzent", m_Wurzel, UiBau.Akzent, false);
            akzent.raycastTarget = true;   // waehrend des Uebergangs keine Klicks durchlassen
            m_AkzentFlaeche = akzent.rectTransform;
            UiBau.Strecken(m_AkzentFlaeche);

            // Ladeflaeche: folgt dicht dahinter und traegt den Inhalt
            Image lade = UiBau.Bild("Ladebildschirm", m_Wurzel, UiBau.Dunkel, false);
            lade.raycastTarget = true;
            m_LadeFlaeche = lade.rectTransform;
            UiBau.Strecken(m_LadeFlaeche);

            RectTransform inhalt = UiBau.Rechteck("Inhalt", m_LadeFlaeche);
            UiBau.Strecken(inhalt);
            m_Inhalt = inhalt.gameObject.AddComponent<CanvasGroup>();

            TextMeshProUGUI titel = UiBau.Text("Titel", inhalt, "LOOPTANK", 120, UiBau.Akzent, TextAlignmentOptions.Center, FontStyles.Bold);
            titel.characterSpacing = 12f;
            UiBau.Setzen(titel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 110), new Vector2(1400, 150));

            m_Meldung = UiBau.Text("Meldung", inhalt, "", 44, UiBau.Hell);
            UiBau.Setzen(m_Meldung.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(1400, 60));

            Image spur = UiBau.Bild("Balken", inhalt, new Color(1f, 1f, 1f, 0.10f));
            m_BalkenSpur = spur.rectTransform;
            UiBau.Setzen(m_BalkenSpur, new Vector2(0.5f, 0.5f), new Vector2(0, -80), new Vector2(520, 14));

            Image laeufer = UiBau.Bild("Laeufer", m_BalkenSpur, UiBau.Akzent);
            m_BalkenLaeufer = laeufer.rectTransform;
            UiBau.Setzen(m_BalkenLaeufer, new Vector2(0f, 0.5f), new Vector2(0, 0), new Vector2(140, 14));

            m_Tipp = UiBau.Text("Tipp", inhalt, "", 30, UiBau.Gedimmt);
            UiBau.Setzen(m_Tipp.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 90), new Vector2(1700, 50));

            m_Canvas.enabled = false;
        }

        private void Update()
        {
            if (m_Canvas == null || !m_Canvas.enabled) return;

            float dt = Time.unscaledDeltaTime;
            float breite = m_Wurzel.rect.width + 40f;

            // Aufdecken erst, wenn der Ladebildschirm eine Mindestzeit zu sehen war
            float ziel = m_Ziel;
            if (ziel == Draussen && Time.unscaledTime - m_ZuSeit < MindestAnzeige) ziel = 1f;

            float vorher = m_Stand;
            m_Stand = Mathf.MoveTowards(m_Stand, ziel, dt / WischDauer);

            if (vorher < 1f && m_Stand >= 1f)
            {
                m_ZuSeit = Time.unscaledTime;
                System.Action aktion = m_WennZu;
                m_WennZu = null;
                aktion?.Invoke();
            }

            // Die Akzentflaeche laeuft beim Hineinwischen voraus und beim Hinauswischen hinterher,
            // sie ist also beide Male als farbige Kante zu sehen.
            m_AkzentFlaeche.anchoredPosition = new Vector2(Lage(m_Stand <= 1f ? m_Stand + 0.16f : Mathf.Max(1f, m_Stand - 0.2f)) * breite, 0f);
            m_LadeFlaeche.anchoredPosition = new Vector2(Lage(m_Stand) * breite, 0f);

            // Inhalt erst zeigen, wenn die Flaeche steht
            float inhaltZiel = (m_Stand >= 1f && ziel == 1f) ? 1f : 0f;
            m_Inhalt.alpha = Mathf.MoveTowards(m_Inhalt.alpha, inhaltZiel, dt * 5f);

            // Laufender Balken: pendelt hin und her
            float spur = m_BalkenSpur.rect.width - m_BalkenLaeufer.rect.width;
            float pendel = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * 3.2f);
            m_BalkenLaeufer.anchoredPosition = new Vector2(spur * pendel, 0f);

            if (m_Stand >= Draussen)
            {
                // Fertig hinausgewischt: zuruecksetzen und ausblenden
                m_Stand = 0f;
                m_Ziel = 0f;
                m_Canvas.enabled = false;
            }
        }

        /// <summary>Lage einer Flaeche fuer einen Stand zwischen 0 und 2, in Bildbreiten (1 = rechts draussen, -1 = links draussen).</summary>
        private static float Lage(float stand)
        {
            stand = Mathf.Clamp(stand, 0f, 2f);
            if (stand <= 1f) return 1f - UiBau.WeichAus(stand);
            return -UiBau.WeichEin(stand - 1f);
        }
    }
}
