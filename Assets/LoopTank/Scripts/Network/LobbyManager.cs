using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using TopDownRace;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Hauptmenue und Lobby. Einzelspieler startet sofort als lokaler Host; Host/Join laufen ueber
/// Unity Relay mit Join-Code. Das Rennen startet der Host, die Szene wird dann fuer alle geladen.
///
/// Die Oberflaeche baut sich beim Start selbst auf (siehe UiBau). In der Szene muss nur dieses
/// Script auf einem Objekt liegen, dazu der NetworkManager.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    /// <summary>Aktueller Relay-Code, auch fuer die Debug-Anzeige.</summary>
    public static string AktuellerJoinCode { get; private set; } = "";

    private NetworkManager m_Netz;
    private bool m_Beschaeftigt;

    // --- Oberflaeche ---
    private RectTransform m_Wurzel;
    private RectTransform m_StartSeite, m_LobbySeite;
    private CanvasGroup m_StartGruppe, m_LobbyGruppe;
    private bool m_LobbyOffen;
    private Coroutine m_SeitenWechsel;

    private UiKnopf m_KnopfEinzel, m_KnopfHost, m_KnopfJoin, m_KnopfAnsicht, m_KnopfBeenden;
    private UiKnopf m_KnopfStart, m_KnopfVerlassen, m_KnopfKopieren;
    private TMP_InputField m_CodeEingabe;
    private TextMeshProUGUI m_CodeAnzeige, m_Status, m_Fehler, m_LobbyHinweis;
    private TextMeshProUGUI[] m_SlotNamen, m_SlotMarken;
    private Image[] m_SlotPunkte;
    private TMP_InputField m_NamenEingabe;
    private Image[] m_FarbFelder, m_FarbRinge;
    private Button[] m_FarbKnoepfe;
    private readonly List<ulong> m_Teilnehmer = new List<ulong>();
    private TextMeshProUGUI m_SitzungsInfo;
    private float m_NaechsteProfilPruefung;
    private RectTransform[] m_Streifen;
    private float[] m_StreifenTempo;
    private RectTransform m_TitelBalken;
    private string m_StatusBasis = "";
    private float m_KopiertBis;

    private static readonly Vector2 SeitenGroesse = new Vector2(640, 620);
    private static readonly Vector2 SeitenPlatz = new Vector2(120, 190);   // linke obere Ecke der Seiten, vom linken Rand und der Bildmitte aus

    // ------------------------------------------------------------------
    // Start
    // ------------------------------------------------------------------

    private void Awake()
    {
        // Die fruehere, in der Szene gespeicherte Oberflaeche wird nicht mehr gebraucht.
        GameObject alt = GameObject.Find("Menue");
        if (alt != null && alt.GetComponent<Canvas>() != null) Destroy(alt);

        UiBau.EventSystemSicherstellen();
        OberflaecheBauen();
    }

    private void Start()
    {
        m_Netz = NetworkManager.Singleton;
        if (m_Netz == null)
        {
            Debug.LogError("[LobbyManager] Kein NetworkManager in der Szene.");
            FehlerZeigen("Kein NetworkManager in der Szene. Bitte 'LoopTank > Multiplayer einrichten' ausführen.");
            return;
        }

        // Falls man aus einem Rennen zurueckkommt und noch eine Sitzung offen ist.
        if (m_Netz.IsListening) m_Netz.Shutdown();
        AktuellerJoinCode = "";
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        m_Netz.OnClientConnectedCallback += OnClientVerbunden;
        m_Netz.OnClientDisconnectCallback += OnClientGetrennt;

        AnsichtAktualisieren();
        StatusSetzen("");

        if (!string.IsNullOrEmpty(NetzwerkSitzung.LetzteMeldung))
        {
            FehlerZeigen(NetzwerkSitzung.LetzteMeldung);
            NetzwerkSitzung.LetzteMeldung = "";
        }

        // Kommt man aus einem Rennen, liegt noch der Ladebildschirm ueber dem Bild.
        UiUebergang.Auf();
        StartCoroutine(Einblenden());
    }

    private void OnDestroy()
    {
        if (m_Netz != null)
        {
            m_Netz.OnClientConnectedCallback -= OnClientVerbunden;
            m_Netz.OnClientDisconnectCallback -= OnClientGetrennt;
        }
    }

    // ------------------------------------------------------------------
    // Oberflaeche aufbauen
    // ------------------------------------------------------------------

    private void OberflaecheBauen()
    {
        Canvas canvas = UiBau.Leinwand("MenueUI", 0);
        m_Wurzel = (RectTransform)canvas.transform;

        Image hintergrund = UiBau.Bild("Hintergrund", m_Wurzel, UiBau.Dunkel, false);
        UiBau.Strecken(hintergrund.rectTransform);

        // Schraege Lichtstreifen, die langsam durchs Bild ziehen: das Menue steht nie ganz still.
        const int Anzahl = 6;
        m_Streifen = new RectTransform[Anzahl];
        m_StreifenTempo = new float[Anzahl];
        for (int i = 0; i < Anzahl; i++)
        {
            Image streifen = UiBau.Bild("Streifen", m_Wurzel, new Color(UiBau.Akzent.r, UiBau.Akzent.g, UiBau.Akzent.b, 0.025f + 0.012f * (i % 3)), false);
            m_Streifen[i] = streifen.rectTransform;
            UiBau.Setzen(m_Streifen[i], new Vector2(0.5f, 0.5f), new Vector2(-1400 + i * 520, 0), new Vector2(120 + 70 * (i % 3), 2200));
            m_Streifen[i].localRotation = Quaternion.Euler(0, 0, -24f);
            m_StreifenTempo[i] = 22f + 9f * (i % 4);
        }

        // Titel links oben
        TextMeshProUGUI titel = UiBau.Text("Titel", m_Wurzel, "LOOPTANK", 150, UiBau.Hell, TextAlignmentOptions.Left, FontStyles.Bold);
        titel.characterSpacing = 6f;
        UiBau.Setzen(titel.rectTransform, new Vector2(0f, 1f), new Vector2(120, -90), new Vector2(1100, 170));

        Image balken = UiBau.Bild("TitelBalken", m_Wurzel, UiBau.Akzent);
        m_TitelBalken = balken.rectTransform;
        UiBau.Setzen(m_TitelBalken, new Vector2(0f, 1f), new Vector2(126, -262), new Vector2(360, 10));

        TextMeshProUGUI unterzeile = UiBau.Text("Unterzeile", m_Wurzel, "Fahr deine Runden. Weich den Ghosts aus. Bleib als Letzter übrig.", 34, UiBau.Gedimmt, TextAlignmentOptions.Left);
        UiBau.Setzen(unterzeile.rectTransform, new Vector2(0f, 1f), new Vector2(126, -290), new Vector2(1300, 50));

        m_SitzungsInfo = UiBau.Text("SitzungsInfo", m_Wurzel, "", 20, new Color(0.40f, 0.47f, 0.57f), TextAlignmentOptions.Right);
        UiBau.Setzen(m_SitzungsInfo.rectTransform, new Vector2(1f, 0f), new Vector2(-30, 20), new Vector2(900, 30));

        StartSeiteBauen();
        LobbySeiteBauen();

        // Meldungen unten
        m_Fehler = UiBau.Text("Fehler", m_Wurzel, "", 34, UiBau.Warnung, TextAlignmentOptions.Left);
        UiBau.Setzen(m_Fehler.rectTransform, new Vector2(0f, 0f), new Vector2(940, 200), new Vector2(940, 50));
        m_Status = UiBau.Text("Status", m_Wurzel, "", 32, UiBau.Gedimmt, TextAlignmentOptions.Left);
        UiBau.Setzen(m_Status.rectTransform, new Vector2(0f, 0f), new Vector2(940, 150), new Vector2(940, 50));

        TextMeshProUGUI steuerung = UiBau.Text("Steuerung", m_Wurzel,
            "W/A/S/D  fahren      Rechte Maustaste halten  umsehen und zielen      Leertaste  Drift      Shift  Boost", 26, new Color(0.45f, 0.52f, 0.62f), TextAlignmentOptions.Left);
        UiBau.Setzen(steuerung.rectTransform, new Vector2(0f, 0f), new Vector2(126, 44), new Vector2(1600, 40));
    }

    private RectTransform SeiteAnlegen(string name, out CanvasGroup gruppe)
    {
        RectTransform seite = UiBau.Rechteck(name, m_Wurzel);
        UiBau.Setzen(seite, new Vector2(0f, 0.5f), new Vector2(0f, 1f), SeitenPlatz, SeitenGroesse);
        gruppe = seite.gameObject.AddComponent<CanvasGroup>();
        return seite;
    }

    private void StartSeiteBauen()
    {
        m_StartSeite = SeiteAnlegen("StartSeite", out m_StartGruppe);

        float y = 0f;
        m_KnopfEinzel = Zeile(m_StartSeite, "Einzelspieler", ref y, true);
        m_KnopfHost = Zeile(m_StartSeite, "Spiel hosten", ref y, false);

        // Join-Code und Beitreten nebeneinander
        m_CodeEingabe = Feld(m_StartSeite, "JoinCodeEingabe", "Join-Code", new Vector2(0, y), new Vector2(300, 86), 6, TMP_InputField.CharacterValidation.Alphanumeric, 42);
        m_KnopfJoin = UiBau.Knopf("KnopfJoin", m_StartSeite, "Beitreten", new Vector2(324, 86), false);
        UiBau.Setzen((RectTransform)m_KnopfJoin.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(316 + 162, y - 43), new Vector2(324, 86));
        y -= 86 + 46;

        m_KnopfAnsicht = Zeile(m_StartSeite, "Ansicht: 3D", ref y, false, 70);
        m_KnopfBeenden = Zeile(m_StartSeite, "Beenden", ref y, false, 70);

        m_KnopfEinzel.BeiKlick(EinzelspielerStarten);
        m_KnopfHost.BeiKlick(HostStarten);
        m_KnopfJoin.BeiKlick(ClientVerbinden);
        m_KnopfBeenden.BeiKlick(Application.Quit);
        m_KnopfAnsicht.BeiKlick(AnsichtWechseln);
        m_CodeEingabe.onSubmit.AddListener(_ => ClientVerbinden());
    }

    private void LobbySeiteBauen()
    {
        m_LobbySeite = SeiteAnlegen("LobbySeite", out m_LobbyGruppe);
        m_LobbySeite.sizeDelta = new Vector2(760, 630);

        // Karte mit dem Join-Code
        Image karte = UiBau.Bild("CodeKarte", m_LobbySeite, UiBau.Flaeche);
        UiBau.Setzen(karte.rectTransform, new Vector2(0f, 1f), new Vector2(0, 0), new Vector2(760, 150));
        TextMeshProUGUI ueber = UiBau.Text("Ueberschrift", karte.transform, "JOIN-CODE", 24, UiBau.Gedimmt, TextAlignmentOptions.Left);
        UiBau.Setzen(ueber.rectTransform, new Vector2(0f, 1f), new Vector2(30, -18), new Vector2(400, 34));
        m_CodeAnzeige = UiBau.Text("Code", karte.transform, "------", 84, UiBau.Akzent, TextAlignmentOptions.Left, FontStyles.Bold);
        m_CodeAnzeige.characterSpacing = 14f;
        UiBau.Setzen(m_CodeAnzeige.rectTransform, new Vector2(0f, 0f), new Vector2(30, 14), new Vector2(480, 96));
        m_KnopfKopieren = UiBau.Knopf("KnopfKopieren", karte.transform, "Kopieren", new Vector2(190, 64), false);
        UiBau.Setzen((RectTransform)m_KnopfKopieren.transform, new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-125, 0), new Vector2(190, 64));
        m_KnopfKopieren.BeiKlick(CodeKopieren);

        // Eigenes Profil: Name und Farbe
        Image profil = UiBau.Bild("Profil", m_LobbySeite, UiBau.Flaeche);
        UiBau.Setzen(profil.rectTransform, new Vector2(0f, 1f), new Vector2(0, -164), new Vector2(760, 96));
        TextMeshProUGUI profilTitel = UiBau.Text("Ueberschrift", profil.transform, "DEIN NAME UND DEINE FARBE", 20, UiBau.Gedimmt, TextAlignmentOptions.Left);
        UiBau.Setzen(profilTitel.rectTransform, new Vector2(0f, 1f), new Vector2(30, -8), new Vector2(500, 28));

        m_NamenEingabe = Feld(profil.transform, "NamenEingabe", "Dein Name", new Vector2(24, -38), new Vector2(300, 48), SpielerProfil.MaxNamenLaenge, TMP_InputField.CharacterValidation.None, 28);
        m_NamenEingabe.text = SpielerProfil.EigenerName;
        m_NamenEingabe.onEndEdit.AddListener(NameGeaendert);

        m_FarbFelder = new Image[SpielerFarben.Anzahl];
        m_FarbKnoepfe = new Button[SpielerFarben.Anzahl];
        m_FarbRinge = new Image[SpielerFarben.Anzahl];
        for (int i = 0; i < SpielerFarben.Anzahl; i++)
        {
            int index = i;
            Vector2 platz = new Vector2(366 + i * 48, -62);

            // Heller Ring hinter der gewaehlten Farbe
            m_FarbRinge[i] = UiBau.Bild("Ring", profil.transform, UiBau.Hell);
            UiBau.Setzen(m_FarbRinge[i].rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), platz, new Vector2(48, 48));
            m_FarbRinge[i].enabled = false;

            m_FarbFelder[i] = UiBau.Bild("Farbe " + i, profil.transform, SpielerFarben.Farbe(i));
            m_FarbFelder[i].raycastTarget = true;
            UiBau.Setzen(m_FarbFelder[i].rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), platz, new Vector2(34, 34));
            m_FarbKnoepfe[i] = m_FarbFelder[i].gameObject.AddComponent<Button>();
            m_FarbKnoepfe[i].transition = Selectable.Transition.None;
            m_FarbKnoepfe[i].onClick.AddListener(() => FarbeGewaehlt(index));
        }

        // Vier Plaetze
        m_SlotNamen = new TextMeshProUGUI[NetzwerkSitzung.MaxSpieler];
        m_SlotMarken = new TextMeshProUGUI[NetzwerkSitzung.MaxSpieler];
        m_SlotPunkte = new Image[NetzwerkSitzung.MaxSpieler];
        for (int i = 0; i < NetzwerkSitzung.MaxSpieler; i++)
        {
            Image zeile = UiBau.Bild("Platz " + (i + 1), m_LobbySeite, UiBau.Flaeche);
            UiBau.Setzen(zeile.rectTransform, new Vector2(0f, 1f), new Vector2(0, -274 - i * 56), new Vector2(760, 50));

            m_SlotPunkte[i] = UiBau.Bild("Farbe", zeile.transform, new Color(1f, 1f, 1f, 0.12f));
            UiBau.Setzen(m_SlotPunkte[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34, 0), new Vector2(24, 24));

            m_SlotNamen[i] = UiBau.Text("Name", zeile.transform, "", 28, UiBau.Hell, TextAlignmentOptions.Left);
            UiBau.Setzen(m_SlotNamen[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(66, 0), new Vector2(460, 40));

            m_SlotMarken[i] = UiBau.Text("Marke", zeile.transform, "", 24, UiBau.Akzent, TextAlignmentOptions.Right, FontStyles.Bold);
            UiBau.Setzen(m_SlotMarken[i].rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-28, 0), new Vector2(220, 40));
        }

        m_LobbyHinweis = UiBau.Text("Hinweis", m_LobbySeite, "", 26, UiBau.Gedimmt, TextAlignmentOptions.Left);
        UiBau.Setzen(m_LobbyHinweis.rectTransform, new Vector2(0f, 1f), new Vector2(4, -500), new Vector2(760, 36));

        m_KnopfStart = UiBau.Knopf("KnopfStart", m_LobbySeite, "Rennen starten", new Vector2(420, 80), true);
        UiBau.Setzen((RectTransform)m_KnopfStart.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(210, -584), new Vector2(420, 80));
        m_KnopfVerlassen = UiBau.Knopf("KnopfVerlassen", m_LobbySeite, "Verlassen", new Vector2(300, 80), false);
        UiBau.Setzen((RectTransform)m_KnopfVerlassen.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(440 + 150 + 20, -584), new Vector2(300, 80));

        m_KnopfStart.BeiKlick(RennenStarten);
        m_KnopfVerlassen.BeiKlick(LobbyVerlassen);

        m_LobbySeite.gameObject.SetActive(false);
    }

    /// <summary>Ein Knopf ueber die volle Seitenbreite; y wandert zur naechsten Zeile.</summary>
    private UiKnopf Zeile(RectTransform seite, string text, ref float y, bool primaer, float hoehe = 86f)
    {
        UiKnopf knopf = UiBau.Knopf("Knopf " + text, seite, text, new Vector2(SeitenGroesse.x, hoehe), primaer);
        UiBau.Setzen((RectTransform)knopf.transform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(SeitenGroesse.x * 0.5f, y - hoehe * 0.5f), new Vector2(SeitenGroesse.x, hoehe));
        y -= hoehe + 18f;
        return knopf;
    }

    /// <summary>Eingabefeld im Stil des Menues. Position ist die linke obere Ecke im Elternobjekt.</summary>
    private TMP_InputField Feld(Transform eltern, string name, string platzhalter, Vector2 position, Vector2 groesse,
        int zeichenLimit, TMP_InputField.CharacterValidation pruefung, float schriftGroesse)
    {
        GameObject objekt = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources());
        objekt.name = name;
        objekt.transform.SetParent(eltern, false);
        foreach (Transform t in objekt.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = eltern.gameObject.layer;
        UiBau.Setzen((RectTransform)objekt.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), position, groesse);

        var flaeche = objekt.GetComponent<Image>();
        flaeche.sprite = UiBau.RundSprite;
        flaeche.type = Image.Type.Sliced;
        flaeche.color = UiBau.Hell;

        var feld = objekt.GetComponent<TMP_InputField>();
        feld.characterLimit = zeichenLimit;
        feld.characterValidation = pruefung;
        feld.pointSize = schriftGroesse;
        feld.textComponent.alignment = TextAlignmentOptions.Center;
        feld.textComponent.color = UiBau.Dunkel;
        feld.textComponent.fontStyle = FontStyles.Bold;
        if (feld.placeholder is TextMeshProUGUI hinweis)
        {
            hinweis.text = platzhalter;
            hinweis.fontSize = schriftGroesse * 0.78f;
            hinweis.alignment = TextAlignmentOptions.Center;
            hinweis.fontStyle = FontStyles.Normal;
            hinweis.color = new Color(UiBau.Dunkel.r, UiBau.Dunkel.g, UiBau.Dunkel.b, 0.45f);
        }
        return feld;
    }

    // ------------------------------------------------------------------
    // Animation
    // ------------------------------------------------------------------

    /// <summary>Beim Betreten des Menues gleiten die Knoepfe nacheinander herein.</summary>
    private IEnumerator Einblenden()
    {
        Vector2 ziel = m_StartSeite.anchoredPosition;
        yield return UiBau.Schieben(m_StartSeite, m_StartGruppe, ziel + new Vector2(-160, 0), ziel, 0f, 1f, 0.5f, 0.1f);
    }

    /// <summary>Wischt zwischen Startseite und Lobby: die alte Seite nach links hinaus, die neue von rechts herein.</summary>
    private void SeiteZeigen(bool lobby)
    {
        if (m_LobbyOffen == lobby) return;
        m_LobbyOffen = lobby;
        if (m_SeitenWechsel != null) StopCoroutine(m_SeitenWechsel);
        m_SeitenWechsel = StartCoroutine(SeitenWechsel(lobby));
    }

    private IEnumerator SeitenWechsel(bool lobby)
    {
        RectTransform raus = lobby ? m_StartSeite : m_LobbySeite;
        RectTransform rein = lobby ? m_LobbySeite : m_StartSeite;
        CanvasGroup rausGruppe = lobby ? m_StartGruppe : m_LobbyGruppe;
        CanvasGroup reinGruppe = lobby ? m_LobbyGruppe : m_StartGruppe;
        Vector2 platz = SeitenPlatz;

        rausGruppe.interactable = false;
        rausGruppe.blocksRaycasts = false;
        rein.gameObject.SetActive(true);
        reinGruppe.interactable = false;
        reinGruppe.blocksRaycasts = false;
        reinGruppe.alpha = 0f;

        yield return UiBau.Schieben(raus, rausGruppe, raus.anchoredPosition, platz + new Vector2(-260, 0), rausGruppe.alpha, 0f, 0.22f);
        raus.gameObject.SetActive(false);

        yield return UiBau.Schieben(rein, reinGruppe, platz + new Vector2(260, 0), platz, 0f, 1f, 0.32f);
        reinGruppe.interactable = true;
        reinGruppe.blocksRaycasts = true;
        m_SeitenWechsel = null;
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Lichtstreifen ziehen nach rechts und kommen links wieder herein
        if (m_Streifen != null)
        {
            for (int i = 0; i < m_Streifen.Length; i++)
            {
                Vector2 p = m_Streifen[i].anchoredPosition;
                p.x += m_StreifenTempo[i] * dt;
                if (p.x > 1600f) p.x -= 3200f;
                m_Streifen[i].anchoredPosition = p;
            }
        }

        // Der Balken unter dem Titel atmet leicht
        if (m_TitelBalken != null)
            m_TitelBalken.sizeDelta = new Vector2(360f + 40f * Mathf.Sin(Time.unscaledTime * 1.4f), 10f);

        // Wartepunkte hinter dem Status
        if (m_Status != null && m_StatusBasis.EndsWith("..."))
        {
            int punkte = 1 + (int)(Time.unscaledTime * 2.5f) % 3;
            m_Status.text = m_StatusBasis.Substring(0, m_StatusBasis.Length - 3) + new string('.', punkte);
        }

        if (m_KnopfKopieren != null && m_KopiertBis > 0f && Time.unscaledTime > m_KopiertBis)
        {
            m_KopiertBis = 0f;
            m_KnopfKopieren.Text = "Kopieren";
        }

        if (m_LobbyOffen) LobbyAktualisieren();
    }

    private void LobbyAktualisieren()
    {
        if (m_Netz == null || !m_Netz.IsListening) return;

        // Gleiche Reihenfolge wie beim Spawnen im Rennen: Host zuerst, dann nach Beitritt.
        m_Teilnehmer.Clear();
        m_Teilnehmer.AddRange(m_Netz.ConnectedClientsIds);
        m_Teilnehmer.Sort();

        for (int i = 0; i < NetzwerkSitzung.MaxSpieler; i++)
        {
            bool belegt = i < m_Teilnehmer.Count;
            SpielerProfil.Eintrag profil = belegt ? SpielerProfil.Fuer(m_Teilnehmer[i], i) : default;
            m_SlotPunkte[i].color = belegt ? SpielerFarben.Farbe(profil.Farbe) : new Color(1f, 1f, 1f, 0.12f);
            m_SlotNamen[i].text = belegt ? profil.Name : "frei";
            m_SlotNamen[i].color = belegt ? UiBau.Hell : new Color(UiBau.Gedimmt.r, UiBau.Gedimmt.g, UiBau.Gedimmt.b, 0.5f);

            string marke = "";
            if (belegt)
            {
                if (m_Teilnehmer[i] == m_Netz.LocalClientId) marke = "DU";
                if (m_Teilnehmer[i] == NetworkManager.ServerClientId) marke = marke == "" ? "HOST" : "DU · HOST";
            }
            m_SlotMarken[i].text = marke;
        }

        // Selbstheilung: Fehlt der eigene Eintrag in der Liste des Hosts oder weicht der Name ab,
        // wird das Profil alle zwei Sekunden erneut geschickt.
        if (!m_Netz.IsServer && Time.unscaledTime >= m_NaechsteProfilPruefung)
        {
            m_NaechsteProfilPruefung = Time.unscaledTime + 2f;
            bool vorhanden = SpielerProfil.Alle.TryGetValue(m_Netz.LocalClientId, out SpielerProfil.Eintrag gemeldet);
            if (!vorhanden || (SpielerProfil.EigenerName.Length > 0 && gemeldet.Name != SpielerProfil.EigenerName) || SpielerProfil.Empfangen == 0)
                SpielerProfil.EigenesSenden();
        }

        // Diagnose unten rechts: zeigt, ob die Profil-Nachrichten in beide Richtungen ankommen.
        m_SitzungsInfo.text = (m_Netz.IsServer ? "Host" : "Client") + "  ID " + m_Netz.LocalClientId
            + "  Spieler " + m_Teilnehmer.Count + "  Profile " + SpielerProfil.Alle.Count
            + "  gesendet " + SpielerProfil.Gesendet + "  empfangen " + SpielerProfil.Empfangen;

        // Farbfelder: die eigene Farbe ist hervorgehoben, vergebene Farben sind blass und gesperrt.
        int eigene = SpielerProfil.Alle.TryGetValue(m_Netz.LocalClientId, out SpielerProfil.Eintrag ich) ? ich.Farbe : -1;
        for (int i = 0; i < m_FarbFelder.Length; i++)
        {
            bool vergeben = SpielerProfil.FarbeVergeben(i, m_Netz.LocalClientId);
            Color farbe = SpielerFarben.Farbe(i);
            farbe.a = vergeben ? 0.18f : 1f;
            m_FarbFelder[i].color = farbe;
            m_FarbKnoepfe[i].interactable = !vergeben;

            float ziel = i == eigene ? 1.3f : 1f;
            float jetzt = Mathf.Lerp(m_FarbFelder[i].rectTransform.localScale.x, ziel, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
            m_FarbFelder[i].rectTransform.localScale = new Vector3(jetzt, jetzt, 1f);
            m_FarbRinge[i].enabled = i == eigene;
        }

        if (m_Netz.IsServer)
            m_LobbyHinweis.text = m_Teilnehmer.Count < 2 ? "Gib den Code an deine Mitspieler weiter." : m_Teilnehmer.Count + " Spieler bereit.";
    }

    // ------------------------------------------------------------------
    // Eigener Name und eigene Farbe
    // ------------------------------------------------------------------

    private void NameGeaendert(string text)
    {
        SpielerProfil.EigenerName = text;
        m_NamenEingabe.SetTextWithoutNotify(SpielerProfil.EigenerName);
        SpielerProfil.EigenesSenden();
    }

    private void FarbeGewaehlt(int index)
    {
        if (m_Netz == null || SpielerProfil.FarbeVergeben(index, m_Netz.LocalClientId)) return;
        SpielerProfil.EigeneFarbe = index;
        SpielerProfil.EigenesSenden();
    }

    // ------------------------------------------------------------------
    // Einzelspieler
    // ------------------------------------------------------------------

    private void AnsichtWechseln()
    {
        NetzwerkSitzung.RennSzene = NetzwerkSitzung.RennSzene == NetzwerkSitzung.RennSzene3D
            ? NetzwerkSitzung.RennSzene2D
            : NetzwerkSitzung.RennSzene3D;
        AnsichtAktualisieren();
    }

    /// <summary>Der Knopf erscheint nur, wenn es die 3D-Szene im Build gibt. Im Multiplayer zaehlt die Wahl des Hosts.</summary>
    private void AnsichtAktualisieren()
    {
        m_KnopfAnsicht.gameObject.SetActive(NetzwerkSitzung.Hat3D);
        m_KnopfAnsicht.Text = NetzwerkSitzung.RennSzene == NetzwerkSitzung.RennSzene3D ? "Ansicht: 3D" : "Ansicht: 2D";
    }

    private void EinzelspielerStarten()
    {
        if (m_Beschaeftigt || m_Netz == null) return;
        FehlerAusblenden();
        m_Beschaeftigt = true;
        KnoepfeAktiv(false);

        // Erst abdecken, dann laden: der Wechsel verschwindet hinter dem Ladebildschirm.
        UiUebergang.Zu("Lade Rennen ...", () =>
        {
            // Lokaler Host ohne Relay und ohne Unity-Dienste, funktioniert auch offline.
            var transport = m_Netz.GetComponent<UnityTransport>();
            transport.SetConnectionData("127.0.0.1", 7777, "127.0.0.1");

            if (!m_Netz.StartHost())
            {
                m_Beschaeftigt = false;
                KnoepfeAktiv(true);
                UiUebergang.Auf();
                FehlerZeigen("Spiel konnte nicht gestartet werden.");
                return;
            }

            m_Netz.SceneManager.LoadScene(NetzwerkSitzung.RennSzene, UnityEngine.SceneManagement.LoadSceneMode.Single);
        });
    }

    // ------------------------------------------------------------------
    // Multiplayer ueber Relay
    // ------------------------------------------------------------------

    private async Task AnmeldenFallsNoetig()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized)
        {
            // Eigenes Profil pro Programmstart: so koennen mehrere Instanzen auf einem
            // Rechner (zum Testen) gleichzeitig angemeldet sein, ohne sich in die Quere zu kommen.
            var optionen = new InitializationOptions();
            optionen.SetProfile("p" + Random.Range(100000, 999999));
            await UnityServices.InitializeAsync(optionen);
        }

        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private async void HostStarten()
    {
        if (m_Beschaeftigt || m_Netz == null) return;
        m_Beschaeftigt = true;
        KnoepfeAktiv(false);
        FehlerAusblenden();

        try
        {
            StatusSetzen("Verbinde mit Unity Services ...");
            await AnmeldenFallsNoetig();

            StatusSetzen("Erstelle Sitzung ...");
            // Relay zaehlt den Host nicht mit.
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(NetzwerkSitzung.MaxSpieler - 1);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            if (this == null) return;

            var transport = m_Netz.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            if (!m_Netz.StartHost())
                throw new System.Exception("StartHost fehlgeschlagen");

            AktuellerJoinCode = joinCode;
            Debug.Log("[LobbyManager] Join Code: " + joinCode);
            LobbyZeigen();
        }
        catch (System.Exception e)
        {
            Debug.LogError("[LobbyManager] Host-Start fehlgeschlagen: " + e.Message);
            if (this == null) return;
            StartseiteZeigen();
            FehlerZeigen("Sitzung konnte nicht erstellt werden. Internetverbindung prüfen und erneut versuchen.");
        }
        finally
        {
            m_Beschaeftigt = false;
        }
    }

    private async void ClientVerbinden()
    {
        if (m_Beschaeftigt || m_Netz == null) return;

        string code = m_CodeEingabe.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            FehlerZeigen("Bitte einen Join-Code eingeben.");
            return;
        }

        m_Beschaeftigt = true;
        KnoepfeAktiv(false);
        FehlerAusblenden();

        try
        {
            StatusSetzen("Verbinde mit Unity Services ...");
            await AnmeldenFallsNoetig();

            StatusSetzen("Suche Sitzung ...");
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);
            if (this == null) return;

            var transport = m_Netz.GetComponent<UnityTransport>();
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            if (!m_Netz.StartClient())
                throw new System.Exception("StartClient fehlgeschlagen");

            AktuellerJoinCode = code;
            StatusSetzen("Verbinde mit Host ...");
            // Weiter geht es in OnClientVerbunden bzw. OnClientGetrennt.
        }
        catch (System.Exception e)
        {
            Debug.LogError("[LobbyManager] Beitritt fehlgeschlagen: " + e.Message);
            if (this == null) return;
            StartseiteZeigen();
            FehlerZeigen("Code nicht gefunden oder abgelaufen. Bitte Code prüfen.");
        }
        finally
        {
            m_Beschaeftigt = false;
        }
    }

    private void OnClientVerbunden(ulong clientId)
    {
        if (clientId == m_Netz.LocalClientId && !m_Netz.IsServer)
        {
            Debug.Log("[LobbyManager] Mit Host verbunden.");
            LobbyZeigen();
        }
    }

    private void OnClientGetrennt(ulong clientId)
    {
        // Betrifft uns nur als Client: abgelehnt, Host weg oder Verbindung nie zustande gekommen.
        if (m_Netz.IsServer || clientId != m_Netz.LocalClientId) return;

        string grund = m_Netz.DisconnectReason;
        AktuellerJoinCode = "";
        StartseiteZeigen();
        FehlerZeigen(string.IsNullOrEmpty(grund) ? "Verbindung zum Host verloren." : grund);
    }

    private void RennenStarten()
    {
        if (m_Netz == null || !m_Netz.IsServer) return;

        m_KnopfStart.Aktiv = false;
        Debug.Log("[LobbyManager] Host startet das Rennen.");

        // Laedt die Szene fuer alle verbundenen Spieler. Bei den Clients legt NetzwerkSitzung
        // den Ladebildschirm darueber, sobald die Lade-Nachricht ankommt.
        UiUebergang.Zu("Lade Rennen ...", () =>
            m_Netz.SceneManager.LoadScene(NetzwerkSitzung.RennSzene, UnityEngine.SceneManagement.LoadSceneMode.Single));
    }

    private void LobbyVerlassen()
    {
        if (m_Netz != null && m_Netz.IsListening) m_Netz.Shutdown();
        AktuellerJoinCode = "";
        StartseiteZeigen();
    }

    private void CodeKopieren()
    {
        if (string.IsNullOrEmpty(AktuellerJoinCode)) return;
        GUIUtility.systemCopyBuffer = AktuellerJoinCode;
        m_KnopfKopieren.Text = "Kopiert!";
        m_KopiertBis = Time.unscaledTime + 1.5f;
    }

    // ------------------------------------------------------------------
    // Anzeige
    // ------------------------------------------------------------------

    private void StartseiteZeigen()
    {
        SeiteZeigen(false);
        KnoepfeAktiv(true);
        StatusSetzen("");
    }

    private void LobbyZeigen()
    {
        bool istHost = m_Netz.IsServer;

        // Eigenes Profil anmelden (doppelt haelt besser: beim Verbinden geschieht das bereits einmal).
        SpielerProfil.Sicherstellen(m_Netz);
        SpielerProfil.EigenesSenden();
        m_KnopfStart.gameObject.SetActive(istHost);
        m_KnopfStart.Aktiv = true;
        m_CodeAnzeige.text = AktuellerJoinCode;
        m_LobbyHinweis.text = istHost ? "Gib den Code an deine Mitspieler weiter." : "Warte, bis der Host das Rennen startet ...";
        StatusSetzen("");
        LobbyAktualisieren();
        SeiteZeigen(true);
    }

    private void KnoepfeAktiv(bool aktiv)
    {
        m_KnopfEinzel.Aktiv = aktiv;
        m_KnopfHost.Aktiv = aktiv;
        m_KnopfJoin.Aktiv = aktiv;
        m_KnopfAnsicht.Aktiv = aktiv;
        m_CodeEingabe.interactable = aktiv;
    }

    private void FehlerZeigen(string text)
    {
        if (m_Fehler != null) m_Fehler.text = text;
    }

    private void FehlerAusblenden()
    {
        if (m_Fehler != null) m_Fehler.text = "";
    }

    private void StatusSetzen(string text)
    {
        m_StatusBasis = text;
        if (m_Status != null) m_Status.text = text;
    }
}
