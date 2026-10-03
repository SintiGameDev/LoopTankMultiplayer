using System.Threading.Tasks;
using TMPro;
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
/// Menue und Lobby. Einzelspieler startet sofort als lokaler Host; Host/Join laufen ueber
/// Unity Relay mit Join-Code. Das Rennen startet der Host, die Szene wird dann fuer alle geladen.
/// </summary>
public class LobbyManager : MonoBehaviour
{
    [Header("Startseite")]
    [SerializeField] private GameObject startPanel;
    [SerializeField] private Button buttonEinzelspieler;
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonJoin;
    [SerializeField] private Button buttonBeenden;
    [SerializeField] private TMP_InputField joinCodeEingabe;

    [Header("Lobby")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private Button buttonStartRace;
    [SerializeField] private Button buttonStop;
    [SerializeField] private TextMeshProUGUI joinCodeAnzeige;
    [SerializeField] private TextMeshProUGUI spielerAnzahlAnzeige;

    [Header("Meldungen")]
    [SerializeField] private TextMeshProUGUI statusAnzeige;
    [SerializeField] private TextMeshProUGUI fehlerAnzeige;

    /// <summary>Aktueller Relay-Code, auch fuer die Debug-Anzeige.</summary>
    public static string AktuellerJoinCode { get; private set; } = "";

    private NetworkManager m_Netz;
    private bool m_Beschaeftigt;

    private void Start()
    {
        m_Netz = NetworkManager.Singleton;
        if (m_Netz == null)
        {
            Debug.LogError("[LobbyManager] Kein NetworkManager in der Szene.");
            return;
        }

        // Falls man aus einem Rennen zurueckkommt und noch eine Sitzung offen ist.
        if (m_Netz.IsListening) m_Netz.Shutdown();
        AktuellerJoinCode = "";

        buttonEinzelspieler.onClick.AddListener(EinzelspielerStarten);
        buttonHost.onClick.AddListener(HostStarten);
        buttonJoin.onClick.AddListener(ClientVerbinden);
        buttonStartRace.onClick.AddListener(RennenStarten);
        buttonStop.onClick.AddListener(LobbyVerlassen);
        buttonBeenden.onClick.AddListener(Application.Quit);
        joinCodeEingabe.onSubmit.AddListener(_ => ClientVerbinden());

        m_Netz.OnClientConnectedCallback += OnClientVerbunden;
        m_Netz.OnClientDisconnectCallback += OnClientGetrennt;

        StartseiteZeigen();
        StatusSetzen("");

        if (!string.IsNullOrEmpty(NetzwerkSitzung.LetzteMeldung))
        {
            FehlerZeigen(NetzwerkSitzung.LetzteMeldung);
            NetzwerkSitzung.LetzteMeldung = "";
        }
    }

    private void OnDestroy()
    {
        if (m_Netz != null)
        {
            m_Netz.OnClientConnectedCallback -= OnClientVerbunden;
            m_Netz.OnClientDisconnectCallback -= OnClientGetrennt;
        }
    }

    private void Update()
    {
        if (m_Netz != null && m_Netz.IsListening && lobbyPanel.activeSelf)
        {
            int anzahl = m_Netz.ConnectedClientsIds.Count;
            spielerAnzahlAnzeige.text = $"Spieler: {anzahl} / {NetzwerkSitzung.MaxSpieler}";
        }
    }

    // ------------------------------------------------------------------
    // Einzelspieler
    // ------------------------------------------------------------------

    private void EinzelspielerStarten()
    {
        if (m_Beschaeftigt) return;
        FehlerAusblenden();

        // Lokaler Host ohne Relay und ohne Unity-Dienste, funktioniert auch offline.
        var transport = m_Netz.GetComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", 7777, "127.0.0.1");

        if (!m_Netz.StartHost())
        {
            FehlerZeigen("Spiel konnte nicht gestartet werden.");
            return;
        }

        m_Netz.SceneManager.LoadScene(NetzwerkSitzung.RennSzene, UnityEngine.SceneManagement.LoadSceneMode.Single);
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
        if (m_Beschaeftigt) return;
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
        if (m_Beschaeftigt) return;

        string code = joinCodeEingabe.text.Trim().ToUpper();
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
        if (!m_Netz.IsServer) return;

        buttonStartRace.interactable = false;
        StatusSetzen("Lade Rennen ...");
        Debug.Log("[LobbyManager] Host startet das Rennen.");

        // Laedt die Szene fuer alle verbundenen Spieler.
        m_Netz.SceneManager.LoadScene(NetzwerkSitzung.RennSzene, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void LobbyVerlassen()
    {
        if (m_Netz.IsListening) m_Netz.Shutdown();
        AktuellerJoinCode = "";
        StartseiteZeigen();
        StatusSetzen("");
    }

    // ------------------------------------------------------------------
    // Anzeige
    // ------------------------------------------------------------------

    private void StartseiteZeigen()
    {
        startPanel.SetActive(true);
        lobbyPanel.SetActive(false);
        KnoepfeAktiv(true);
        StatusSetzen("");
    }

    private void LobbyZeigen()
    {
        startPanel.SetActive(false);
        lobbyPanel.SetActive(true);

        bool istHost = m_Netz.IsServer;
        buttonStartRace.gameObject.SetActive(istHost);
        buttonStartRace.interactable = true;
        joinCodeAnzeige.text = "Code: " + AktuellerJoinCode;
        StatusSetzen(istHost ? "Gib den Code an deine Mitspieler weiter." : "Warte, bis der Host das Rennen startet ...");
    }

    private void KnoepfeAktiv(bool aktiv)
    {
        buttonEinzelspieler.interactable = aktiv;
        buttonHost.interactable = aktiv;
        buttonJoin.interactable = aktiv;
        joinCodeEingabe.interactable = aktiv;
    }

    private void FehlerZeigen(string text)
    {
        if (fehlerAnzeige == null) return;
        fehlerAnzeige.text = text;
        fehlerAnzeige.gameObject.SetActive(true);
    }

    private void FehlerAusblenden()
    {
        if (fehlerAnzeige == null) return;
        fehlerAnzeige.text = "";
        fehlerAnzeige.gameObject.SetActive(false);
    }

    private void StatusSetzen(string text)
    {
        if (statusAnzeige != null)
            statusAnzeige.text = text;
    }
}
