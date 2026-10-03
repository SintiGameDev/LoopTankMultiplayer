using TopDownRace;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-Teil des Streckengenerators:
/// - Knoepfe im Inspector der Komponente StreckenGenerator
/// - Menue LoopTank > Strecke generieren (3D): richtet den Generator in Race3D ein, ersetzt die
///   aus der 2D-Szene uebernommene Strecke und erzeugt eine neue.
/// </summary>
[CustomEditor(typeof(StreckenGenerator))]
public class StreckenGeneratorEditor : Editor
{
    private const string Race3DPfad = "Assets/LoopTank/Scenes/Race3D.unity";
    private const string GeneratorName = "StreckenGenerator";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var generator = (StreckenGenerator)target;
        EditorGUILayout.Space();

        using (new EditorGUI.DisabledScope(Application.isPlaying))
        {
            if (GUILayout.Button("Strecke erzeugen", GUILayout.Height(30)))
            {
                Bauen(generator, generator.m_Seed);
            }
            if (GUILayout.Button("Neue zufällige Strecke"))
            {
                Bauen(generator, Random.Range(1, 1000000));
            }
        }

        if (generator.Mittellinie.Count > 0)
        {
            EditorGUILayout.HelpBox(
                generator.IstNotfall
                    ? "Mit diesen Einstellungen passte keine Strecke, es wurde ein flacher Kreis gebaut. Radius vergrößern oder Breite verkleinern."
                    : "Zuletzt gebaut: " + generator.GebauteForm + ", " + generator.BrueckenAnzahl + " Brücke(n), " + generator.AbzweigAnzahl + " Abzweigung(en).",
                generator.IstNotfall ? MessageType.Warning : MessageType.None);
        }

        EditorGUILayout.HelpBox(
            "Gleicher Seed = gleiche Strecke. Ist 'Zur Laufzeit erzeugen' an, baut das Spiel die Strecke beim Rennstart selbst neu "
            + "(mit 'Zufälliger Seed' jedes Mal eine andere); die hier erzeugte Strecke dient dann nur der Vorschau.",
            MessageType.Info);
    }

    private static void Bauen(StreckenGenerator generator, int seed)
    {
        Undo.RecordObject(generator, "Strecke erzeugen");
        generator.Erzeugen(seed);
        EditorUtility.SetDirty(generator);
        if (generator.m_Kontrolle != null) EditorUtility.SetDirty(generator.m_Kontrolle);
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
    }

    [MenuItem("LoopTank/Strecke generieren (3D)")]
    public static void Menue()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("LoopTank", "Bitte zuerst den Play-Modus beenden.", "OK");
            return;
        }
        if (!System.IO.File.Exists(Race3DPfad))
        {
            EditorUtility.DisplayDialog("LoopTank", "Die Szene Race3D gibt es noch nicht. Bitte zuerst 'LoopTank > 3D-Rennszene erzeugen' ausführen.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        try
        {
            string bericht = Einrichten();
            Debug.Log("[StreckenGenerator] " + bericht);
            EditorUtility.DisplayDialog("LoopTank – Strecke generiert", bericht, "OK");
        }
        catch (System.Exception fehler)
        {
            Debug.LogException(fehler);
            EditorUtility.DisplayDialog("LoopTank – Strecke nicht generiert", fehler.Message, "OK");
        }
    }

    /// <summary>Richtet den Generator in Race3D ein (falls noetig) und erzeugt eine Strecke.</summary>
    public static string Einrichten()
    {
        var szene = EditorSceneManager.OpenScene(Race3DPfad, OpenSceneMode.Single);

        // Ohne Rennleitung waere das nicht die Rennszene. Dann lieber nichts anfassen.
        if (Object.FindFirstObjectByType<GameControl>(FindObjectsInactive.Include) == null)
            throw new System.InvalidOperationException("In Race3D fehlt die Rennleitung (GameControl). Die Szene wurde nicht verändert.");

        string bericht = "";
        var generator = Object.FindFirstObjectByType<StreckenGenerator>(FindObjectsInactive.Include);
        if (generator == null)
        {
            // Die bisherige Strecke stammt aus der 2D-Szene und liegt unter dem Objekt "Strecke"
            // (Waende, Checkpoints, Startplaetze, alte Streckenkontrolle). Sie wird ersetzt.
            foreach (GameObject wurzel in szene.GetRootGameObjects())
            {
                if (wurzel.name == "Strecke" && wurzel.GetComponent<StreckenGenerator>() == null)
                {
                    Object.DestroyImmediate(wurzel);
                    bericht += "- Alte, aus der 2D-Szene übernommene Strecke entfernt\n";
                }
            }

            var objekt = new GameObject(GeneratorName);
            var kontrolle = objekt.AddComponent<RaceTrackControl>();
            generator = objekt.AddComponent<StreckenGenerator>();
            generator.m_Kontrolle = kontrolle;
            bericht += "- Objekt '" + GeneratorName + "' mit Generator und Streckenkontrolle angelegt\n";
        }

        MaterialienZuweisen(generator);
        generator.Erzeugen(generator.m_Seed);
        EditorUtility.SetDirty(generator);

        int renderer = generator.GetComponentsInChildren<MeshRenderer>(true).Length;
        int kanister = generator.GetComponentsInChildren<TreibstoffKanister>(true).Length;
        bericht += "- Strecke mit Seed " + generator.m_Seed + " erzeugt: "
            + generator.Mittellinie.Count + " Stücke, "
            + generator.m_Kontrolle.m_Checkpoints.Length + " Tore (Ziellinie + "
            + generator.m_Kontrolle.ZwischenIds.Length + " Checkpoints), "
            + generator.m_Kontrolle.m_StartPositions.Length + " Startplätze, "
            + kanister + " Kanister\n"
            + "- Form: " + generator.GebauteForm + ", " + generator.BrueckenAnzahl + " Brücke(n), " + generator.AbzweigAnzahl + " Abzweigung(en)"
            + (generator.IstNotfall ? " – ACHTUNG: Notfall-Kreis, mit diesen Einstellungen passte keine Strecke" : "") + "\n"
            + "- " + (renderer - kanister) + " Meshes für die ganze Strecke (Wände als ein Mesh mit einem Collider)\n";

        // Kamera ueber die neue Startaufstellung setzen, damit die Szene im Editor sinnvoll aussieht.
        var kamera = Object.FindFirstObjectByType<VerfolgerKamera>(FindObjectsInactive.Include);
        Transform start = generator.m_Kontrolle.m_StartPositions[0];
        if (kamera != null && start != null)
        {
            kamera.transform.position = start.position - start.forward * kamera.m_Abstand + Vector3.up * kamera.m_Hoehe;
            kamera.transform.rotation = Quaternion.LookRotation(start.position + start.forward * kamera.m_Abstand - kamera.transform.position);
        }

        EditorSceneManager.MarkSceneDirty(szene);
        EditorSceneManager.SaveScene(szene);
        AssetDatabase.SaveAssets();

        Selection.activeGameObject = generator.gameObject;
        return "Erledigt:\n\n" + bericht + "\nForm, Breite und Checkpoint-Anzahl stellst du am Objekt '" + GeneratorName
            + "' ein; der Knopf 'Strecke erzeugen' baut neu.";
    }

    /// <summary>Traegt die 3D-Materialien ein, wo noch keines gesetzt ist.</summary>
    private static void MaterialienZuweisen(StreckenGenerator generator)
    {
        Renn3DErzeugen.OrdnerSicherstellen("Assets/LoopTank/Art/Materials3D");

        if (generator.m_BodenMaterial == null) generator.m_BodenMaterial = Renn3DErzeugen.MaterialAnlegen("Boden", new Color(0.36f, 0.33f, 0.27f), false);
        if (generator.m_StrassenMaterial == null) generator.m_StrassenMaterial = Renn3DErzeugen.MaterialAnlegen("Strasse", new Color(0.20f, 0.21f, 0.23f), false);
        if (generator.m_WandMaterial == null) generator.m_WandMaterial = Renn3DErzeugen.MaterialAnlegen("Wand", new Color(0.62f, 0.64f, 0.68f), false);
        if (generator.m_ZielMaterial == null) generator.m_ZielMaterial = Renn3DErzeugen.MaterialAnlegen("Ziellinie", new Color(0.95f, 0.95f, 0.95f), false);
        if (generator.m_CheckpointMaterial == null) generator.m_CheckpointMaterial = Renn3DErzeugen.MaterialAnlegen("Checkpoint", new Color(0.10f, 0.83f, 0.85f, 0.35f), true);
        if (generator.m_StartMaterial == null) generator.m_StartMaterial = Renn3DErzeugen.MaterialAnlegen("Startplatz", new Color(0.30f, 0.32f, 0.36f), false);
        if (generator.m_KanisterMaterial == null) generator.m_KanisterMaterial = Renn3DErzeugen.MaterialAnlegen("Kanister", new Color(0.86f, 0.16f, 0.12f), false);
    }
}
