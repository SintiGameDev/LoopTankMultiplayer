using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TopDownRace
{
    /// <summary>
    /// Anzeige waehrend des Rennens: Zeit, Runde, Checkpoints, Tempo, Treibstoff, Countdown,
    /// Meldungen, Spielerliste (Multiplayer) und der Ergebnis-Bildschirm.
    ///
    /// Baut sich beim Start selbst auf (siehe UiBau), es muss nichts im Inspector verdrahtet
    /// werden. Die aeltere Oberflaeche aus dem Asset-Pack (ui-base mit Timer-Text, Rundentexten
    /// und win/lose-Fenstern) laeuft im Hintergrund weiter, weil Timer und GhostManager an ihr
    /// haengen, wird aber nicht mehr gezeichnet.
    /// </summary>
    public class RennHud : MonoBehaviour
    {
        public static RennHud Instanz { get; private set; }

        private CanvasGroup m_HudGruppe;
        private RectTransform m_Wurzel;

        private TextMeshProUGUI m_Zeit;
        private Image m_ZeitFlaeche;
        private TextMeshProUGUI m_Runde;
        private RectTransform m_PunkteReihe;
        private Image[] m_Punkte = new Image[0];
        private TextMeshProUGUI m_Spielerliste;
        private TextMeshProUGUI m_Diagnose;

        private TextMeshProUGUI m_Tempo;
        private RectTransform m_TempoFuellung;
        private TextMeshProUGUI m_Treibstoff;
        private RectTransform m_TreibstoffFuellung;
        private Image m_TreibstoffBild;
        private TextMeshProUGUI m_Tasten;

        private TextMeshProUGUI m_Countdown;
        private TextMeshProUGUI m_Banner;
        private TextMeshProUGUI m_Toast;
        private float m_ToastBis;

        private CanvasGroup m_ErgebnisGruppe;
        private RectTransform m_ErgebnisKarte;

        private readonly StringBuilder m_Text = new StringBuilder();
        private bool m_DiagnoseAn;
        private float m_NaechsteListe;
        private float m_AltesUiPruefenBis;

        // Zuletzt angezeigte Werte: Texte werden nur neu gesetzt, wenn sich etwas aendert.
        private int m_ZeitSekunden = -1, m_RundeGezeigt = -1, m_TempoGezeigt = -1, m_TreibstoffGezeigt = -1;
        private int m_CountdownGezeigt = -1, m_PunkteGezeigt = -1, m_PunkteVoll = -1;
        private bool m_LosGezeigt;
        private float m_CountdownPuls, m_TreibstoffPuls, m_RundePuls;
        private float m_LetzterTreibstoff = -1f;
        private float m_RundenStart;

        // ------------------------------------------------------------------
        // Aufbau
        // ------------------------------------------------------------------

        private void Awake()
        {
            Instanz = this;
            UiBau.EventSystemSicherstellen();

            Canvas canvas = UiBau.Leinwand("RennHud-Canvas", 50, transform);
            m_Wurzel = (RectTransform)canvas.transform;

            RectTransform hud = UiBau.Rechteck("Hud", m_Wurzel);
            UiBau.Strecken(hud);
            m_HudGruppe = hud.gameObject.AddComponent<CanvasGroup>();
            m_HudGruppe.alpha = 0f;
            m_HudGruppe.blocksRaycasts = false;

            // --- Zeit, oben Mitte ---
            m_ZeitFlaeche = UiBau.Bild("Zeit", hud, UiBau.Flaeche);
            UiBau.Setzen(m_ZeitFlaeche.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -28), new Vector2(250, 84));
            m_Zeit = UiBau.Text("Wert", m_ZeitFlaeche.transform, "0:00", 58, UiBau.Hell, TextAlignmentOptions.Center, FontStyles.Bold);
            UiBau.Strecken(m_Zeit.rectTransform);

            // --- Runde und Checkpoints, oben links ---
            Image rundenFlaeche = UiBau.Bild("Runde", hud, UiBau.Flaeche);
            UiBau.Setzen(rundenFlaeche.rectTransform, new Vector2(0f, 1f), new Vector2(28, -28), new Vector2(400, 116));
            TextMeshProUGUI rundenTitel = UiBau.Text("Titel", rundenFlaeche.transform, "RUNDE", 22, UiBau.Gedimmt, TextAlignmentOptions.Left);
            UiBau.Setzen(rundenTitel.rectTransform, new Vector2(0f, 1f), new Vector2(24, -12), new Vector2(200, 30));
            m_Runde = UiBau.Text("Wert", rundenFlaeche.transform, "0", 56, UiBau.Hell, TextAlignmentOptions.Left, FontStyles.Bold);
            UiBau.Setzen(m_Runde.rectTransform, new Vector2(0f, 1f), new Vector2(24, -36), new Vector2(160, 64));
            m_PunkteReihe = UiBau.Rechteck("Checkpoints", rundenFlaeche.transform);
            UiBau.Setzen(m_PunkteReihe, new Vector2(0f, 0f), new Vector2(120, 22), new Vector2(260, 16));

            m_Spielerliste = UiBau.Text("Spielerliste", hud, "", 28, UiBau.Hell, TextAlignmentOptions.TopLeft);
            UiBau.Setzen(m_Spielerliste.rectTransform, new Vector2(0f, 1f), new Vector2(34, -160), new Vector2(620, 220));

            m_Diagnose = UiBau.Text("Diagnose", hud, "", 22, new Color(1f, 0.82f, 0.5f), TextAlignmentOptions.TopLeft);
            UiBau.Setzen(m_Diagnose.rectTransform, new Vector2(0f, 1f), new Vector2(34, -330), new Vector2(900, 40));

            // --- Tempo, unten links ---
            Image tempoFlaeche = UiBau.Bild("Tempo", hud, UiBau.Flaeche);
            UiBau.Setzen(tempoFlaeche.rectTransform, new Vector2(0f, 0f), new Vector2(28, 28), new Vector2(300, 124));
            TextMeshProUGUI tempoTitel = UiBau.Text("Titel", tempoFlaeche.transform, "TEMPO", 22, UiBau.Gedimmt, TextAlignmentOptions.Left);
            UiBau.Setzen(tempoTitel.rectTransform, new Vector2(0f, 1f), new Vector2(24, -12), new Vector2(200, 30));
            m_Tempo = UiBau.Text("Wert", tempoFlaeche.transform, "0", 62, UiBau.Hell, TextAlignmentOptions.Left, FontStyles.Bold);
            UiBau.Setzen(m_Tempo.rectTransform, new Vector2(0f, 1f), new Vector2(24, -32), new Vector2(240, 66));
            m_TempoFuellung = Balken(tempoFlaeche.transform, new Vector2(24, 16), new Vector2(252, 10), UiBau.Akzent, out _);

            // --- Treibstoff, unten rechts ---
            Image tankFlaeche = UiBau.Bild("Treibstoff", hud, UiBau.Flaeche);
            UiBau.Setzen(tankFlaeche.rectTransform, new Vector2(1f, 0f), new Vector2(-28, 28), new Vector2(380, 124));
            TextMeshProUGUI tankTitel = UiBau.Text("Titel", tankFlaeche.transform, "TREIBSTOFF", 22, UiBau.Gedimmt, TextAlignmentOptions.Left);
            UiBau.Setzen(tankTitel.rectTransform, new Vector2(0f, 1f), new Vector2(24, -12), new Vector2(220, 30));
            m_Treibstoff = UiBau.Text("Wert", tankFlaeche.transform, "100", 40, UiBau.Hell, TextAlignmentOptions.Right, FontStyles.Bold);
            UiBau.Setzen(m_Treibstoff.rectTransform, new Vector2(1f, 1f), new Vector2(-24, -8), new Vector2(140, 46));
            m_TreibstoffFuellung = Balken(tankFlaeche.transform, new Vector2(24, 46), new Vector2(332, 18), UiBau.Akzent, out m_TreibstoffBild);
            m_Tasten = UiBau.Text("Tasten", tankFlaeche.transform, "", 21, UiBau.Gedimmt, TextAlignmentOptions.Left);
            UiBau.Setzen(m_Tasten.rectTransform, new Vector2(0f, 0f), new Vector2(24, 12), new Vector2(340, 28));

            // --- Mitte: Countdown, Banner, kurze Meldungen ---
            m_Countdown = UiBau.Text("Countdown", hud, "", 300, UiBau.Hell, TextAlignmentOptions.Center, FontStyles.Bold);
            UiBau.Setzen(m_Countdown.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(900, 340));
            Umranden(m_Countdown);

            m_Banner = UiBau.Text("Banner", m_Wurzel, "", 40, UiBau.Hell, TextAlignmentOptions.Center, FontStyles.Bold);
            UiBau.Setzen(m_Banner.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -130), new Vector2(1500, 60));
            Umranden(m_Banner);

            m_Toast = UiBau.Text("Meldung", hud, "", 38, UiBau.Akzent, TextAlignmentOptions.Center, FontStyles.Bold);
            UiBau.Setzen(m_Toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 210), new Vector2(1200, 56));
            Umranden(m_Toast);
            m_Toast.alpha = 0f;

            ErgebnisBauen();
            m_AltesUiPruefenBis = Time.unscaledTime + 4f;
        }

        private void OnDestroy()
        {
            if (Instanz == this) Instanz = null;
        }

        private static void Umranden(TextMeshProUGUI text)
        {
            text.outlineWidth = 0.22f;
            text.outlineColor = new Color32(0, 0, 0, 220);
        }

        /// <summary>Balken aus Spur und Fuellung. Die Fuellung wird ueber ihren rechten Anker verkuerzt.</summary>
        private static RectTransform Balken(Transform eltern, Vector2 position, Vector2 groesse, Color farbe, out Image fuellBild)
        {
            Image spur = UiBau.Bild("Balken", eltern, new Color(1f, 1f, 1f, 0.12f));
            UiBau.Setzen(spur.rectTransform, new Vector2(0f, 0f), position, groesse);

            fuellBild = UiBau.Bild("Fuellung", spur.transform, farbe);
            RectTransform fuellung = fuellBild.rectTransform;
            fuellung.anchorMin = Vector2.zero;
            fuellung.anchorMax = Vector2.one;
            fuellung.offsetMin = Vector2.zero;
            fuellung.offsetMax = Vector2.zero;
            return fuellung;
        }

        private static void Fuellen(RectTransform fuellung, float anteil)
        {
            fuellung.anchorMax = new Vector2(Mathf.Clamp01(anteil), 1f);
        }

        private void ErgebnisBauen()
        {
            RectTransform ergebnis = UiBau.Rechteck("Ergebnis", m_Wurzel);
            UiBau.Strecken(ergebnis);
            m_ErgebnisGruppe = ergebnis.gameObject.AddComponent<CanvasGroup>();
            m_ErgebnisGruppe.alpha = 0f;
            ergebnis.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Von aussen
        // ------------------------------------------------------------------

        /// <summary>Dauerhafte Zeile oben in der Mitte, z. B. "Du bist raus und schaust zu". Leerer Text blendet sie aus.</summary>
        public void BannerZeigen(string text)
        {
            if (m_Banner != null) m_Banner.text = text;
        }

        /// <summary>Kurze Meldung, die von selbst wieder verschwindet, z. B. "+35 Treibstoff".</summary>
        public void Meldung(string text)
        {
            if (m_Toast == null) return;
            m_Toast.text = text;
            m_ToastBis = Time.unscaledTime + 1.8f;
        }

        /// <summary>Ergebnis-Bildschirm am Rennende.</summary>
        public void ErgebnisZeigen(bool gewonnen, string titel, int runden)
        {
            if (m_ErgebnisGruppe.gameObject.activeSelf) return;

            Transform ergebnis = m_ErgebnisGruppe.transform;
            Image schleier = UiBau.Bild("Schleier", ergebnis, new Color(0.01f, 0.03f, 0.07f, 0.78f), false);
            schleier.raycastTarget = true;
            UiBau.Strecken(schleier.rectTransform);

            Image karte = UiBau.Bild("Karte", ergebnis, UiBau.Flaeche);
            m_ErgebnisKarte = karte.rectTransform;
            UiBau.Setzen(m_ErgebnisKarte, new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(820, 470));

            Image kante = UiBau.Bild("Kante", karte.transform, gewonnen ? UiBau.Gut : UiBau.Warnung);
            UiBau.Setzen(kante.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -22), new Vector2(120, 8));

            TextMeshProUGUI titelText = UiBau.Text("Titel", karte.transform, titel, 74, gewonnen ? UiBau.Gut : UiBau.Hell, TextAlignmentOptions.Center, FontStyles.Bold);
            titelText.enableAutoSizing = true;
            titelText.fontSizeMin = 36;
            titelText.fontSizeMax = 74;
            UiBau.Setzen(titelText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -56), new Vector2(740, 110));

            TextMeshProUGUI rundenText = UiBau.Text("Runden", karte.transform, runden + (runden == 1 ? " Runde" : " Runden"), 46, UiBau.Gedimmt);
            UiBau.Setzen(rundenText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, -180), new Vector2(740, 60));

            var leitung = GameControl.m_Current;
            bool istHost = leitung != null && leitung.IsServer;
            if (istHost)
            {
                UiKnopf nochmal = UiBau.Knopf("Nochmal", karte.transform, "Nochmal", new Vector2(340, 86), true);
                UiBau.Setzen((RectTransform)nochmal.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-185, 90), new Vector2(340, 86));
                nochmal.BeiKlick(() =>
                {
                    nochmal.Aktiv = false;
                    if (GameControl.m_Current != null) GameControl.m_Current.NeustartAnfordern();
                });
            }
            else
            {
                TextMeshProUGUI warten = UiBau.Text("Warten", karte.transform, "Der Host startet die nächste Runde.", 28, UiBau.Gedimmt);
                UiBau.Setzen(warten.rectTransform, new Vector2(0.5f, 0f), new Vector2(0, 150), new Vector2(740, 40));
            }

            UiKnopf menue = UiBau.Knopf("Menue", karte.transform, "Zum Menü", new Vector2(340, 86), !istHost);
            UiBau.Setzen((RectTransform)menue.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(istHost ? 185 : 0, 90), new Vector2(340, 86));
            menue.BeiKlick(() =>
            {
                menue.Aktiv = false;
                NetzwerkSitzung.Verlassen();
            });

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            m_ErgebnisGruppe.gameObject.SetActive(true);
            StartCoroutine(ErgebnisEinblenden());
        }

        private IEnumerator ErgebnisEinblenden()
        {
            // Das HUD tritt zurueck, die Karte gleitet von unten herein.
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.35f;
                float k = UiBau.WeichAus(t);
                m_ErgebnisGruppe.alpha = k;
                m_ErgebnisKarte.anchoredPosition = new Vector2(0, Mathf.Lerp(-90f, 0f, k));
                yield return null;
            }
        }

        // ------------------------------------------------------------------
        // Laufend
        // ------------------------------------------------------------------

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F3)) m_DiagnoseAn = !m_DiagnoseAn;
            float dt = Time.unscaledDeltaTime;

            if (Time.unscaledTime < m_AltesUiPruefenBis) AlteOberflaecheAusblenden();

            // Kurze Meldung ausblenden
            if (m_Toast.text.Length > 0)
            {
                float rest = m_ToastBis - Time.unscaledTime;
                m_Toast.alpha = Mathf.Clamp01(rest / 0.4f);
                m_Toast.rectTransform.anchoredPosition = new Vector2(0, 210 + (1.8f - Mathf.Max(0f, rest)) * 26f);
                if (rest <= 0f) m_Toast.text = "";
            }

            var leitung = GameControl.m_Current;
            bool bereit = leitung != null && leitung.IsSpawned && leitung.Phase.Value != RennPhase.Warten;
            bool ergebnis = m_ErgebnisGruppe.gameObject.activeSelf;
            m_HudGruppe.alpha = Mathf.MoveTowards(m_HudGruppe.alpha, bereit && !ergebnis ? 1f : (ergebnis ? 0.25f : 0f), dt * 3f);
            if (!bereit) return;

            CountdownAktualisieren(leitung, dt);
            ZeitAktualisieren(leitung, dt);
            RundeAktualisieren(leitung, dt);
            FahrzeugAktualisieren(dt);

            if (Time.unscaledTime >= m_NaechsteListe)
            {
                m_NaechsteListe = Time.unscaledTime + (m_DiagnoseAn ? 0.1f : 0.25f);
                ListeAktualisieren(leitung);
            }
        }

        /// <summary>
        /// Die alte Oberflaeche (ui-base) wird nicht mehr gezeichnet, ihre Scripts laufen aber weiter.
        /// Deshalb werden nur Canvas und UI-Kamera abgeschaltet, nicht die Objekte.
        /// </summary>
        private void AlteOberflaecheAusblenden()
        {
            if (UISystem.m_Main == null) return;
            foreach (Canvas canvas in UISystem.m_Main.GetComponentsInChildren<Canvas>(true))
                if (canvas.enabled) canvas.enabled = false;
            foreach (Camera kamera in UISystem.m_Main.GetComponentsInChildren<Camera>(true))
                if (kamera.enabled) kamera.enabled = false;
        }

        private void CountdownAktualisieren(GameControl leitung, float dt)
        {
            m_CountdownPuls = Mathf.MoveTowards(m_CountdownPuls, 0f, dt * 2.2f);

            if (!leitung.m_StartRace)
            {
                int zahl = leitung.m_StartTimer;
                if (zahl != m_CountdownGezeigt)
                {
                    m_CountdownGezeigt = zahl;
                    m_Countdown.text = zahl > 0 ? zahl.ToString() : "";
                    m_Countdown.color = UiBau.Hell;
                    m_CountdownPuls = 1f;
                }
            }
            else if (!m_LosGezeigt)
            {
                m_LosGezeigt = true;
                m_Countdown.text = "LOS!";
                m_Countdown.color = UiBau.Gut;
                m_CountdownPuls = 1f;
                m_RundenStart = Time.time;
            }

            if (m_Countdown.text.Length == 0) return;

            // Jede neue Zahl springt gross herein und setzt sich; "LOS!" verblasst danach.
            float puls = UiBau.WeichAus(1f - m_CountdownPuls);
            m_Countdown.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.5f, 1f, puls);
            if (m_LosGezeigt)
            {
                m_Countdown.alpha = Mathf.Clamp01(m_CountdownPuls * 2.5f);
                if (m_CountdownPuls <= 0f) m_Countdown.text = "";
            }
            else
            {
                m_Countdown.alpha = Mathf.Lerp(0.35f, 1f, puls);
            }
        }

        private void ZeitAktualisieren(GameControl leitung, float dt)
        {
            Timer timer = leitung.RundenTimer;
            if (timer == null) return;

            float rest = Mathf.Max(0f, timer.Restzeit);
            int sekunden = Mathf.CeilToInt(rest);
            if (sekunden != m_ZeitSekunden)
            {
                m_ZeitSekunden = sekunden;
                m_Zeit.text = (sekunden / 60) + ":" + (sekunden % 60).ToString("00");
            }

            // Die letzten zehn Sekunden pulsieren rot.
            bool knapp = timer.Laeuft && rest <= 10f;
            float puls = knapp ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) : 0f;
            m_Zeit.color = Color.Lerp(UiBau.Hell, UiBau.Warnung, knapp ? 0.6f + 0.4f * puls : 0f);
            m_ZeitFlaeche.rectTransform.localScale = Vector3.one * (1f + 0.05f * puls);
        }

        private void RundeAktualisieren(GameControl leitung, float dt)
        {
            m_RundePuls = Mathf.MoveTowards(m_RundePuls, 0f, dt * 2.5f);

            int runde = leitung.m_FinishedLaps;
            if (runde != m_RundeGezeigt)
            {
                if (m_RundeGezeigt >= 0 && runde > m_RundeGezeigt)
                {
                    m_RundePuls = 1f;
                    // Ab der zweiten Zielueberquerung gibt es eine gefahrene Rundenzeit.
                    if (runde > 1) Meldung("Runde " + runde + "   " + ZeitText(Time.time - m_RundenStart));
                    m_RundenStart = Time.time;
                }
                m_RundeGezeigt = runde;
                m_Runde.text = runde.ToString();
            }
            m_Runde.rectTransform.localScale = Vector3.one * (1f + 0.25f * m_RundePuls);
            m_Runde.color = Color.Lerp(UiBau.Hell, UiBau.Akzent, m_RundePuls);

            // Checkpoints als Reihe kleiner Marken: gefuellt = durchfahren
            var strecke = RaceTrackControl.m_Main;
            var eigener = PlayerCar.m_Current;
            if (strecke == null || eigener == null) return;

            int noetig = strecke.BenoetigteCheckpoints;
            if (noetig != m_Punkte.Length) PunkteBauen(noetig);

            int voll = eigener.VorErsterUeberquerung ? 0 : Mathf.Min(eigener.ZwischenIndex, noetig);
            if (voll != m_PunkteVoll || noetig != m_PunkteGezeigt)
            {
                m_PunkteVoll = voll;
                m_PunkteGezeigt = noetig;
                bool alle = noetig > 0 && voll >= noetig;
                for (int i = 0; i < m_Punkte.Length; i++)
                    m_Punkte[i].color = i < voll ? (alle ? UiBau.Gut : UiBau.Akzent) : new Color(1f, 1f, 1f, 0.16f);
            }
        }

        private void PunkteBauen(int anzahl)
        {
            foreach (Image alt in m_Punkte) if (alt != null) Destroy(alt.gameObject);

            m_Punkte = new Image[anzahl];
            if (anzahl == 0) return;

            float breite = Mathf.Min(30f, (260f - (anzahl - 1) * 6f) / anzahl);
            for (int i = 0; i < anzahl; i++)
            {
                m_Punkte[i] = UiBau.Bild("Marke", m_PunkteReihe, new Color(1f, 1f, 1f, 0.16f));
                UiBau.Setzen(m_Punkte[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(i * (breite + 6f), 0), new Vector2(breite, 12));
            }
            m_PunkteVoll = -1;
        }

        private void FahrzeugAktualisieren(float dt)
        {
            var eigener = PlayerCar.m_Current;
            FahrPhysik physik = eigener != null ? eigener.GetComponent<FahrPhysik>() : null;

            // Tempo
            float tempo = physik != null ? physik.Tempo : 0f;
            var physik3D = physik as CarPhysics3D;
            float hoechst = physik3D != null ? physik3D.m_Hoechsttempo : 80f;
            int tempoGanz = Mathf.RoundToInt(tempo);
            if (tempoGanz != m_TempoGezeigt)
            {
                m_TempoGezeigt = tempoGanz;
                m_Tempo.text = tempoGanz.ToString();
            }
            Fuellen(m_TempoFuellung, tempo / Mathf.Max(1f, hoechst * (physik3D != null ? physik3D.m_BoostFaktor : 1f)));
            bool boost = physik3D != null && physik3D.BoostAktiv && tempo > hoechst * 0.5f;
            m_Tempo.color = Color.Lerp(m_Tempo.color, boost ? UiBau.Akzent : UiBau.Hell, 1f - Mathf.Exp(-10f * dt));

            // Treibstoff
            FuelMechanic tank = FuelMechanic.Instance;
            if (tank != null)
            {
                float stand = tank.CurrentFuel;
                float anteil = stand / Mathf.Max(1f, tank.MaxFuel);
                int ganz = Mathf.RoundToInt(stand);
                if (ganz != m_TreibstoffGezeigt)
                {
                    m_TreibstoffGezeigt = ganz;
                    m_Treibstoff.text = ganz.ToString();
                }
                if (m_LetzterTreibstoff >= 0f && stand > m_LetzterTreibstoff + 1f) m_TreibstoffPuls = 1f;   // aufgetankt
                m_LetzterTreibstoff = stand;
                m_TreibstoffPuls = Mathf.MoveTowards(m_TreibstoffPuls, 0f, dt * 2f);

                Fuellen(m_TreibstoffFuellung, anteil);
                Color grund = anteil < 0.2f ? UiBau.Warnung : UiBau.Akzent;
                m_TreibstoffBild.color = Color.Lerp(grund, UiBau.Gut, m_TreibstoffPuls);
                m_Treibstoff.rectTransform.localScale = Vector3.one * (1f + 0.2f * m_TreibstoffPuls);
            }

            string tasten = physik != null && physik.KannDriften ? "Shift  Boost      Leertaste  Drift" : "Leertaste  Boost";
            if (m_Tasten.text != tasten) m_Tasten.text = tasten;
        }

        private void ListeAktualisieren(GameControl leitung)
        {
            // Diagnose (F3): zeigt, was die Fahrphysik gerade tut.
            var eigener = PlayerCar.m_Current;
            var physik = eigener != null ? eigener.GetComponent<CarPhysics3D>() : null;
            if (m_DiagnoseAn)
            {
                m_Text.Clear();
                if (physik != null) m_Text.Append("Tempo ").Append(Mathf.RoundToInt(physik.Tempo)).Append(" / ").Append(Mathf.RoundToInt(physik.m_Hoechsttempo))
                    .Append("   Gas ").Append(physik.m_Gas.ToString("0.0"))
                    .Append("   Drift ").Append(Mathf.RoundToInt(physik.DriftAnteil * 100f)).Append(" %")
                    .Append("   Boost ").Append(physik.BoostAktiv ? "an" : "aus")
                    .Append("   Kontakt ").Append(physik.HatKontakt ? "JA" : "nein");

                // Netzwerk: fuer jeden Panzer, wem er gehoert, wo er steht und wo sein Startplatz ist.
                var netz = Unity.Netcode.NetworkManager.Singleton;
                if (netz != null)
                    m_Text.Append("\n").Append(netz.IsHost ? "Host" : "Client").Append("  eigene ID ").Append(netz.LocalClientId)
                        .Append("  Phase ").Append(leitung.Phase.Value).Append("  Panzer ").Append(PlayerCar.Alle.Count);
                foreach (var panzer in PlayerCar.Alle)
                {
                    if (panzer == null) continue;
                    var koerper = panzer.GetComponent<Rigidbody>();
                    Vector3 p = panzer.transform.position, s = panzer.StartPosition.Value;
                    m_Text.Append("\n").Append(panzer.AnzeigeName).Append("  Besitzer ").Append(panzer.OwnerClientId)
                        .Append(panzer.IsOwner ? " (ich)" : "")
                        .Append("  Platz ").Append(panzer.SpielerSlot.Value + 1)
                        .Append("  steht (").Append(Mathf.RoundToInt(p.x)).Append(", ").Append(Mathf.RoundToInt(p.z)).Append(")")
                        .Append("  Start (").Append(Mathf.RoundToInt(s.x)).Append(", ").Append(Mathf.RoundToInt(s.z)).Append(")")
                        .Append(panzer.StartGesetzt.Value ? "" : " NICHT GESETZT")
                        .Append(koerper != null ? (koerper.isKinematic ? "  kinematisch" : "  dynamisch") : "");
                }
                m_Diagnose.text = m_Text.ToString();
            }
            else if (m_Diagnose.text.Length > 0)
            {
                m_Diagnose.text = "";
            }

            // Im Einzelspieler gibt es niemanden aufzulisten.
            if (leitung.IstEinzelspieler)
            {
                if (m_Spielerliste.text.Length > 0) m_Spielerliste.text = "";
                return;
            }

            m_Text.Clear();
            for (int slot = 0; slot < NetzwerkSitzung.MaxSpieler; slot++)
            {
                foreach (var panzer in PlayerCar.Alle)
                {
                    if (panzer == null || !panzer.IsSpawned || panzer.SpielerSlot.Value != slot) continue;

                    string name = panzer.AnzeigeName + (panzer.IsOwner ? " (du)" : "");
                    string zeile = name + ":  " + panzer.Runden.Value + " Runden";
                    if (panzer.Lebt.Value)
                        m_Text.Append("<color=#").Append(SpielerFarben.Hex(panzer.FarbIndex.Value)).Append(">").Append(zeile).Append("</color>\n");
                    else
                        m_Text.Append("<color=#777777><s>").Append(zeile).Append("</s>  raus</color>\n");
                }
            }
            m_Spielerliste.text = m_Text.ToString();
        }

        private static string ZeitText(float sekunden)
        {
            sekunden = Mathf.Max(0f, sekunden);
            int minuten = (int)(sekunden / 60f);
            return minuten + ":" + (sekunden - minuten * 60).ToString("00.0");
        }
    }
}
