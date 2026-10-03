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
    public const string RennSzene = "Race";
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
    }

    private void OnDestroy()
    {
        if (m_Manager != null)
            m_Manager.OnClientStopped -= OnClientGestoppt;
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

        SceneManager.LoadScene(MenueSzene);
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
            SceneManager.LoadScene(MenueSzene);
    }

    public static bool IstAktiv => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
}
