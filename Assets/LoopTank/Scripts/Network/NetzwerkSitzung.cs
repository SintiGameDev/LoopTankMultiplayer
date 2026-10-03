using TopDownRace;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Haengt am NetworkManager-Objekt der Menue-Szene und lebt (wie der NetworkManager selbst)
/// ueber Szenenwechsel hinweg. Kuemmert sich um alles, was die ganze Sitzung betrifft:
/// doppelte NetworkManager verhindern, Beitritte pruefen und bei Verbindungsende zurueck ins Menue.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class NetzwerkSitzung : MonoBehaviour
{
    public const string MenueSzene = "MainMenu";
    public const string RennSzene2D = "Race";
    public const string RennSzene3D = "Race3D";

    /// <summary>Welche Rennszene der Host laedt. Standard ist 3D, sobald die Szene im Build ist.</summary>
    public static string RennSzene
    {
        get => s_RennSzene ?? (Hat3D ? RennSzene3D : RennSzene2D);
        set => s_RennSzene = value;
    }
    private static string s_RennSzene;

    public static bool Hat3D => Application.CanStreamedLevelBeLoaded(RennSzene3D);
    public const int MaxSpieler = 4;

    /// <summary>Meldung, die das Menue nach einer unfreiwilligen Rueckkehr anzeigt.</summary>
    public static string LetzteMeldung = "";

    private static bool s_GewolltVerlassen;

    private NetworkManager m_Manager;

    private void Awake()
    {
        // Beim Zurueckkehren ins Menue wird die Szene neu geladen und bringt einen zweiten
        // NetworkManager mit. Der erste (DontDestroyOnLoad) bleibt, das Duplikat wird entfernt.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.gameObject != gameObject)
        {
            Destroy(gameObject);
            return;
        }

        m_Manager = GetComponent<NetworkManager>();
    }

    private void Start()
    {
        if (m_Manager == null) return;

        m_Manager.NetworkConfig.ConnectionApproval = true;
        m_Manager.ConnectionApprovalCallback = BeitrittPruefen;
        m_Manager.OnClientStopped += OnClientGestoppt;
        m_Manager.OnServerStarted += SzenenEreignisseAbonnieren;
        m_Manager.OnClientStarted += SzenenEreignisseAbonnieren;
    }

    private void OnDestroy()
    {
        if (m_Manager != null)
        {
            m_Manager.OnClientStopped -= OnClientGestoppt;
            m_Manager.OnServerStarted -= SzenenEreignisseAbonnieren;
            m_Manager.OnClientStarted -= SzenenEreignisseAbonnieren;
        }
    }

    /// <summary>
    /// Der Szenenmanager existiert erst, sobald Host oder Client laufen. Ab dann legt jede
    /// Lade-Nachricht den Ladebildschirm ueber das Bild: bei den Clients, wenn der Host das Rennen
    /// startet, und bei allen, wenn der Host eine neue Runde laedt.
    /// </summary>
    private void SzenenEreignisseAbonnieren()
    {
        if (m_Manager == null || m_Manager.SceneManager == null) return;
        m_Manager.SceneManager.OnSceneEvent -= OnSzenenEreignis;
        m_Manager.SceneManager.OnSceneEvent += OnSzenenEreignis;

        // Namen und Farben der Spieler gehoeren zur Sitzung und beginnen mit ihr.
        SpielerProfil.Starten(m_Manager);
    }

    private void OnSzenenEreignis(SceneEvent ereignis)
    {
        if (ereignis.SceneEventType == SceneEventType.Load)
            UiUebergang.Zu("Lade Rennen ...");
    }

    /// <summary>Wechselt hinter dem Ladebildschirm ins Menue. Dort gibt LobbyManager das Bild wieder frei.</summary>
    private static void InsMenue()
    {
        UiUebergang.Zu("Zurück zum Menü ...", () => SceneManager.LoadScene(MenueSzene));
    }

    private void BeitrittPruefen(NetworkManager.ConnectionApprovalRequest anfrage, NetworkManager.ConnectionApprovalResponse antwort)
    {
        // Panzer werden erst in der Rennszene gespawnt, nie automatisch beim Verbinden.
        antwort.CreatePlayerObject = false;
        antwort.Pending = false;

        // Der Host selbst laeuft auch durch diese Pruefung.
        if (anfrage.ClientNetworkId == NetworkManager.ServerClientId)
        {
            antwort.Approved = true;
            return;
        }

        if (SceneManager.GetActiveScene().name != MenueSzene)
        {
            antwort.Approved = false;
            antwort.Reason = "Das Rennen laeuft bereits.";
            return;
        }

        if (m_Manager.ConnectedClientsIds.Count >= MaxSpieler)
        {
            antwort.Approved = false;
            antwort.Reason = "Die Lobby ist voll.";
            return;
        }

        antwort.Approved = true;
    }

    private void OnClientGestoppt(bool warHost)
    {
        bool gewollt = s_GewolltVerlassen;
        s_GewolltVerlassen = false;

        if (SceneManager.GetActiveScene().name == MenueSzene) return;

        if (!gewollt && !warHost)
        {
            string grund = m_Manager != null ? m_Manager.DisconnectReason : "";
            LetzteMeldung = string.IsNullOrEmpty(grund) ? "Verbindung zum Host verloren." : grund;
        }

        InsMenue();
    }

    /// <summary>Trennt die Verbindung und kehrt ins Menue zurueck.</summary>
    public static void Verlassen()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            s_GewolltVerlassen = true;
            nm.Shutdown();
            return;
        }

        if (SceneManager.GetActiveScene().name != MenueSzene)
            InsMenue();
    }

    public static bool IstAktiv => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
}
