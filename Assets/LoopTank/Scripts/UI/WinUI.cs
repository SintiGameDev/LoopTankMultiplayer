using UnityEngine;

namespace TopDownRace
{
    public class WinUI : MonoBehaviour
    {
        public static WinUI m_Current;

        private void Awake()
        {
            m_Current = this;
        }

        void Start()
        {
            ErgebnisKnoepfe.NeustartNurFuerHost(transform, "Button_Retry");
        }

        public void Restart()
        {
            if (GameControl.m_Current != null) GameControl.m_Current.NeustartAnfordern();
        }

        public void Continue()
        {
            NetzwerkSitzung.Verlassen();
        }
    }
}
