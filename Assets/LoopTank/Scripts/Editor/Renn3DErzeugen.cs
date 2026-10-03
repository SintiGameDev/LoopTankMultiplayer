using System.Collections.Generic;
using TMPro;
using TopDownRace;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Erzeugt die 3D-Rennszene "Race3D" als Prototyp aus Wuerfeln und anderen Primitives.
/// Die Strecke wird nicht neu erfunden, sondern aus der 2D-Szene "Race" abgelesen:
/// Waende aus den EdgeCollider2D, Checkpoints, Startplaetze und die Schutzzone am Start.
/// Aus der 2D-Ebene XY wird dabei die 3D-Ebene XZ: (x, y) -> (x, 0, y).
///
/// Menue: LoopTank > 3D-Rennszene erzeugen
/// Die Szene und die 3D-Prefabs werden bei jedem Lauf komplett neu gebaut. Die 2D-Szene
/// wird nur gelesen und nicht veraendert. Details: Assets/LoopTank/Docs/3D-Umstellung.md
/// </summary>
public static class Renn3DErzeugen
{
    private const string Race2DPfad = "Assets/LoopTank/Scenes/Race.unity";
    private const string Race3DPfad = "Assets/LoopTank/Scenes/Race3D.unity";
    private const string MenuePfad = "Assets/LoopTank/Scenes/MainMenu.unity";
    private const string Tank2DPfad = "Assets/LoopTank/Prefabs/Gameplay/Tank.prefab";
    private const string PrefabOrdner = "Assets/LoopTank/Prefabs/Gameplay3D";
    private const string Tank3DPfad = PrefabOrdner + "/Tank3D.prefab";
    private const string Ghost3DPfad = PrefabOrdner + "/GhostTank3D.prefab";
    private const string MaterialOrdner = "Assets/LoopTank/Art/Materials3D";
    private const string Renderer3DPfad = "Assets/Settings/Renderer3D.asset";
    private const string PrefabListePfad = "Assets/DefaultNetworkPrefabs.asset";

    private const float WandDicke = 1.5f;

    // ------------------------------------------------------------------
    // Was aus der 2D-Szene abgelesen wird
    // ------------------------------------------------------------------

    private class Kasten { public Vector2 Mitte; public Vector2 Groesse; public float Winkel; public int Id; public bool Ziel; public int Index; }

    private class Vorlage
    {
        public readonly List<List<Vector2>> Waende = new List<List<Vector2>>();
        public readonly List<Kasten> Checkpoints = new List<Kasten>();
        public readonly List<Kasten> Schutzzonen = new List<Kasten>();
        public readonly List<Kasten> Startplaetze = new List<Kasten>();
        public int CheckpointArrayLaenge;
        public int Runden = 99;
        public bool EigeneGhostsToedlich;
    }

    [MenuItem("LoopTank/3D-Rennszene erzeugen")]
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
            List<string> bericht = Erzeugen();
            string text = "Erledigt:\n\n- " + string.Join("\n- ", bericht);
            Debug.Log("[Renn3DErzeugen] " + text);
            EditorUtility.DisplayDialog("LoopTank – 3D-Rennszene", text + "\n\nStart wie immer aus 'MainMenu'. Dort schaltet der Knopf 'Ansicht' zwischen 3D und 2D um.", "OK");
        }
        catch (System.Exception fehler)
        {
            Debug.LogException(fehler);
            EditorUtility.DisplayDialog("LoopTank – 3D-Rennszene abgebrochen", fehler.Message, "OK");
        }
    }

    public static List<string> Erzeugen()
    {
        var bericht = new List<string>();

        GameObject tank2D = AssetDatabase.LoadAssetAtPath<GameObject>(Tank2DPfad);
        if (tank2D == null) throw new System.InvalidOperationException("Tank.prefab nicht gefunden: " + Tank2DPfad);

        OrdnerSicherstellen(PrefabOrdner);
        OrdnerSicherstellen(MaterialOrdner);
        OrdnerSicherstellen("Assets/LoopTank/Docs");

        // --- 2D-Szene oeffnen und ablesen ---
        Scene szene2D = EditorSceneManager.OpenScene(Race2DPfad, OpenSceneMode.Single);
        var strecke2D = Object.FindFirstObjectByType<RaceTrackControl>(FindObjectsInactive.Include);
        var ui2D = Object.FindFirstObjectByType<UISystem>(FindObjectsInactive.Include);
        var ghosts2D = Object.FindFirstObjectByType<GhostManager>(FindObjectsInactive.Include);
        var fuel2D = Object.FindFirstObjectByType<FuelMechanic>(FindObjectsInactive.Include);
        if (strecke2D == null || ui2D == null || ghosts2D == null || Object.FindFirstObjectByType<Timer>(FindObjectsInactive.Include) == null)
            throw new System.InvalidOperationException("Race.unity ist unvollständig (Strecke, UI, GhostManager oder Timer fehlen). Es wurde nichts erzeugt.");

        Vorlage vorlage = Ablesen(strecke2D);
        if (vorlage.Startplaetze.Count == 0) throw new System.InvalidOperationException("In Race.unity sind keine Startpositionen eingetragen.");
        if (vorlage.Checkpoints.Count == 0) throw new System.InvalidOperationException("In Race.unity sind keine Checkpoints eingetragen.");

        // --- Assets: Materialien, Renderer, Prefabs ---
        Material matBoden = MaterialAnlegen("Boden", new Color(0.36f, 0.33f, 0.27f), false);
        Material matWand = MaterialAnlegen("Wand", new Color(0.62f, 0.64f, 0.68f), false);
        Material matPanzer = MaterialAnlegen("Panzer", Color.white, false);
        Material matGhost = MaterialAnlegen("Ghost", new Color(1f, 1f, 1f, 0.75f), true);
        Material matZiel = MaterialAnlegen("Ziellinie", new Color(0.95f, 0.95f, 0.95f), false);
        Material matCheckpoint = MaterialAnlegen("Checkpoint", new Color(0.10f, 0.83f, 0.85f, 0.35f), true);
        Material matStart = MaterialAnlegen("Startplatz", new Color(0.20f, 0.22f, 0.26f), false);

        int rendererIndex = Renderer3DSicherstellen(bericht);

        Vector3 panzerMasse = PanzerMasseLesen(tank2D, out Vector3 panzerMitte);   // (Breite, Hoehe, Laenge)
        GameObject tank3D = PanzerPrefabBauen(tank2D, panzerMasse, panzerMitte, matPanzer);
        GameObject ghost3D = GhostPrefabBauen(panzerMasse, panzerMitte, matGhost);
        PrefabListeErgaenzen(tank3D);
        bericht.Add("Prefabs Tank3D und GhostTank3D gebaut (" + panzerMasse.x.ToString("F1") + " breit, " + panzerMasse.z.ToString("F1") + " lang, Werte vom 2D-Panzer übernommen)");

        // --- Neue Szene neben der 2D-Szene anlegen und UI/Manager hinueberkopieren ---
        Scene szene3D = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        GameObject ui = Kopieren(ui2D.transform.root.gameObject, szene3D);
        GameObject ghostObjekt = Kopieren(ghosts2D.gameObject, szene3D);
        GameObject fuelObjekt = fuel2D != null ? Kopieren(fuel2D.gameObject, szene3D) : null;

        SceneManager.SetActiveScene(szene3D);
        EditorSceneManager.CloseScene(szene2D, true);

        // --- Szene aufbauen ---
        Licht();
        Transform streckenWurzel = new GameObject("Strecke").transform;
        int wandTeile = StreckeBauen(vorlage, streckenWurzel, matBoden, matWand, panzerMasse.x * 0.6f);
        bericht.Add("Strecke aus der 2D-Szene nachgebaut: " + wandTeile + " Wandstücke, " + vorlage.Checkpoints.Count + " Checkpoints, " + vorlage.Startplaetze.Count + " Startplätze");

        RaceTrackControl strecke = CheckpointsUndStartBauen(vorlage, streckenWurzel, matZiel, matCheckpoint, matStart, panzerMasse);

        VerfolgerKamera kamera = KameraBauen(vorlage, panzerMasse, rendererIndex);

        // Rennleitung
        var leitungObjekt = new GameObject("GameControl");
        leitungObjekt.AddComponent<NetworkObject>();
        var leitung = leitungObjekt.AddComponent<GameControl>();
        leitung.m_levelRounds = vorlage.Runden;
        leitung.m_EigeneGhostsToedlich = vorlage.EigeneGhostsToedlich;
        leitung.m_PlayerCarPrefab = tank3D;
        leitung.m_CameraFollow = kamera;

        // GhostManager und Fuel auf die kopierte UI und das 3D-Ghost-Prefab umhaengen
        var ghosts = ghostObjekt.GetComponent<GhostManager>();
        ghosts.ghostPrefab = ghost3D;
        ghosts.playerRecorder = null;
        ghosts.ghostParent = null;
        ghosts.roundTimer = null;
        ghosts.lapStatusUIBar = null;
        ghosts.lapStatusText = TextFinden(ui, "LapCounter");
        ghosts.lastLapTimeText = TextFinden(ui, "LapCounterSubtext");
        ghostObjekt.transform.position = Vector3.zero;

        if (fuelObjekt != null)
        {
            var fuelSo = new SerializedObject(fuelObjekt.GetComponent<FuelMechanic>());
            fuelSo.FindProperty("m_FuelTextUI").objectReferenceValue = TextFinden(ui, "FuelCounter");
            fuelSo.ApplyModifiedPropertiesWithoutUndo();
        }

        new GameObject("RennHud").AddComponent<RennHud>();
        bericht.Add("UI, Timer, GhostManager und Fuel aus der 2D-Szene übernommen; Third-Person-Kamera und Rennleitung angelegt");

        // --- Speichern, Build Settings, Netzwerk-ID ---
        EditorSceneManager.SaveScene(szene3D, Race3DPfad);
        if (BuildSettingsErgaenzen()) bericht.Add("Race3D in die Build Settings aufgenommen");
        if (RennSzeneUmstellen.NetzwerkIdAktualisieren(leitungObjekt.GetComponent<NetworkObject>()))
        {
            EditorSceneManager.MarkSceneDirty(szene3D);
            EditorSceneManager.SaveScene(szene3D);
        }
        AssetDatabase.SaveAssets();

        if (MenueKnopfErgaenzen()) bericht.Add("Menü: Knopf 'Ansicht: 3D / 2D' ergänzt");

        EditorSceneManager.OpenScene(Race3DPfad, OpenSceneMode.Single);
        return bericht;
    }

    // ------------------------------------------------------------------
    // 2D-Szene ablesen
    // ------------------------------------------------------------------

    private static Vorlage Ablesen(RaceTrackControl strecke)
    {
        var v = new Vorlage();

        // Waende: alle festen EdgeCollider2D der Szene
        foreach (var kante in Object.FindObjectsByType<EdgeCollider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (kante.isTrigger || kante.GetComponentInParent<Rigidbody2D>() != null) continue;
            var punkte = new List<Vector2>();
            foreach (Vector2 p in kante.points)
                punkte.Add(kante.transform.TransformPoint(p + kante.offset));
            punkte = Vereinfachen(punkte, 0.15f);
            if (punkte.Count >= 2) v.Waende.Add(punkte);
        }

        // Checkpoints, mit ihrem Platz im Array der Streckenkontrolle
        v.CheckpointArrayLaenge = strecke.m_Checkpoints != null ? strecke.m_Checkpoints.Length : 0;
        for (int i = 0; i < v.CheckpointArrayLaenge; i++)
        {
            Checkpoint c = strecke.m_Checkpoints[i];
            if (c == null) continue;
            Kasten k = KastenLesen(c.GetComponent<BoxCollider2D>(), c.transform);
            k.Id = c.m_ID;
            k.Ziel = c.isFinishLine || c.m_ID == 0;
            k.Index = i;
            v.Checkpoints.Add(k);
        }

        if (strecke.m_StartPositions != null)
        {
            foreach (Transform platz in strecke.m_StartPositions)
            {
                if (platz == null) continue;
                v.Startplaetze.Add(new Kasten { Mitte = platz.position, Winkel = platz.eulerAngles.z });
            }
        }

        // Schutzzonen: Trigger mit dem Tag "CollisionIgnorer", in denen Ghosts nicht toeten
        foreach (var box in Object.FindObjectsByType<BoxCollider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (box.isTrigger && box.CompareTag("CollisionIgnorer")) v.Schutzzonen.Add(KastenLesen(box, box.transform));
        }

        var leitung = Object.FindFirstObjectByType<GameControl>(FindObjectsInactive.Include);
        if (leitung != null)
        {
            v.Runden = leitung.m_levelRounds;
            v.EigeneGhostsToedlich = leitung.m_EigeneGhostsToedlich;
        }
        return v;
    }

    private static Kasten KastenLesen(BoxCollider2D box, Transform t)
    {
        Vector2 groesse = box != null ? box.size : Vector2.one;
        Vector2 versatz = box != null ? box.offset : Vector2.zero;
        Vector3 skala = t.lossyScale;
        return new Kasten
        {
            Mitte = t.TransformPoint(versatz),
            Groesse = new Vector2(Mathf.Abs(groesse.x * skala.x), Mathf.Abs(groesse.y * skala.y)),
            Winkel = t.eulerAngles.z,
        };
    }

    /// <summary>Entfernt Punkte, die fast genau auf der Linie zwischen ihren Nachbarn liegen.</summary>
    private static List<Vector2> Vereinfachen(List<Vector2> punkte, float toleranz)
    {
        if (punkte.Count < 3) return punkte;
        var ergebnis = new List<Vector2> { punkte[0] };
        for (int i = 1; i < punkte.Count - 1; i++)
        {
            Vector2 a = ergebnis[ergebnis.Count - 1];
            Vector2 b = punkte[i + 1];
            Vector2 ab = b - a;
            float abstand = ab.sqrMagnitude < 0.0001f
                ? Vector2.Distance(punkte[i], a)
                : Mathf.Abs(ab.x * (punkte[i].y - a.y) - ab.y * (punkte[i].x - a.x)) / ab.magnitude;
            if (abstand > toleranz) ergebnis.Add(punkte[i]);
        }
        ergebnis.Add(punkte[punkte.Count - 1]);
        return ergebnis;
    }

    // ------------------------------------------------------------------
    // Umrechnung 2D -> 3D
    // ------------------------------------------------------------------

    /// <summary>Punkt der 2D-Ebene (x, y) auf der 3D-Ebene (x, hoehe, z = y).</summary>
    private static Vector3 Welt(Vector2 p, float hoehe = 0f) => new Vector3(p.x, hoehe, p.y);

    /// <summary>Drehung fuer Objekte, deren lokale X-Achse der 2D-X-Achse entspricht (Kaesten, Waende).</summary>
    private static Quaternion KastenDrehung(float winkel2D) => Quaternion.Euler(0f, -winkel2D, 0f);

    /// <summary>Drehung fuer Panzer: in 2D ist "vorwaerts" die lokale X-Achse, in 3D die lokale Z-Achse.</summary>
    private static Quaternion PanzerDrehung(float winkel2D) => Quaternion.Euler(0f, 90f - winkel2D, 0f);

    // ------------------------------------------------------------------
    // Assets
    // ------------------------------------------------------------------

    internal static void OrdnerSicherstellen(string pfad)
    {
        if (AssetDatabase.IsValidFolder(pfad)) return;
        string eltern = System.IO.Path.GetDirectoryName(pfad).Replace('\\', '/');
        OrdnerSicherstellen(eltern);
        AssetDatabase.CreateFolder(eltern, System.IO.Path.GetFileName(pfad));
    }

    /// <summary>True, wenn das Projekt mit URP rendert. Ohne eingetragenes Pipeline-Asset laeuft die Built-in-Pipeline.</summary>
    private static bool NutztUrp => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;

    internal static Material MaterialAnlegen(string name, Color farbe, bool durchsichtig)
    {
        string pfad = MaterialOrdner + "/" + name + ".mat";
        bool urp = NutztUrp;
        string shaderName = urp ? "Universal Render Pipeline/Lit" : "Standard";
        Shader shader = Shader.Find(shaderName);
        if (shader == null) throw new System.InvalidOperationException("Shader '" + shaderName + "' nicht gefunden.");

        Material material = AssetDatabase.LoadAssetAtPath<Material>(pfad);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, pfad);
        }
        material.shader = shader;
        material.color = farbe;
        material.SetFloat(urp ? "_Smoothness" : "_Glossiness", 0.15f);
        material.enableInstancing = true;   // gleiche Meshes mit gleichem Material werden in einem Batch gezeichnet

        if (durchsichtig)
        {
            if (urp)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            else
            {
                material.SetFloat("_Mode", 2f);   // Standard-Shader: "Fade"
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    /// <summary>
    /// Nur fuer URP noetig: Der 2D-Renderer von URP beleuchtet 3D-Objekte nicht und sortiert sie
    /// nicht nach Tiefe, die 3D-Kamera braucht dann einen zweiten, normalen Renderer.
    /// Mit der Built-in-Pipeline (aktueller Stand des Projekts) ist nichts zu tun; Rueckgabe -1.
    /// </summary>
    private static int Renderer3DSicherstellen(List<string> bericht)
    {
        var urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (urp == null) return -1;

        var so = new SerializedObject(urp);
        SerializedProperty liste = so.FindProperty("m_RendererDataList");
        for (int i = 0; i < liste.arraySize; i++)
        {
            if (liste.GetArrayElementAtIndex(i).objectReferenceValue is UniversalRendererData) return i;
        }

        var daten = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Renderer3DPfad);
        if (daten == null)
        {
            daten = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(daten, Renderer3DPfad);
        }

        liste.arraySize++;
        liste.GetArrayElementAtIndex(liste.arraySize - 1).objectReferenceValue = daten;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(urp);
        bericht.Add("3D-Renderer (Renderer3D.asset) angelegt und im URP-Asset als zweiter Renderer eingetragen");
        return liste.arraySize - 1;
    }

    /// <summary>Liest Laenge und Breite des 2D-Panzers aus seinem Collider. Ergebnis: (Breite, Hoehe, Laenge).</summary>
    private static Vector3 PanzerMasseLesen(GameObject tank2D, out Vector3 mitte)
    {
        float laenge = 10f, breite = 6f;
        mitte = Vector3.zero;

        var collider = tank2D.GetComponent<Collider2D>();
        Vector3 skala = tank2D.transform.localScale;
        if (collider is CapsuleCollider2D kapsel)
        {
            laenge = Mathf.Abs(kapsel.size.x * skala.x);
            breite = Mathf.Abs(kapsel.size.y * skala.y);
            // 2D lokal: x = vorwaerts, y = links. 3D lokal: z = vorwaerts, x = rechts.
            mitte = new Vector3(-kapsel.offset.y * skala.y, 0f, kapsel.offset.x * skala.x);
        }
        else if (collider is BoxCollider2D box)
        {
            laenge = Mathf.Abs(box.size.x * skala.x);
            breite = Mathf.Abs(box.size.y * skala.y);
            mitte = new Vector3(-box.offset.y * skala.y, 0f, box.offset.x * skala.x);
        }

        float hoehe = breite * 0.45f;
        mitte.y = hoehe * 0.5f;
        return new Vector3(breite, hoehe, laenge);
    }

    private static GameObject Wuerfel(string name, Transform eltern, Vector3 lokalePosition, Vector3 groesse, Material material, string tag = null)
    {
        GameObject w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Object.DestroyImmediate(w.GetComponent<Collider>());   // Kollision macht jeweils ein eigener, passender Collider
        w.name = name;
        w.transform.SetParent(eltern, false);
        w.transform.localPosition = lokalePosition;
        w.transform.localScale = groesse;
        w.GetComponent<MeshRenderer>().sharedMaterial = material;
        if (tag != null) w.tag = tag;
        return w;
    }

    /// <summary>Wanne, drehbarer Turm und Rohr aus Wuerfeln. Tags wie beim 2D-Panzer, damit die Faerbung greift.</summary>
    private static void PanzerOptikBauen(Transform wurzel, Vector3 masse, Vector3 mitte, Material material)
    {
        Wuerfel("TankBody", wurzel, mitte, masse, material, "TankBody");

        // PlayerCar sucht ein Kind mit genau diesem Namen und dreht es mit den Pfeiltasten.
        var turm = new GameObject("TankTop");
        turm.tag = "TankTop";
        turm.transform.SetParent(wurzel, false);
        turm.transform.localPosition = new Vector3(mitte.x, masse.y, mitte.z - masse.z * 0.05f);

        float turmHoehe = masse.y * 0.6f;
        Wuerfel("Turm", turm.transform, new Vector3(0, turmHoehe * 0.5f, 0), new Vector3(masse.x * 0.6f, turmHoehe, masse.z * 0.42f), material, "TankTop");
        float rohr = masse.x * 0.14f;
        Wuerfel("Rohr", turm.transform, new Vector3(0, turmHoehe * 0.55f, masse.z * 0.45f), new Vector3(rohr, rohr, masse.z * 0.55f), material, "TankTop");
    }

    private static GameObject PanzerPrefabBauen(GameObject tank2D, Vector3 masse, Vector3 mitte, Material material)
    {
        var wurzel = new GameObject("Tank3D");
        wurzel.tag = "Player";

        // Physik: dieselben Werte wie der 2D-Panzer, festgehalten auf der Ebene XZ.
        var body2D = tank2D.GetComponent<Rigidbody2D>();
        var body = wurzel.AddComponent<Rigidbody>();
        body.mass = body2D != null ? body2D.mass : 250f;
        body.linearDamping = body2D != null ? body2D.linearDamping : 1f;
        body.angularDamping = body2D != null ? body2D.angularDamping : 20f;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        var box = wurzel.AddComponent<BoxCollider>();
        box.center = mitte;
        box.size = masse;

        // Netzwerk: wie beim 2D-Panzer bewegt jeder Client seinen eigenen Panzer.
        wurzel.AddComponent<NetworkObject>();
        var nt = wurzel.AddComponent<NetworkTransform>();
        nt.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
        nt.SyncPositionX = true;
        nt.SyncPositionY = true;    // erzeugte Strecken haben Hoehenunterschiede
        nt.SyncPositionZ = true;
        nt.SyncRotAngleX = false;
        nt.SyncRotAngleY = true;
        nt.SyncRotAngleZ = false;
        nt.SyncScaleX = false;
        nt.SyncScaleY = false;
        nt.SyncScaleZ = false;
        nt.Interpolate = true;
        wurzel.AddComponent<NetworkRigidbody>();

        // Spiel-Scripts: Sounds und Einstellungen vom 2D-Panzer uebernehmen.
        wurzel.AddComponent<AudioSource>().playOnAwake = false;
        var panzer = wurzel.AddComponent<PlayerCar>();
        var panzer2D = tank2D.GetComponent<PlayerCar>();
        if (panzer2D != null) EditorUtility.CopySerialized(panzer2D, panzer);

        var physik = wurzel.AddComponent<CarPhysics3D>();
        var physik2D = tank2D.GetComponent<CarPhysics>();
        if (physik2D != null) physik.m_SpeedForce = physik2D.m_SpeedForce;

        wurzel.AddComponent<LapRecorder>();
        wurzel.AddComponent<PanzerTurm3D>();   // Zielen mit der Kamera, Gewicht von Turm und Kanone

        PanzerOptikBauen(wurzel.transform, masse, mitte, material);

        PrefabUtility.SaveAsPrefabAsset(wurzel, Tank3DPfad, out bool gespeichert);
        Object.DestroyImmediate(wurzel);
        if (!gespeichert) throw new System.InvalidOperationException("Tank3D.prefab konnte nicht gespeichert werden (siehe Konsole).");

        AssetDatabase.ImportAsset(Tank3DPfad);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Tank3DPfad);
        RennSzeneUmstellen.NetzwerkIdAktualisieren(prefab.GetComponent<NetworkObject>());
        AssetDatabase.SaveAssets();
        return prefab;
    }

    private static GameObject GhostPrefabBauen(Vector3 masse, Vector3 mitte, Material material)
    {
        var wurzel = new GameObject("GhostTank3D");
        wurzel.tag = "Ghost";

        // Kinematisch: der Ghost wird nur vom Replay bewegt. Trigger: man faehrt hinein statt abzuprallen.
        var body = wurzel.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var box = wurzel.AddComponent<BoxCollider>();
        box.center = mitte;
        box.size = masse;
        box.isTrigger = true;

        wurzel.AddComponent<GhostReplay>();
        PanzerOptikBauen(wurzel.transform, masse, mitte, material);

        // Solange dieses Kind aktiv ist (erste Sekunde nach dem Erscheinen), ist der Ghost harmlos.
        var schutz = new GameObject("CollisionIgnorer");
        schutz.tag = "CollisionIgnorer";
        schutz.transform.SetParent(wurzel.transform, false);

        PrefabUtility.SaveAsPrefabAsset(wurzel, Ghost3DPfad, out bool gespeichert);
        Object.DestroyImmediate(wurzel);
        if (!gespeichert) throw new System.InvalidOperationException("GhostTank3D.prefab konnte nicht gespeichert werden (siehe Konsole).");
        return AssetDatabase.LoadAssetAtPath<GameObject>(Ghost3DPfad);
    }

    private static void PrefabListeErgaenzen(GameObject tank3D)
    {
        var liste = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(PrefabListePfad);
        if (liste == null) throw new System.InvalidOperationException("Netzwerk-Prefab-Liste fehlt. Bitte zuerst 'LoopTank > Multiplayer einrichten' ausführen.");
        if (!liste.Contains(tank3D))
        {
            liste.Add(new NetworkPrefab { Prefab = tank3D });
            EditorUtility.SetDirty(liste);
        }
    }

    // ------------------------------------------------------------------
    // Szene
    // ------------------------------------------------------------------

    private static GameObject Kopieren(GameObject original, Scene ziel)
    {
        GameObject kopie = Object.Instantiate(original);
        kopie.name = original.name;
        kopie.transform.SetParent(null, true);
        SceneManager.MoveGameObjectToScene(kopie, ziel);
        return kopie;
    }

    private static TMP_Text TextFinden(GameObject wurzel, string name)
    {
        foreach (var text in wurzel.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.name == name) return text;
        }
        Debug.LogWarning("[Renn3DErzeugen] UI-Text '" + name + "' nicht gefunden.");
        return null;
    }

    private static void Licht()
    {
        var licht = new GameObject("Directional Light").AddComponent<Light>();
        licht.type = LightType.Directional;
        licht.intensity = 1.1f;
        licht.shadows = LightShadows.Soft;
        licht.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        // Gleichmaessiges Grundlicht, damit ohne gebackene Beleuchtung nichts schwarz ist.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);
    }

    private static int StreckeBauen(Vorlage v, Transform wurzel, Material matBoden, Material matWand, float wandHoehe)
    {
        // Ausdehnung der Strecke
        var grenzen = new Bounds(Welt(v.Startplaetze[0].Mitte), Vector3.zero);
        foreach (var wand in v.Waende) foreach (var p in wand) grenzen.Encapsulate(Welt(p));
        foreach (var k in v.Checkpoints) grenzen.Encapsulate(Welt(k.Mitte));
        foreach (var k in v.Startplaetze) grenzen.Encapsulate(Welt(k.Mitte));

        // Boden ohne Collider: die Panzer sind auf ihrer Hoehe festgehalten und wuerden sonst am Boden reiben.
        GameObject boden = Wuerfel("Boden", wurzel, new Vector3(grenzen.center.x, -0.5f, grenzen.center.z),
            new Vector3(grenzen.size.x + 60f, 1f, grenzen.size.z + 60f), matBoden);
        GameObjectUtility.SetStaticEditorFlags(boden, StaticEditorFlags.BatchingStatic);

        Transform waende = new GameObject("Waende").transform;
        waende.SetParent(wurzel, false);
        int anzahl = 0;
        foreach (var wand in v.Waende)
        {
            for (int i = 0; i < wand.Count - 1; i++)
            {
                Vector2 a = wand[i], b = wand[i + 1];
                Vector2 d = b - a;
                if (d.magnitude < 0.05f) continue;

                GameObject stueck = GameObject.CreatePrimitive(PrimitiveType.Cube);   // behaelt seinen BoxCollider
                stueck.name = "Wand";
                stueck.transform.SetParent(waende, false);
                stueck.transform.position = Welt((a + b) * 0.5f, wandHoehe * 0.5f);
                stueck.transform.rotation = KastenDrehung(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
                stueck.transform.localScale = new Vector3(d.magnitude + WandDicke, wandHoehe, WandDicke);
                stueck.GetComponent<MeshRenderer>().sharedMaterial = matWand;
                GameObjectUtility.SetStaticEditorFlags(stueck, StaticEditorFlags.BatchingStatic);
                anzahl++;
            }
        }
        return anzahl;
    }

    private static RaceTrackControl CheckpointsUndStartBauen(Vorlage v, Transform wurzel, Material matZiel, Material matCheckpoint, Material matStart, Vector3 panzerMasse)
    {
        float triggerHoehe = panzerMasse.y * 4f;

        Transform eltern = new GameObject("Checkpoints").transform;
        eltern.SetParent(wurzel, false);
        var array = new Checkpoint[v.CheckpointArrayLaenge];
        foreach (Kasten k in v.Checkpoints)
        {
            var objekt = new GameObject(k.Ziel ? "Ziellinie" : "Checkpoint " + k.Id);
            objekt.transform.SetParent(eltern, false);
            objekt.transform.SetPositionAndRotation(Welt(k.Mitte), KastenDrehung(k.Winkel));

            var box = objekt.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, triggerHoehe * 0.5f, 0);
            box.size = new Vector3(k.Groesse.x, triggerHoehe, k.Groesse.y);

            var checkpoint = objekt.AddComponent<Checkpoint>();
            checkpoint.m_ID = k.Id;
            checkpoint.isFinishLine = k.Ziel;
            array[k.Index] = checkpoint;

            // Sichtbare Markierung flach auf dem Boden
            Wuerfel("Markierung", objekt.transform, new Vector3(0, 0.06f, 0), new Vector3(k.Groesse.x, 0.1f, k.Groesse.y), k.Ziel ? matZiel : matCheckpoint);
        }

        foreach (Kasten k in v.Schutzzonen)
        {
            var zone = new GameObject("CollisionIgnorer");
            zone.tag = "CollisionIgnorer";
            zone.transform.SetParent(wurzel, false);
            zone.transform.SetPositionAndRotation(Welt(k.Mitte), KastenDrehung(k.Winkel));
            var box = zone.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0, triggerHoehe * 0.5f, 0);
            box.size = new Vector3(k.Groesse.x, triggerHoehe, k.Groesse.y);
        }

        var kontrolleObjekt = new GameObject("track-control");
        kontrolleObjekt.transform.SetParent(wurzel, false);
        var kontrolle = kontrolleObjekt.AddComponent<RaceTrackControl>();
        kontrolle.m_Checkpoints = array;
        kontrolle.m_StartPositions = new Transform[v.Startplaetze.Count];
        for (int i = 0; i < v.Startplaetze.Count; i++)
        {
            var platz = new GameObject("StartPosition " + (i + 1));
            platz.transform.SetParent(kontrolleObjekt.transform, false);
            platz.transform.SetPositionAndRotation(Welt(v.Startplaetze[i].Mitte), PanzerDrehung(v.Startplaetze[i].Winkel));
            Wuerfel("Markierung", platz.transform, new Vector3(0, 0.05f, 0), new Vector3(panzerMasse.x * 1.3f, 0.08f, panzerMasse.z * 1.2f), matStart);
            kontrolle.m_StartPositions[i] = platz.transform;
        }
        return kontrolle;
    }

    private static VerfolgerKamera KameraBauen(Vorlage v, Vector3 panzerMasse, int rendererIndex)
    {
        var objekt = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        var kamera = objekt.GetComponent<Camera>();
        kamera.orthographic = false;
        kamera.fieldOfView = 60f;
        kamera.nearClipPlane = 0.3f;
        kamera.farClipPlane = 2000f;
        kamera.depth = -1;
        kamera.clearFlags = CameraClearFlags.SolidColor;
        kamera.backgroundColor = new Color(0.50f, 0.66f, 0.82f);
        kamera.cullingMask = ~(1 << LayerMask.NameToLayer("UI"));   // die UI zeichnet ihre eigene Kamera
        if (rendererIndex >= 0) kamera.GetUniversalAdditionalCameraData().SetRenderer(rendererIndex);

        objekt.AddComponent<TempoEffekt>();   // Unschaerfe, Streifen und Vignette bei hohem Tempo
        var verfolger = objekt.AddComponent<VerfolgerKamera>();
        verfolger.m_Abstand = panzerMasse.z * 2.2f;
        verfolger.m_Hoehe = panzerMasse.z * 1.1f;

        // Bis die Panzer gespawnt sind: Blick ueber die Startaufstellung
        Kasten start = v.Startplaetze[0];
        Quaternion richtung = PanzerDrehung(start.Winkel);
        objekt.transform.position = Welt(start.Mitte) - richtung * Vector3.forward * verfolger.m_Abstand + Vector3.up * verfolger.m_Hoehe;
        objekt.transform.rotation = Quaternion.LookRotation(Welt(start.Mitte) + richtung * Vector3.forward * verfolger.m_Abstand - objekt.transform.position);
        return verfolger;
    }

    // ------------------------------------------------------------------
    // Build Settings und Menue
    // ------------------------------------------------------------------

    private static bool BuildSettingsErgaenzen()
    {
        var szenen = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        foreach (var s in szenen)
        {
            if (s.path == Race3DPfad)
            {
                if (s.enabled) return false;
                s.enabled = true;
                EditorBuildSettings.scenes = szenen.ToArray();
                return true;
            }
        }
        szenen.Add(new EditorBuildSettingsScene(Race3DPfad, true));
        EditorBuildSettings.scenes = szenen.ToArray();
        return true;
    }

    /// <summary>Ergaenzt im Menue den Umschalter zwischen 3D- und 2D-Rennszene, falls er noch fehlt.</summary>
    private static bool MenueKnopfErgaenzen()
    {
        if (!System.IO.File.Exists(MenuePfad)) return false;

        Scene menue = EditorSceneManager.OpenScene(MenuePfad, OpenSceneMode.Single);
        var lobby = Object.FindFirstObjectByType<LobbyManager>(FindObjectsInactive.Include);
        if (lobby == null) return false;

        var so = new SerializedObject(lobby);
        SerializedProperty feld = so.FindProperty("buttonStrecke");
        if (feld == null || feld.objectReferenceValue != null) return false;

        var vorbild = so.FindProperty("buttonBeenden").objectReferenceValue as Button;
        if (vorbild == null) return false;

        // "Beenden" wird schmaler und rueckt nach links, der neue Knopf kommt daneben.
        var vorbildRect = vorbild.GetComponent<RectTransform>();
        float zeile = vorbildRect.anchoredPosition.y;
        vorbildRect.anchoredPosition = new Vector2(-135, zeile);
        vorbildRect.sizeDelta = new Vector2(250, 90);

        GameObject kopie = Object.Instantiate(vorbild.gameObject, vorbild.transform.parent);
        kopie.name = "ButtonStrecke";
        kopie.GetComponent<RectTransform>().anchoredPosition = new Vector2(135, zeile);
        var beschriftung = kopie.GetComponentInChildren<TextMeshProUGUI>();
        if (beschriftung != null)
        {
            beschriftung.text = "Ansicht: 3D";
            beschriftung.fontSize = 32;
        }

        feld.objectReferenceValue = kopie.GetComponent<Button>();
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(menue);
        EditorSceneManager.SaveScene(menue);
        return true;
    }
}
