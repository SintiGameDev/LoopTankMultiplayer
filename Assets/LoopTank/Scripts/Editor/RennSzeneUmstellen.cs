using System.Collections.Generic;
using System.Reflection;
using TopDownRace;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Stellt die Rennszene vom alten Multiplayer-Versuch (Lobby-Overlay, NetworkManager und
/// SpawnManager direkt in der Szene) auf das neue Setup um: Der NetworkManager lebt in der
/// Menue-Szene, die Rennszene enthaelt nur noch die Rennleitung als Netzwerk-Objekt.
///
/// Menue: LoopTank > Rennszene auf neues Setup umstellen
/// Kann beliebig oft ausgefuehrt werden; was schon stimmt, bleibt unveraendert.
/// </summary>
public static class RennSzeneUmstellen
{
    private const string RennPfad = "Assets/LoopTank/Scenes/Race.unity";
    private const string MenuePfad = "Assets/LoopTank/Scenes/MainMenu.unity";
    private const string TankPfad = "Assets/LoopTank/Prefabs/Gameplay/Tank.prefab";

    // Objekte des alten Versuchs, die als leere Huellen in der Szene liegen koennen.
    private static readonly string[] AlteNamen =
    {
        "Multiplayer Setup", "LobbyUI", "LobbyManager", "SpawnManager", "NetworkManager", "Spawnpunkt1", "Spawnpunkt2",
    };

    [MenuItem("LoopTank/Rennszene auf neues Setup umstellen")]
    public static void Menue()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("LoopTank", "Bitte zuerst den Play-Modus beenden.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        try
        {
            List<string> bericht = Umstellen();
            string text = bericht.Count == 0
                ? "Die Rennszene war bereits auf dem neuen Stand. Nichts geändert."
                : "Erledigt:\n\n- " + string.Join("\n- ", bericht);
            Debug.Log("[RennSzeneUmstellen] " + text);
            EditorUtility.DisplayDialog("LoopTank – Rennszene umgestellt", text + "\n\nGestartet wird das Spiel aus der Szene 'MainMenu'.", "OK");
        }
        catch (System.Exception fehler)
        {
            Debug.LogError("[RennSzeneUmstellen] " + fehler.Message);
            EditorUtility.DisplayDialog("LoopTank – Umstellung abgebrochen", fehler.Message, "OK");
        }
    }

    /// <summary>Fuehrt die Umstellung aus und gibt zurueck, was geaendert wurde.</summary>
    public static List<string> Umstellen()
    {
        var bericht = new List<string>();

        GameObject tank = AssetDatabase.LoadAssetAtPath<GameObject>(TankPfad);
        if (tank == null || tank.GetComponent<NetworkObject>() == null)
            throw new System.InvalidOperationException("Das Panzer-Prefab hat noch kein NetworkObject. Bitte zuerst 'LoopTank > Multiplayer einrichten' ausführen.");

        var szene = EditorSceneManager.OpenScene(RennPfad, OpenSceneMode.Single);

        // Erst pruefen, dann aendern: Fehlt eines dieser Scripts, ist die Szene beschaedigt oder
        // die Scripts sind noch nicht fertig importiert. Dann nichts anfassen.
        var fehlend = new List<string>();
        if (Finde<RaceTrackControl>() == null) fehlend.Add("RaceTrackControl (Strecke)");
        if (Finde<Timer>() == null) fehlend.Add("Timer");
        if (Finde<CameraFollow>() == null) fehlend.Add("CameraFollow (Kamera)");
        if (Finde<GhostManager>() == null) fehlend.Add("GhostManager");
        if (Finde<UISystem>() == null) fehlend.Add("UISystem (ui-base)");
        if (fehlend.Count > 0)
            throw new System.InvalidOperationException("Race.unity ist unvollständig, es fehlt: " + string.Join(", ", fehlend) + ". Die Szene wurde nicht verändert.");

        AltesSetupEntfernen(szene, bericht);
        RennleitungEinrichten(tank, bericht);
        VerweiseBereinigen(bericht);

        if (Finde<RennHud>() == null)
        {
            new GameObject("RennHud").AddComponent<RennHud>();
            bericht.Add("RennHud (Spielerliste und Meldungen) hinzugefügt");
        }

        if (Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length == 0)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            bericht.Add("EventSystem ergänzt (ohne sind die Knöpfe im Ergebnis-Bildschirm nicht klickbar)");
        }

        BuildSettingsPruefen(bericht);

        if (bericht.Count > 0)
        {
            EditorSceneManager.MarkSceneDirty(szene);
            EditorSceneManager.SaveScene(szene);
        }

        // Netcode erkennt Szenenobjekte ueber einen Hash, der erst nach dem Speichern stabil ist.
        var leitung = Finde<GameControl>();
        if (NetzwerkIdAktualisieren(leitung.GetComponent<NetworkObject>()))
        {
            EditorSceneManager.MarkSceneDirty(szene);
            EditorSceneManager.SaveScene(szene);
        }

        return bericht;
    }

    // ------------------------------------------------------------------

    private static void AltesSetupEntfernen(UnityEngine.SceneManagement.Scene szene, List<string> bericht)
    {
        var zuLoeschen = new List<GameObject>();

        // 1. Alles, was in der Rennszene nichts mehr zu suchen hat, weil es jetzt im Menue lebt.
        foreach (var k in Object.FindObjectsByType<NetworkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) zuLoeschen.Add(k.gameObject);
        foreach (var k in Object.FindObjectsByType<UnityTransport>(FindObjectsInactive.Include, FindObjectsSortMode.None)) zuLoeschen.Add(k.gameObject);
        foreach (var k in Object.FindObjectsByType<LobbyManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)) zuLoeschen.Add(k.gameObject);
        foreach (var k in Object.FindObjectsByType<NetworkDebugUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)) zuLoeschen.Add(k.gameObject);
        foreach (var k in Object.FindObjectsByType<NetzwerkSitzung>(FindObjectsInactive.Include, FindObjectsSortMode.None)) zuLoeschen.Add(k.gameObject);

        // 2. Die Huellen des alten Versuchs, erkennbar am Namen.
        foreach (GameObject wurzel in szene.GetRootGameObjects())
        {
            foreach (Transform t in wurzel.GetComponentsInChildren<Transform>(true))
            {
                if (System.Array.IndexOf(AlteNamen, t.name) >= 0) zuLoeschen.Add(t.gameObject);
            }
        }

        // Ein EventSystem, das nur zum alten Lobby-Overlay gehoerte, verschwindet mit seinem Elternobjekt.
        var entfernt = new List<string>();
        foreach (GameObject objekt in zuLoeschen)
        {
            if (objekt == null) continue;   // schon zusammen mit einem Elternobjekt geloescht
            entfernt.Add(objekt.name);
            Object.DestroyImmediate(objekt);
        }

        if (entfernt.Count > 0)
            bericht.Add("Altes Multiplayer-Setup entfernt: " + string.Join(", ", entfernt));
    }

    private static void RennleitungEinrichten(GameObject tank, List<string> bericht)
    {
        var leitung = Finde<GameControl>();
        if (leitung == null)
        {
            leitung = new GameObject("GameControl").AddComponent<GameControl>();
            leitung.m_levelRounds = 99;
            bericht.Add("GameControl (Rennleitung) neu angelegt");
        }

        GameObject objekt = leitung.gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(objekt))
        {
            // Als eigenstaendiges Szenenobjekt, damit das NetworkObject sauber zur Szene gehoert.
            PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(objekt), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            bericht.Add("GameControl vom alten Prefab gelöst");
        }

        if (objekt.GetComponent<NetworkObject>() == null)
        {
            objekt.AddComponent<NetworkObject>();
            bericht.Add("GameControl ist jetzt ein Netzwerk-Objekt");
        }

        if (leitung.m_PlayerCarPrefab != tank)
        {
            leitung.m_PlayerCarPrefab = tank;
            bericht.Add("GameControl spawnt das Netzwerk-Panzer-Prefab");
        }

        var kamera = Finde<CameraFollow>();
        if (leitung.m_CameraFollow != kamera)
        {
            leitung.m_CameraFollow = kamera;
            bericht.Add("GameControl kennt die Kamera");
        }

        EditorUtility.SetDirty(leitung);
    }

    /// <summary>Verweise auf Prefabs, an deren Stelle zur Laufzeit die echten Panzer treten.</summary>
    private static void VerweiseBereinigen(List<string> bericht)
    {
        var kamera = Finde<CameraFollow>();
        var kameraSo = new SerializedObject(kamera);
        SerializedProperty ziel = kameraSo.FindProperty("m_Target");
        if (ziel.objectReferenceValue != null)
        {
            ziel.objectReferenceValue = null;
            kameraSo.ApplyModifiedPropertiesWithoutUndo();
            bericht.Add("Kamera folgt nicht mehr einem Prefab, sondern dem gespawnten Panzer");
        }

        var ghosts = Finde<GhostManager>();
        if (ghosts.playerRecorder != null)
        {
            ghosts.playerRecorder = null;
            EditorUtility.SetDirty(ghosts);
            bericht.Add("GhostManager nimmt den gespawnten Panzer auf statt eines Prefabs");
        }
    }

    private static void BuildSettingsPruefen(List<string> bericht)
    {
        var szenen = EditorBuildSettings.scenes;
        bool stimmt = szenen.Length == 2
            && szenen[0].path == MenuePfad && szenen[0].enabled
            && szenen[1].path == RennPfad && szenen[1].enabled;
        if (stimmt) return;

        if (!System.IO.File.Exists(MenuePfad))
            throw new System.InvalidOperationException("Die Menü-Szene fehlt. Bitte 'LoopTank > Multiplayer einrichten' ausführen.");

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuePfad, true),
            new EditorBuildSettingsScene(RennPfad, true),
        };
        bericht.Add("Build Settings: MainMenu (0), Race (1)");
    }

    /// <summary>Stoesst die Hash-Berechnung von Netcode an. True, wenn sich der Wert geaendert hat.</summary>
    internal static bool NetzwerkIdAktualisieren(NetworkObject netzObjekt)
    {
        if (netzObjekt == null) return false;

        var so = new SerializedObject(netzObjekt);
        long vorher = so.FindProperty("GlobalObjectIdHash").longValue;

        MethodInfo onValidate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (onValidate != null) onValidate.Invoke(netzObjekt, null);

        so.Update();
        long nachher = so.FindProperty("GlobalObjectIdHash").longValue;
        if (nachher == vorher) return false;

        EditorUtility.SetDirty(netzObjekt);
        return true;
    }

    private static T Finde<T>() where T : Object
    {
        return Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    }
}
