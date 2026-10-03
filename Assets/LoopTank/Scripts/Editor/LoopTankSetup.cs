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

        // Oberflaeche
        TMP_FontAsset titelSchrift = SchriftFinden("CaviarModern");

        var canvasObjekt = new GameObject("Menue", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObjekt.layer = LayerMask.NameToLayer("UI");
        var canvas = canvasObjekt.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObjekt.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Transform wurzel = canvasObjekt.transform;

        var hintergrund = Bild(wurzel, "Hintergrund", Dunkel);
        Strecken(hintergrund.rectTransform);

        var titel = Text(wurzel, "Titel", "LoopTank", 150, titelSchrift, Akzent);
        Platzieren(titel.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(1400, 190));

        // --- Startseite ---
        var startPanel = Panel(wurzel, "StartPanel");
        Button knopfEinzel = Knopf(startPanel, "ButtonEinzelspieler", "Einzelspieler", new Vector2(0, 130), titelSchrift);
        Button knopfHost = Knopf(startPanel, "ButtonHost", "Host starten", new Vector2(0, 20), titelSchrift);

        TMP_InputField eingabe = Eingabefeld(startPanel, "JoinCodeEingabe", "Join-Code", new Vector2(-135, -120), new Vector2(250, 90));
        Button knopfJoin = Knopf(startPanel, "ButtonJoin", "Beitreten", new Vector2(135, -120), titelSchrift, new Vector2(250, 90));
        Button knopfBeenden = Knopf(startPanel, "ButtonBeenden", "Beenden", new Vector2(0, -260), titelSchrift);
        Faerben(knopfBeenden, new Color(0.30f, 0.36f, 0.46f), Color.white);

        // --- Lobby ---
        var lobbyPanel = Panel(wurzel, "LobbyPanel");
        var lobbyTitel = Text(lobbyPanel, "LobbyTitel", "Lobby", 70, titelSchrift, Color.white);
        Platzieren(lobbyTitel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 190), new Vector2(900, 90));
        var codeAnzeige = Text(lobbyPanel, "JoinCodeAnzeige", "Code: ------", 96, null, Akzent);
        Platzieren(codeAnzeige.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 70), new Vector2(1200, 120));
        var spielerAnzeige = Text(lobbyPanel, "SpielerAnzahlAnzeige", "Spieler: 1 / 4", 48, null, Color.white);
        Platzieren(spielerAnzeige.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(900, 70));
        Button knopfStart = Knopf(lobbyPanel, "ButtonStartRace", "Rennen starten", new Vector2(0, -150), titelSchrift);
        Button knopfVerlassen = Knopf(lobbyPanel, "ButtonStop", "Lobby verlassen", new Vector2(0, -260), titelSchrift);
        Faerben(knopfVerlassen, new Color(0.30f, 0.36f, 0.46f), Color.white);
        lobbyPanel.gameObject.SetActive(false);

        // --- Meldungen ---
        var fehler = Text(wurzel, "FehlerAnzeige", "", 36, null, new Color(1f, 0.35f, 0.35f));
        Platzieren(fehler.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1600, 60));
        var status = Text(wurzel, "StatusAnzeige", "", 34, null, new Color(0.8f, 0.85f, 0.9f));
        Platzieren(status.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(1600, 60));

        var hinweis = Text(wurzel, "Steuerung", "W/A/S/D  fahren      Leertaste  Boost      Pfeiltasten  Turm", 26, null, new Color(0.55f, 0.62f, 0.70f));
        Platzieren(hinweis.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 35), new Vector2(1600, 40));

        // --- Netzwerk-Status oben rechts ---
        var debugText = Text(wurzel, "DebugText", "", 24, null, Color.white);
        debugText.alignment = TextAlignmentOptions.TopRight;
        Platzieren(debugText.rectTransform, new Vector2(1, 1), new Vector2(-30, -30), new Vector2(420, 200));
        debugText.rectTransform.pivot = new Vector2(1, 1);
        debugText.rectTransform.anchoredPosition = new Vector2(-30, -30);

        // --- Logik ---
        var lobbyObjekt = new GameObject("LobbyManager");
        var lobby = lobbyObjekt.AddComponent<LobbyManager>();
        var so = new SerializedObject(lobby);
        so.FindProperty("startPanel").objectReferenceValue = startPanel.gameObject;
        so.FindProperty("buttonEinzelspieler").objectReferenceValue = knopfEinzel;
        so.FindProperty("buttonHost").objectReferenceValue = knopfHost;
        so.FindProperty("buttonJoin").objectReferenceValue = knopfJoin;
        so.FindProperty("buttonBeenden").objectReferenceValue = knopfBeenden;
        so.FindProperty("joinCodeEingabe").objectReferenceValue = eingabe;
        so.FindProperty("lobbyPanel").objectReferenceValue = lobbyPanel.gameObject;
        so.FindProperty("buttonStartRace").objectReferenceValue = knopfStart;
        so.FindProperty("buttonStop").objectReferenceValue = knopfVerlassen;
        so.FindProperty("joinCodeAnzeige").objectReferenceValue = codeAnzeige;
        so.FindProperty("spielerAnzahlAnzeige").objectReferenceValue = spielerAnzeige;
        so.FindProperty("statusAnzeige").objectReferenceValue = status;
        so.FindProperty("fehlerAnzeige").objectReferenceValue = fehler;
        so.ApplyModifiedPropertiesWithoutUndo();

        var debug = lobbyObjekt.AddComponent<NetworkDebugUI>();
        var debugSo = new SerializedObject(debug);
        debugSo.FindProperty("debugText").objectReferenceValue = debugText;
        debugSo.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(szene, MenuePfad);
    }

    // ------------------------------------------------------------------
    // Rennszene
    // ------------------------------------------------------------------

    private static void RennSzeneEinrichten(GameObject tank)
    {
        var szene = EditorSceneManager.OpenScene(RennPfad, OpenSceneMode.Single);

        // Reste des alten Multiplayer-Versuchs (Lobby-Overlay, leere Manager-Huellen).
        foreach (GameObject wurzel in szene.GetRootGameObjects())
        {
            if (wurzel.name == "Multiplayer Setup") Object.DestroyImmediate(wurzel);
        }
        foreach (GameObject wurzel in szene.GetRootGameObjects())
        {
            foreach (Transform t in wurzel.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        }

        // Rennleitung als festes Netzwerk-Objekt der Szene.
        var leitung = Object.FindFirstObjectByType<GameControl>(FindObjectsInactive.Include);
        if (leitung == null)
        {
            leitung = new GameObject("GameControl").AddComponent<GameControl>();
            leitung.m_levelRounds = 99;
        }
        GameObject leitungObjekt = leitung.gameObject;
        if (PrefabUtility.IsPartOfPrefabInstance(leitungObjekt))
        {
            PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(leitungObjekt), PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        Sicherstellen<NetworkObject>(leitungObjekt);
        leitung.m_PlayerCarPrefab = tank;
        leitung.m_CameraFollow = Object.FindFirstObjectByType<CameraFollow>(FindObjectsInactive.Include);
        EditorUtility.SetDirty(leitung);

        // Verweise auf Prefabs, die erst zur Laufzeit durch echte Panzer ersetzt werden.
        if (leitung.m_CameraFollow != null)
        {
            var kameraSo = new SerializedObject(leitung.m_CameraFollow);
            kameraSo.FindProperty("m_Target").objectReferenceValue = null;
            kameraSo.ApplyModifiedPropertiesWithoutUndo();
        }
        var ghosts = Object.FindFirstObjectByType<GhostManager>(FindObjectsInactive.Include);
        if (ghosts != null)
        {
            ghosts.playerRecorder = null;
            EditorUtility.SetDirty(ghosts);
        }

        if (Object.FindFirstObjectByType<RennHud>(FindObjectsInactive.Include) == null)
        {
            new GameObject("RennHud").AddComponent<RennHud>();
        }

        EditorSceneManager.MarkSceneDirty(szene);
        EditorSceneManager.SaveScene(szene);

        // Erst nach dem Speichern hat das Szenenobjekt eine stabile ID.
        NetzwerkIdAktualisieren(leitungObjekt.GetComponent<NetworkObject>());
        EditorSceneManager.MarkSceneDirty(szene);
        EditorSceneManager.SaveScene(szene);
    }

    private static void BuildSettingsSetzen()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(MenuePfad, true),
            new EditorBuildSettingsScene(RennPfad, true),
        };
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

    private static TMP_FontAsset SchriftFinden(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name + " t:TMP_FontAsset"))
        {
            var schrift = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (schrift != null) return schrift;
        }
        return null;
    }

    private static RectTransform Panel(Transform eltern, string name)
    {
        var objekt = new GameObject(name, typeof(RectTransform));
        objekt.layer = eltern.gameObject.layer;
        objekt.transform.SetParent(eltern, false);
        var rect = objekt.GetComponent<RectTransform>();
        Strecken(rect);
        return rect;
    }

    private static Image Bild(Transform eltern, string name, Color farbe)
    {
        var objekt = new GameObject(name, typeof(RectTransform));
        objekt.layer = eltern.gameObject.layer;
        objekt.transform.SetParent(eltern, false);
        var bild = objekt.AddComponent<Image>();
        bild.color = farbe;
        bild.raycastTarget = false;
        return bild;
    }

    private static TextMeshProUGUI Text(Transform eltern, string name, string inhalt, float groesse, TMP_FontAsset schrift, Color farbe)
    {
        var objekt = new GameObject(name, typeof(RectTransform));
        objekt.layer = eltern.gameObject.layer;
        objekt.transform.SetParent(eltern, false);
        var text = objekt.AddComponent<TextMeshProUGUI>();
        if (schrift != null) text.font = schrift;
        text.text = inhalt;
        text.fontSize = groesse;
        text.color = farbe;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static Button Knopf(Transform eltern, string name, string beschriftung, Vector2 position, TMP_FontAsset schrift, Vector2? groesse = null)
    {
        var bild = Bild(eltern, name, Akzent);
        bild.raycastTarget = true;
        bild.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bild.type = Image.Type.Sliced;
        Platzieren(bild.rectTransform, new Vector2(0.5f, 0.5f), position, groesse ?? new Vector2(520, 90));

        var knopf = bild.gameObject.AddComponent<Button>();
        knopf.targetGraphic = bild;

        var text = Text(bild.transform, "Text", beschriftung, 40, schrift, Dunkel);
        Strecken(text.rectTransform);
        return knopf;
    }

    private static void Faerben(Button knopf, Color flaeche, Color schrift)
    {
        knopf.GetComponent<Image>().color = flaeche;
        knopf.GetComponentInChildren<TextMeshProUGUI>().color = schrift;
    }

    private static TMP_InputField Eingabefeld(Transform eltern, string name, string platzhalter, Vector2 position, Vector2 groesse)
    {
        GameObject objekt = TMP_DefaultControls.CreateInputField(new TMP_DefaultControls.Resources
        {
            inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd")
        });
        objekt.name = name;
        objekt.transform.SetParent(eltern, false);
        foreach (Transform t in objekt.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = eltern.gameObject.layer;
        Platzieren(objekt.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), position, groesse);

        var feld = objekt.GetComponent<TMP_InputField>();
        feld.characterLimit = 6;
        feld.characterValidation = TMP_InputField.CharacterValidation.Alphanumeric;
        feld.pointSize = 44;
        feld.textComponent.alignment = TextAlignmentOptions.Center;
        feld.textComponent.color = Dunkel;
        if (feld.placeholder is TextMeshProUGUI hinweis)
        {
            hinweis.text = platzhalter;
            hinweis.fontSize = 34;
            hinweis.alignment = TextAlignmentOptions.Center;
            hinweis.fontStyle = FontStyles.Normal;
        }
        return feld;
    }

    private static void Strecken(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Platzieren(RectTransform rect, Vector2 anker, Vector2 position, Vector2 groesse)
    {
        rect.anchorMin = anker;
        rect.anchorMax = anker;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = groesse;
    }
}
