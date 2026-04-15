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

    [Header("UI Felder")]
    [SerializeField] private TMP_InputField joinCodeEingabe;
    [SerializeField] private TextMeshProUGUI joinCodeAnzeige;
    [SerializeField] private TextMeshProUGUI statusAnzeige;
    [SerializeField] private TextMeshProUGUI fehlerAnzeige;

    [Header("Lobby Panel")]
    [SerializeField] private GameObject lobbyPanel;

    [Header("Einstellungen")]
    [SerializeField] private int maxSpieler = 4;

    // Aktueller Relay Code fuer Debug UI
    public static string AktuellerJoinCode { get; private set; } = "";

    private async void Start()
    {
        buttonHost.onClick.AddListener(HostStarten);
        buttonJoin.onClick.AddListener(ClientVerbinden);
        buttonStop.onClick.AddListener(Beenden);

        joinCodeAnzeige.text = "";
        FehlerAusblenden();
        StatusSetzen("Verbinde mit Unity Services...");
        LobbyAnzeigen(true);

        await UnityServicesInitialisieren();
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
            joinCodeEingabe.gameObject.SetActive(false);
            buttonJoin.gameObject.SetActive(false);
            LobbyAnzeigen(false);
            joinCodeAnzeige.gameObject.SetActive(true);
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

        // Fehler: Kein Code eingegeben
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

            // Eingabefeld ausblenden nach erfolgreicher Verbindung
            joinCodeEingabe.gameObject.SetActive(false);
            buttonJoin.gameObject.SetActive(false);
            LobbyAnzeigen(false);

            Debug.Log("[LobbyManager] Client verbunden mit Code: " + code);
        }
        catch (System.Exception e)
        {
            // Fehler: Code nicht gefunden oder abgelaufen
            StatusSetzen("Verbindung fehlgeschlagen.");
            FehlerZeigen("Code nicht gefunden oder abgelaufen. Bitte Code pruefen.");
            buttonJoin.interactable = true;
            Debug.LogError("[LobbyManager] Verbindung fehlgeschlagen: " + e.Message);
        }
    }

    private void Beenden()
    {
        NetworkManager.Singleton.Shutdown();
        AktuellerJoinCode = "";
        joinCodeAnzeige.text = "";
        joinCodeEingabe.gameObject.SetActive(true);
        buttonJoin.gameObject.SetActive(true);
        buttonHost.interactable = true;
        buttonJoin.interactable = true;
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
