using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Anzeigename und Farbe der Spieler.
///
/// Die eigene Wahl liegt in den PlayerPrefs und gilt ueber Sitzungen hinweg. In einer Sitzung
/// fuehrt der Host die Liste aller Spieler und verteilt sie an alle, sobald sich etwas aendert.
/// Das laeuft ueber benannte Nachrichten statt ueber ein Netzwerk-Objekt, weil es in der Lobby
/// (Menue-Szene) noch keine gespawnten Objekte gibt.
///
/// Regel fuer Farben: Jede Farbe gibt es nur einmal. Wer eine vergebene Farbe waehlt, behaelt
/// seine bisherige; wer noch keine hat, bekommt die erste freie. So bleiben Panzer und Ghosts
/// im Rennen unterscheidbar.
/// </summary>
public static class SpielerProfil
{
    public struct Eintrag
    {
        public string Name;
        public int Farbe;
    }

    public const int MaxNamenLaenge = 16;

    private const string NachrichtSetzen = "LoopTank.ProfilSetzen";   // Client -> Host
    private const string NachrichtListe = "LoopTank.ProfilListe";     // Host -> alle
    private const string PrefName = "LoopTank.Name";
    private const string PrefFarbe = "LoopTank.Farbe";

    /// <summary>Alle Spieler der laufenden Sitzung, nach Client-ID. Bei allen Teilnehmern gleich.</summary>
    public static readonly Dictionary<ulong, Eintrag> Alle = new Dictionary<ulong, Eintrag>();

    /// <summary>Wird ausgeloest, wenn sich die Liste geaendert hat.</summary>
    public static event Action Geaendert;

    private static NetworkManager s_Netz;

    /// <summary>Zaehler fuer die Diagnose in der Lobby: abgeschickte und empfangene Profil-Nachrichten.</summary>
    public static int Gesendet { get; private set; }
    public static int Empfangen { get; private set; }

    // ------------------------------------------------------------------
    // Eigene Wahl
    // ------------------------------------------------------------------

    public static string EigenerName
    {
        get => Bereinigen(PlayerPrefs.GetString(PrefName, ""));
        set => PlayerPrefs.SetString(PrefName, Bereinigen(value));
    }

    /// <summary>-1 = noch keine gewaehlt, der Host teilt dann die erste freie zu.</summary>
    public static int EigeneFarbe
    {
        get => PlayerPrefs.GetInt(PrefFarbe, -1);
        set => PlayerPrefs.SetInt(PrefFarbe, value);
    }

    /// <summary>Kuerzt den Namen und entfernt Zeichen, mit denen sich Text-Formatierung einschleusen liesse.</summary>
    public static string Bereinigen(string name)
    {
        if (string.IsNullOrEmpty(name)) return "";
        name = name.Replace("<", "").Replace(">", "").Replace("\n", " ").Replace("\r", " ").Trim();
        return name.Length > MaxNamenLaenge ? name.Substring(0, MaxNamenLaenge) : name;
    }

    /// <summary>Profil eines Spielers; fehlt es (noch), gibt es einen Standardnamen und eine Farbe nach Platz.</summary>
    public static Eintrag Fuer(ulong clientId, int slot)
    {
        if (Alle.TryGetValue(clientId, out Eintrag eintrag))
        {
            if (string.IsNullOrEmpty(eintrag.Name)) eintrag.Name = SpielerFarben.StandardName(slot);
            return eintrag;
        }
        return new Eintrag { Name = SpielerFarben.StandardName(slot), Farbe = slot % SpielerFarben.Anzahl };
    }

    // ------------------------------------------------------------------
    // Sitzung
    // ------------------------------------------------------------------

    /// <summary>Beim Start von Host oder Client aufrufen (macht NetzwerkSitzung).</summary>
    public static void Starten(NetworkManager netz)
    {
        if (netz == null || netz.CustomMessagingManager == null) return;

        if (s_Netz != null)
        {
            s_Netz.OnClientConnectedCallback -= OnVerbunden;
            s_Netz.OnClientDisconnectCallback -= OnGetrennt;
        }
        s_Netz = netz;
        Alle.Clear();

        netz.CustomMessagingManager.RegisterNamedMessageHandler(NachrichtSetzen, OnSetzenEmpfangen);
        netz.CustomMessagingManager.RegisterNamedMessageHandler(NachrichtListe, OnListeEmpfangen);
        netz.OnClientConnectedCallback += OnVerbunden;
        netz.OnClientDisconnectCallback += OnGetrennt;

        // Der Host traegt sich selbst sofort ein.
        if (netz.IsServer) ServerSetzen(netz.LocalClientId, EigenerName, EigeneFarbe);
    }

    /// <summary>Startet die Profil-Verwaltung, falls sie fuer diese Sitzung noch nicht laeuft.</summary>
    public static void Sicherstellen(NetworkManager netz)
    {
        if (netz != null && s_Netz != netz) Starten(netz);
    }

    /// <summary>Schickt die eigene Wahl an den Host (bzw. traegt sie als Host direkt ein).</summary>
    public static void EigenesSenden()
    {
        if (s_Netz == null || !s_Netz.IsListening) return;

        if (s_Netz.IsServer)
        {
            ServerSetzen(s_Netz.LocalClientId, EigenerName, EigeneFarbe);
            return;
        }
        if (!s_Netz.IsConnectedClient) return;

        // Sofort lokal anzeigen, damit die eigene Auswahl ohne Wartezeit reagiert. Die Liste des
        // Hosts kommt gleich danach und hat das letzte Wort (z. B. wenn die Farbe schon vergeben ist).
        Alle.TryGetValue(s_Netz.LocalClientId, out Eintrag eigener);
        eigener.Name = EigenerName;
        int wunsch = EigeneFarbe;
        if (wunsch >= 0 && wunsch < SpielerFarben.Anzahl && !FarbeVergeben(wunsch, s_Netz.LocalClientId)) eigener.Farbe = wunsch;
        Alle[s_Netz.LocalClientId] = eigener;
        Geaendert?.Invoke();
        Gesendet++;

        using (var schreiber = new FastBufferWriter(256, Allocator.Temp))
        {
            schreiber.WriteValueSafe(EigenerName);
            schreiber.WriteValueSafe(EigeneFarbe);
            s_Netz.CustomMessagingManager.SendNamedMessage(NachrichtSetzen, NetworkManager.ServerClientId, schreiber, NetworkDelivery.ReliableSequenced);
        }
    }

    private static void OnVerbunden(ulong clientId)
    {
        if (s_Netz == null) return;

        if (s_Netz.IsServer)
        {
            // Neuer Spieler: bekommt sofort einen Eintrag mit freier Farbe und die aktuelle Liste.
            if (!Alle.ContainsKey(clientId)) ServerSetzen(clientId, "", -1);
            else ListeVerteilen();
        }
        else if (clientId == s_Netz.LocalClientId)
        {
            EigenesSenden();
        }
    }

    private static void OnGetrennt(ulong clientId)
    {
        Empfangen++;
        if (Alle.Remove(clientId)) ListeVerteilen();
    }

    private static void OnSetzenEmpfangen(ulong absender, FastBufferReader leser)
    {
        if (s_Netz == null || !s_Netz.IsServer) return;
        Empfangen++;
        leser.ReadValueSafe(out string name);
        leser.ReadValueSafe(out int farbe);
        ServerSetzen(absender, name, farbe);
    }

    /// <summary>Host: uebernimmt die Wahl eines Spielers (soweit erlaubt) und verteilt die Liste.</summary>
    private static void ServerSetzen(ulong clientId, string name, int farbe)
    {
        bool hatte = Alle.TryGetValue(clientId, out Eintrag eintrag);
        eintrag.Name = Bereinigen(name);

        bool gueltig = farbe >= 0 && farbe < SpielerFarben.Anzahl && !FarbeVergeben(farbe, clientId);
        if (gueltig) eintrag.Farbe = farbe;
        else if (!hatte) eintrag.Farbe = ErsteFreieFarbe();

        Alle[clientId] = eintrag;
        ListeVerteilen();
    }

    public static bool FarbeVergeben(int farbe, ulong ausser)
    {
        foreach (var paar in Alle)
        {
            if (paar.Key != ausser && paar.Value.Farbe == farbe) return true;
        }
        return false;
    }

    private static int ErsteFreieFarbe()
    {
        for (int i = 0; i < SpielerFarben.Anzahl; i++)
        {
            if (!FarbeVergeben(i, ulong.MaxValue)) return i;
        }
        return 0;
    }

    private static void ListeVerteilen()
    {
        Geaendert?.Invoke();
        if (s_Netz == null || !s_Netz.IsServer || s_Netz.CustomMessagingManager == null) return;

        using (var schreiber = new FastBufferWriter(1024, Allocator.Temp))
        {
            schreiber.WriteValueSafe(Alle.Count);
            foreach (var paar in Alle)
            {
                schreiber.WriteValueSafe(paar.Key);
                schreiber.WriteValueSafe(paar.Value.Name ?? "");
                schreiber.WriteValueSafe(paar.Value.Farbe);
            }
            s_Netz.CustomMessagingManager.SendNamedMessageToAll(NachrichtListe, schreiber, NetworkDelivery.ReliableSequenced);
        }
    }

    private static void OnListeEmpfangen(ulong absender, FastBufferReader leser)
    {
        if (s_Netz == null || s_Netz.IsServer) return;   // der Host fuehrt die Liste selbst
        Empfangen++;

        leser.ReadValueSafe(out int anzahl);
        Alle.Clear();
        for (int i = 0; i < anzahl && i < 64; i++)
        {
            leser.ReadValueSafe(out ulong id);
            leser.ReadValueSafe(out string name);
            leser.ReadValueSafe(out int farbe);
            Alle[id] = new Eintrag { Name = Bereinigen(name), Farbe = farbe };
        }
        Geaendert?.Invoke();
    }
}
