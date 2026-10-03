using UnityEngine;

/// <summary>Feste Farbe pro Startplatz, damit man Panzer und Ghosts der Spieler unterscheiden kann.</summary>
public static class SpielerFarben
{
    private static readonly Color[] s_Farben =
    {
        new Color(1.00f, 1.00f, 1.00f),
        new Color(1.00f, 0.50f, 0.45f),
        new Color(0.55f, 1.00f, 0.60f),
        new Color(1.00f, 0.88f, 0.40f),
    };

    private static readonly string[] s_Namen = { "Weiss", "Rot", "Gruen", "Gelb" };

    public static Color Farbe(int slot) => s_Farben[Mathf.Abs(slot) % s_Farben.Length];

    public static string Name(int slot) => "Spieler " + (slot + 1) + " (" + s_Namen[Mathf.Abs(slot) % s_Namen.Length] + ")";

    public static string Hex(int slot) => ColorUtility.ToHtmlStringRGB(Farbe(slot));
}
