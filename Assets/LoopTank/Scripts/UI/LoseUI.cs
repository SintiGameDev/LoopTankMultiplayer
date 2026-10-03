using Unity.Netcode;
using UnityEngine;

namespace TopDownRace
{
    public class LoseUI : MonoBehaviour
    {
        void Start()
        {
            ErgebnisKnoepfe.NeustartNurFuerHost(transform, "btn-restart");
        }

        public void BtnRestart()
        {
            if (GameControl.m_Current != null) GameControl.m_Current.NeustartAnfordern();
        }

        public void BtnExit()
        {
            NetzwerkSitzung.Verlassen();
        }
    }

    /// <summary>Gemeinsame Hilfe fuer win-ui und lose-ui.</summary>
    public static class ErgebnisKnoepfe
    {
        /// <summary>Eine neue Runde kann nur der Host starten, Clients sehen den Knopf deshalb nicht.</summary>
        public static void NeustartNurFuerHost(Transform wurzel, string knopfName)
        {
            bool istHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            foreach (Transform kind in wurzel.GetComponentsInChildren<Transform>(true))
            {
                if (kind.name == knopfName) kind.gameObject.SetActive(istHost);
            }
        }
    }
}
