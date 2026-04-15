using UnityEngine;
using Unity.Netcode;
using TMPro;

public class NetworkDebugUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI debugText;

    [Header("Farben")]
    [SerializeField] private Color farbeHost    = new Color(0.2f, 0.9f, 0.4f);
    [SerializeField] private Color farbeClient  = new Color(0.3f, 0.7f, 1.0f);
    [SerializeField] private Color farbeGetrennt = new Color(0.8f, 0.3f, 0.3f);

    private string FarbeAlsHex(Color c)
    {
        return ColorUtility.ToHtmlStringRGB(c);
    }

    void Update()
    {
        if (debugText == null) return;

        var nm = NetworkManager.Singleton;

        if (nm == null || !nm.IsListening)
        {
            debugText.text =
                "<color=#" + FarbeAlsHex(farbeGetrennt) + ">" +
                "● Nicht verbunden" +
                "</color>";
            return;
        }

        bool istHost   = nm.IsHost;
        bool istClient = nm.IsClient && !nm.IsHost;

        string rolle     = istHost ? "Host" : "Client";
        Color rollenFarbe = istHost ? farbeHost : farbeClient;
        string rollenHex  = FarbeAlsHex(rollenFarbe);

        int    spielerAnzahl = nm.ConnectedClients.Count;
        ulong  clientId      = nm.LocalClientId;

        float ping = 0f;
        if (istClient)
        {
            var transport = nm.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport != null)
                ping = transport.GetCurrentRtt(NetworkManager.ServerClientId);
        }

        string trennlinie = "<color=#555555>─────────────────</color>\n";

        string codeZeile = "";
        if (!string.IsNullOrEmpty(LobbyManager.AktuellerJoinCode))
        {
            string codeLabel = istHost ? "Host Code" : "Session";
            codeZeile = Zeile(codeLabel, "<b>" + LobbyManager.AktuellerJoinCode + "</b>");
        }

        string pingZeile = "";
        if (istClient)
        {
            string pingFarbe = ping < 60 ? "00cc66" : ping < 120 ? "ffaa00" : "ff4444";
            pingZeile = Zeile("Ping", "<color=#" + pingFarbe + ">" + ping + " ms</color>");
        }

        debugText.text =
            "<color=#" + rollenHex + "><b>● " + rolle + "</b></color>\n" +
            trennlinie +
            Zeile("Client ID", clientId.ToString()) +
            Zeile("Spieler", spielerAnzahl.ToString()) +
            codeZeile +
            pingZeile;
    }

    private string Zeile(string label, string wert)
    {
        return "<color=#aaaaaa>" + label + ":</color>  " + wert + "\n";
    }
}
