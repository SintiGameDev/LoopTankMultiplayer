using UnityEngine;
using UnityEngine.UI;

namespace TopDownRace
{
    /// <summary>Zeigt am Rennende das vorhandene win-ui bzw. lose-ui mit passendem Titel an.</summary>
    public static class ErgebnisUI
    {
        public static void Zeigen(bool gewonnen, string titel, int runden)
        {
            // Das neue HUD bringt seinen eigenen Ergebnis-Bildschirm mit. Die alten win/lose-Fenster
            // bleiben nur als Rueckfall fuer Szenen ohne RennHud.
            if (RennHud.Instanz != null)
            {
                RennHud.Instanz.ErgebnisZeigen(gewonnen, titel, runden);
                return;
            }

            if (UISystem.m_Main == null) return;

            string name = gewonnen ? "win-ui" : "lose-ui";
            if (UISystem.FindOpenUIByName(name) != null) return;

            GameObject ui = UISystem.ShowUI(name);
            if (ui == null) return;

            // Die Prefabs haben feste Texte; der Titel wird ueber seinen Platzhaltertext gefunden.
            string platzhalter = gewonnen ? "Nice Job!" : "Phantom Collision";
            foreach (Text text in ui.GetComponentsInChildren<Text>(true))
            {
                if (text.text == platzhalter)
                {
                    text.text = titel;
                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                }
                else if (text.CompareTag("Score"))
                {
                    text.text = runden.ToString();
                }
            }
        }
    }
}
