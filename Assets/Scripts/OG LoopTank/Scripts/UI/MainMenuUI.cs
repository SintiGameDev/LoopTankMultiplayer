using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TopDownRace.ScriptableObjects;
namespace TopDownRace
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField, Space]
        private DataStorage m_DataStorage;

        [SerializeField, Space]
        private GameplayData m_GameplayData;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
        }

        public void BtnExit()
        {
            Application.Quit();
        }

        public void BtnLevel(int num)
        {
            m_GameplayData.LevelNumber = num;
            switch (num)
            {
                case 0:
                    SceneManager.LoadScene("Forest");
                    break;

                case 1:
                    SceneManager.LoadScene("Desert");
                    break;

                case 2:
                    SceneManager.LoadScene("Snow");
                    break;
            }



        }
    }
}