using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        }

        void Update()
        {
        }

        public void Restart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        public void Continue()
        {
            SceneManager.LoadScene("MainMenu");
        }
    }

}