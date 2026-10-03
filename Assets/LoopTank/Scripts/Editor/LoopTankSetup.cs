using System.Reflection;
using TMPro;
using TopDownRace;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Baut alles auf, was der Multiplayer an Szenen- und Prefab-Verdrahtung braucht:
/// Panzer-Prefab mit Netzwerk-Komponenten, Netzwerk-Prefab-Liste, Menue-Szene mit
/// NetworkManager und Lobby, Rennszene mit Rennleitung, Build Settings.
/// Kann jederzeit erneut ausgefuehrt werden (Menue: LoopTank > Multiplayer einrichten);
/// die Menue-Szene wird dabei neu erzeugt, die Rennszene nur ergaenzt.
/// </summary>
public static class LoopTankSetup
{
    private const string Wurzel = "Assets/LoopTank";
    private const string MenuePfad = Wurzel + "/Scenes/MainMenu.unity";
    private const string RennPfad = Wurzel + "/Scenes/Race.unity";
    private const string TankPfad = Wurzel + "/Prefabs/Gameplay/Tank.prefab";
    private const string UIDataPfad = Wurzel + "/Data/UIData.asset";
    private const string PrefabListePfad = "Assets/DefaultNetworkPrefabs.asset";

    private static readonly Color Akzent = new Color(0.10f, 0.83f, 0.85f);
    private static readonly Color Dunkel = new Color(0.03f, 0.07f, 0.14f);

    [MenuItem("LoopTank/Multiplayer einrichten")]
    public static void EinrichtenMenue()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Einrichten();
        EditorSceneManager.OpenScene(MenuePfad);
        EditorUtility.DisplayDialog("LoopTank", "Multiplayer ist eingerichtet.\n\nStarte das Spiel aus der Szene 'MainMenu'.", "OK");
    }

    /// <summary>Auch per Kommandozeile aufrufbar: -executeMethod LoopTankSetup.Einrichten</summary>
    public static void Einrichten()
    {
        GameObject tank = TankEinrichten();
        NetworkPrefabsList liste = PrefabListeEinrichten(tank);
        MenueSzeneErzeugen(liste);
        BuildSettingsSetzen();
        RennSzeneEinrichten(tank);
        UIDataAufraeumen();
        AssetDatabase.SaveAssets();
        Debug.Log("[LoopTankSetup] Fertig.");
    }

    // ------------------------------------------------------------------
    // Panzer-Prefab
    // ------------------------------------------------------------------

    private static GameObject TankEinrichten()
    {
        GameObject inhalt = PrefabUtility.LoadPrefabContents(TankPfad);

        Sicherstellen<NetworkObject>(inhalt);

        // Jeder Client bewegt seinen eigenen Panzer selbst (Owner-Autoritaet), alle anderen
        // bekommen Position und Drehung interpoliert. In 2D reichen X, Y und die Z-Drehung.
        var nt = Sicherstellen<NetworkTransform>(inhalt);
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        nt.SyncPositionX = true;
        nt.SyncPositionY = true;
        nt.SyncPositionZ = false;
        nt.SyncRotAngleX = false;
        nt.SyncRotAngleY = false;
        nt.SyncRotAngleZ = true;
        nt.SyncScaleX = false;
        nt.SyncScaleY = false;
        nt.SyncScaleZ = false;
        nt.Interpolate = true;

        // Macht fremde Panzer kinematisch, damit nur der Besitzer Physik rechnet.
        Sicherstellen<NetworkRigidbody2D>(inhalt);

        PrefabUtility.SaveAsPrefabAsset(inhalt, TankPfad, out bool gespeichert);
        if (!gespeichert) throw new System.InvalidOperationException("Tank-Prefab konnte nicht gespeichert werden (siehe Konsole).");
        PrefabUtility.UnloadPrefabContents(inhalt);
        AssetDatabase.ImportAsset(TankPfad);

        GameObject tank = AssetDatabase.LoadAssetAtPath<GameObject>(TankPfad);
        NetzwerkIdAktualisieren(tank.GetComponent<NetworkObject>());
        AssetDatabase.SaveAssets();
        return tank;
    }

    /// <summary>
    /// Netcode erkennt Prefabs und Szenenobjekte ueber einen Hash, den es in OnValidate berechnet.
    /// Bei per Script hinzugefuegten Komponenten stossen wir das hier ausdruecklich an.
    /// </summary>
    private static void NetzwerkIdAktualisieren(NetworkObject netzObjekt)
    {
        if (netzObjekt == null) return;
        MethodInfo onValidate = typeof(NetworkObject).GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (onValidate != null) onValidate.Invoke(netzObjekt, null);
        EditorUtility.SetDirty(netzObjekt);
    }

    private static NetworkPrefabsList PrefabListeEinrichten(GameObject tank)
    {
        var liste = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListePfad);
        if (liste == null)
        {
            liste = ScriptableObject.CreateInstance<NetworkPrefabsList>();
            AssetDatabase.CreateAsset(liste, PrefabListePfad);
        }

        // Eintraege entfernen, deren Prefab nicht mehr existiert.
        var so = new SerializedObject(liste);
        SerializedProperty eintraege = so.FindProperty("List");
        for (int i = eintraege.arraySize - 1; i >= 0; i--)
        {
            if (eintraege.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab").objectReferenceValue == null)
                eintraege.DeleteArrayElementAtIndex(i);
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        if (!liste.Contains(tank))
            liste.Add(new NetworkPrefab { Prefab = tank });

        EditorUtility.SetDirty(liste);
        return liste;
    }

    // ------------------------------------------------------------------
    // Menue-Szene
    // ------------------------------------------------------------------

    private static void MenueSzeneErzeugen(NetworkPrefabsList liste)
    {
        var szene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Kamera
        var kameraObjekt = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        kameraObjekt.tag = "MainCamera";
        kameraObjekt.transform.position = new Vector3(0, 0, -10);
        var kamera = kameraObjekt.GetComponent<Camera>();
        kamera.orthographic = true;
        kamera.clearFlags = CameraClearFlags.SolidColor;
        kamera.backgroundColor = Dunkel;

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        // NetworkManager: bleibt ueber Szenenwechsel bestehen.
        var netzObjekt = new GameObject("NetworkManager");
        var transport = netzObjekt.AddComponent<UnityTransport>();
        var netz = netzObjekt.AddComponent<NetworkManager>();
        netzObjekt.AddComponent<NetzwerkSitzung>();
        netz.NetworkConfig.NetworkTransport = transport;
        netz.NetworkConfig.PlayerPrefab = null;
        netz.NetworkConfig.ConnectionApproval = true;
        netz.NetworkConfig.EnableSceneManagement = true;
        netz.NetworkConfig.Prefabs.NetworkPrefabsLists.Clear();
        netz.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(liste);
        netz.RunInBackground = true;

        // Menue und Lobby: Die Oberflaeche baut LobbyManager beim Start selbst auf (siehe UiBau).
        // In der Szene liegt deshalb nur das Script, keine Knoepfe und keine Verdrahtung.
        new GameObject("LobbyManager").AddComponent<LobbyManager>();

        EditorSceneManager.SaveScene(szene, MenuePfad);
    }

    // ------------------------------------------------------------------
    // Rennszene
    // ------------------------------------------------------------------

    private static void RennSzeneEinrichten(GameObject tank)
    {
        // Die Umstellung der Rennszene hat ein eigenes Script, das auch einzeln im Menue haengt.
        RennSzeneUmstellen.Umstellen();
    }

    private static void BuildSettingsSetzen()
    {
        RennSzeneUmstellen.BuildSettingsPruefen(null);
    }

    private static void UIDataAufraeumen()
    {
        var daten = AssetDatabase.LoadAssetAtPath<UIData>(UIDataPfad);
        if (daten == null || daten.m_UIPrefabs == null) return;

        // Das alte Level-Auswahl-Menue des Asset-Packs wird nicht mehr benutzt.
        var behalten = new System.Collections.Generic.List<GameObject>();
        foreach (GameObject prefab in daten.m_UIPrefabs)
        {
            if (prefab != null && prefab.name != "MainMenu") behalten.Add(prefab);
        }
        if (behalten.Count != daten.m_UIPrefabs.Length)
        {
            daten.m_UIPrefabs = behalten.ToArray();
            EditorUtility.SetDirty(daten);
        }
    }

    // ------------------------------------------------------------------
    // Bausteine
    // ------------------------------------------------------------------

    private static T Sicherstellen<T>(GameObject objekt) where T : Component
    {
        T komponente = objekt.GetComponent<T>();
        return komponente != null ? komponente : objekt.AddComponent<T>();
    }
}
