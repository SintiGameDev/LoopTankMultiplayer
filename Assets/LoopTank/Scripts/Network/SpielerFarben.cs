using UnityEngine;

/// <summary>
/// Die Farben, aus denen Spieler in der Lobby waehlen koennen. Panzer und Ghosts eines Spielers
/// tragen seine Farbe. Uebertragen wird immer nur die Nummer der Farbe.
/// </summary>
public static class SpielerFarben
{
    private static readonly Color[] s_Farben =
    {
        new Color(1.00f, 1.00f, 1.00f),   // Weiss
        new Color(1.00f, 0.42f, 0.38f),   // Rot
        new Color(0.50f, 1.00f, 0.56f),   // Gruen
        new Color(1.00f, 0.86f, 0.36f),   // Gelb
        new Color(0.42f, 0.66f, 1.00f),   // Blau
        new Color(1.00f, 0.62f, 0.26f),   // Orange
        new Color(0.80f, 0.52f, 1.00f),   // Violett
        new Color(1.00f, 0.54f, 0.82f),   // Pink
    };

    public static int Anzahl => s_Farben.Length;

    public static Color Farbe(int index) => s_Farben[((index % s_Farben.Length) + s_Farben.Length) % s_Farben.Length];

    public static string Hex(int index) => ColorUtility.ToHtmlStringRGB(Farbe(index));

    /// <summary>Name fuer einen Spieler, der (noch) keinen eigenen gewaehlt hat.</summary>
    public static string StandardName(int slot) => "Spieler " + (slot + 1);

    /// <summary>
    /// Faerbt alle Teile eines Panzers oder Ghosts, die mit "TankBody" oder "TankTop" getaggt sind.
    /// Funktioniert fuer Sprites (2D) und fuer Meshes (3D).
    /// </summary>
    /// <param name="deckkraftSetzen">True: Alpha aus der Farbe uebernehmen. False: vorhandenes Alpha behalten.</param>
    public static void Einfaerben(GameObject wurzel, Color farbe, bool deckkraftSetzen)
    {
        foreach (var renderer in wurzel.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer.CompareTag("TankBody") && !renderer.CompareTag("TankTop")) continue;

            if (renderer is SpriteRenderer sprite)
            {
                sprite.color = new Color(farbe.r, farbe.g, farbe.b, deckkraftSetzen ? farbe.a : sprite.color.a);
            }
            else if (renderer is MeshRenderer)
            {
                Material material = renderer.material;   // eigene Instanz pro Objekt
                float alpha = deckkraftSetzen ? farbe.a : material.color.a;
                material.color = new Color(farbe.r, farbe.g, farbe.b, alpha);
            }
        }
    }
}
