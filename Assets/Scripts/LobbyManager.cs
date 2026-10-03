using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class LobbyManager : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button buttonHost;
    [SerializeField] private Button buttonJoin;
    [SerializeField] private Button buttonStop;
    [SerializeField] private Button buttonStartRace; // Nur für Host sichtbar

    [Header("UI Felder")]
    [SerializeField] private TMP_InputField joinCodeEingabe;
    [SerializeField] private TextMeshProUGUI joinCodeAnzeige;
    [SerializeField] private TextMeshProUGUI statusAnzeige;
    [SerializeField] private TextMeshProUGUI fehlerAnzeige;
    [SerializeField] private TextMeshProUGUI spielerAnzahlAnzeige;

    [Header("Lobby Panel")]
    [SerializeField] private GameObject lobbyPanel;

    [Header("Einstellungen")]
    [SerializeField] private int maxSpieler = 4;

    [Header("Szene")]
    [Tooltip("Exakter Name der Rennszene (wie in den Build Settings eingetragen).")]
    [SerializeField] private string rennSzenenName = "RaceScene";

    // Aktueller Relay Code fuer Debug UI
    public static string AktuellerJoinCode { get; private set; } = "";

    private bool istHost = false;

    private async void Start()
    {
        buttonHost.onClick.AddListener(HostStarten);
        buttonJoin.onClick.AddListener(ClientVerbinden);
        buttonStop.onClick.AddListener(Beenden);
        buttonStartRace.onClick.AddListener(RennenStarten);

        // Start-Rennen-Button initial ausblenden
        buttonStartRace.gameObject.SetActive(false);

        joinCodeAnzeige.text = "";
        if (spielerAnzahlAnzeige != null) spielerAnzahlAnzeige.text = "";
        FehlerAusblenden();
        StatusSetzen("Verbinde mit Unity Services...");
        LobbyAnzeigen(true);

        await UnityServicesInitialisieren();

        // Spielerzahl-Callback registrieren
        NetworkManager.Singleton.OnClientConnectedCallback += OnSpielerVerbunden;
        NetworkManager.Singleton.OnClientDisconnectCallback += OnSpielerGetrennt;
    }

    private void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnSpielerVerbunden;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnSpielerGetrennt;
        }
    }

    private void OnSpielerVerbunden(ulong clientId)
    {
        SpielerAnzahlAktualisieren();
    }

    private void OnSpielerGetrennt(ulong clientId)
    {
        SpielerAnzahlAktualisieren();
    }

    private void SpielerAnzahlAktualisieren()
    {
        if (spielerAnzahlAnzeige == null) return;
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        int anzahl = NetworkManager.Singleton.ConnectedClients.Count;
        spielerAnzahlAnzeige.text = $"Spieler: {anzahl} / {maxSpieler}";
    }

    private async Task UnityServicesInitialisieren()
    {
        try
        {
            await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            StatusSetzen("Bereit.");
            Debug.Log("[LobbyManager] Angemeldet als: "
                      + AuthenticationService.Instance.PlayerId);
        }
        catch (System.Exception e)
        {
            StatusSetzen("Dienst nicht erreichbar.");
            Debug.LogError("[LobbyManager] Initialisierung fehlgeschlagen: " + e.Message);
        }
    }

    private async void HostStarten()
    {
        try
        {
            buttonHost.interactable = false;
            FehlerAusblenden();
            StatusSetzen("Erstelle Session...");

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxSpieler);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            AktuellerJoinCode = joinCode;
            joinCodeAnzeige.text = "Code: " + joinCode;
            StatusSetzen("Warte auf Spieler...");
            Debug.Log("[LobbyManager] Join Code: " + joinCode);

            var relayData = AllocationUtils.ToRelayServerData(allocation, "dtls");
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(relayData);

            NetworkManager.Singleton.StartHost();

            istHost = true;
            joinCodeEingabe.gameObject.SetActive(false);
            buttonJoin.gameObject.SetActive(false);

            // Start-Rennen-Button nur für Host einblenden
            buttonStartRace.gameObject.SetActive(true);
            buttonStartRace.interactable = true;

            LobbyAnzeigen(false);
            joinCodeAnzeige.gameObject.SetActive(true);
            SpielerAnzahlAktualisieren();
        }
        catch (System.Exception e)
        {
            StatusSetzen("Fehler beim Starten.");
            FehlerZeigen("Session konnte nicht erstellt werden. Bitte erneut versuchen.");
            buttonHost.interactable = true;
            Debug.LogError("[LobbyManager] Host-Start fehlgeschlagen: " + e.Message);
        }
    }

    private async void ClientVerbinden()
    {
        string code = joinCodeEingabe.text.Trim().ToUpper();

        if (string.IsNullOrEmpty(code))
        {
            FehlerZeigen("Bitte einen Join Code eingeben.");
            return;
        }

        try
        {
            buttonJoin.interactable = false;
            FehlerAusblenden();
            StatusSetzen("Verbinde...");

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(code);

            AktuellerJoinCode = code;

            var relayData = AllocationUtils.ToRelayServerData(joinAllocation, "dtls");
            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetRelayServerData(relayData);

            NetworkManager.Singleton.StartClient();

            istHost = false;
            joinCodeEingabe.gameObject.SetActive(false);
            buttonJoin.gameObject.SetActive(false);
            LobbyAnzeigen(false);

            // Client wartet – Szenenübergang kommt automatisch vom Host
            StatusSetzen("Warte auf Spielstart...");
            joinCodeAnzeige.gameObject.SetActive(true);

            Debug.Log("[LobbyManager] Client verbunden mit Code: " + code);
        }
        catch (System.Exception e)
        {
            StatusSetzen("Verbindung fehlgeschlagen.");
            FehlerZeigen("Code nicht gefunden oder abgelaufen. Bitte Code pruefen.");
            buttonJoin.interactable = true;
            Debug.LogError("[LobbyManager] Verbindung fehlgeschlagen: " + e.Message);
        }
    }

    // Nur der Host darf diese Methode aufrufen
    private void RennenStarten()
    {
        if (!NetworkManager.Singleton.IsHost)
        {
            Debug.LogWarning("[LobbyManager] Nur der Host kann das Rennen starten.");
            return;
        }

        if (string.IsNullOrEmpty(rennSzenenName))
        {
            FehlerZeigen("Kein Szenenname angegeben! Bitte im Inspector eintragen.");
            Debug.LogError("[LobbyManager] rennSzenenName ist leer.");
            return;
        }

        Debug.Log($"[LobbyManager] Host startet Rennen – lade Szene: {rennSzenenName}");
        buttonStartRace.interactable = false;
        StatusSetzen("Lade Rennen...");

        // Lädt die Szene für alle verbundenen Clients gleichzeitig
        NetworkManager.Singleton.SceneManager.LoadScene(rennSzenenName, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    private void Beenden()
    {
        NetworkManager.Singleton.Shutdown();
        AktuellerJoinCode = "";
        istHost = false;

        joinCodeAnzeige.text = "";
        joinCodeAnzeige.gameObject.SetActive(true);
        joinCodeEingabe.gameObject.SetActive(true);
        buttonJoin.gameObject.SetActive(true);
        buttonStartRace.gameObject.SetActive(false);
        buttonHost.interactable = true;
        buttonJoin.interactable = true;

        if (spielerAnzahlAnzeige != null) spielerAnzahlAnzeige.text = "";
        FehlerAusblenden();
        StatusSetzen("Bereit.");
        LobbyAnzeigen(true);
        Debug.Log("[LobbyManager] Verbindung getrennt.");
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

    private void LobbyAnzeigen(bool anzeigen)
    {
        lobbyPanel.SetActive(anzeigen);
    }

    private void StatusSetzen(string text)
    {
        if (statusAnzeige != null)
            statusAnzeige.text = text;
    }
}