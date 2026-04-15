using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace TopDownRace
{
    public class UISystem : MonoBehaviour
    {
        public static UISystem m_Main;


        public UIData m_UIData;
        [HideInInspector]
        public List<GameObject> m_UIStack;
        [HideInInspector]
        public GameObject m_LastUI;
        [HideInInspector]
        public int m_LayerOrder = 0;
        public int m_MessageLayerOrder = 0;

        public string m_InitUI = "";

        public Vector2 m_GeneralCanvasSize = new Vector2(1600, 900);

        private void Awake()
        {
            m_Main = this;



            m_UIStack = new List<GameObject>();
            m_LayerOrder = 1;
            m_MessageLayerOrder = 100;
        }
        // Start is called before the first frame update
        void Start()
        {
            float ratio = (float)Screen.width / (float)Screen.height;
            m_GeneralCanvasSize = new Vector2(ratio * 900, 900);

            if (m_InitUI != "")
            {
                ShowUI(m_InitUI);
            }
        }

        // Update is called once per frame
        void Update()
        {

        }

        public static GameObject FindUIByName(string uiName)
        {
            foreach (GameObject uiObj in m_Main.m_UIData.m_UIPrefabs)
            {
                if (uiObj.name == uiName)
                {
                    return uiObj;
                }
            }
            return null;
        }

        public static GameObject FindOpenUIByName(string uiName)
        {
            for (int i = 0; i < m_Main.m_UIStack.Count; i++)
            {
                if (m_Main.m_UIStack[i].name == uiName)
                {
                    return m_Main.m_UIStack[i];
                }
            }
            return null;
        }
        public static GameObject ShowUI(string uiName)
        {
            GameObject uiprefab = FindUIByName(uiName);
            if (uiprefab != null)
            {
                GameObject uiObj = Instantiate(uiprefab);
                uiObj.transform.SetParent(m_Main.transform);
                uiObj.transform.localPosition = Vector3.zero;
                uiObj.name = uiName;
                m_Main.m_LastUI = uiObj;
                m_Main.m_UIStack.Add(uiObj);

                Canvas canvas = uiObj.GetComponentInChildren<Canvas>();
                if (canvas != null)
                {
                    canvas.sortingOrder = m_Main.m_LayerOrder;
                    m_Main.m_LayerOrder++;
                }

                return uiObj;
            }
            Debug.Log("UI not found : " + uiName);
            return null;
        }

        public static void RemoveUI(string uiName)
        {
            for (int i = 0; i < m_Main.m_UIStack.Count; i++)
            {
                if (m_Main.m_UIStack[i].name == uiName)
                {
                    // if (i == m_Main.m_UIStack.Count - 1)
                    // {
                    //     m_Main.m_LayerOrder--;
                    //     m_Main.m_LayerOrder = Mathf.Clamp(m_Main.m_LayerOrder, 0, 100);
                    // }
                    if (m_Main.m_LastUI == m_Main.m_UIStack[i])
                        m_Main.m_LastUI = null;
                    Destroy(m_Main.m_UIStack[i]);
                    m_Main.m_UIStack.RemoveAt(i);
                    i--;
                }
            }
        }

        public static void RemoveUI(GameObject uiObj)
        {
            for (int i = 0; i < m_Main.m_UIStack.Count; i++)
            {
                if (m_Main.m_UIStack[i] == uiObj)
                {

                    // if (i == m_Main.m_UIStack.Count - 1)
                    // {
                    //     m_Main.m_LayerOrder--;
                    //     m_Main.m_LayerOrder = Mathf.Clamp(m_Main.m_LayerOrder, 0, 100);
                    // }
                    if (m_Main.m_LastUI == m_Main.m_UIStack[i])
                        m_Main.m_LastUI = null;
                    Destroy(m_Main.m_UIStack[i]);
                    m_Main.m_UIStack.RemoveAt(i);
                    i--;
                }
            }
        }




        public static Image FindImage(GameObject parent, string targetName)
        {
            Image[] all = parent.GetComponentsInChildren<Image>(true);
            foreach (Image img in all)
            {
                if (img.gameObject.name == targetName)
                {
                    return img;
                }
            }

            return null;
        }

        public static Text FindText(GameObject parent, string targetName)
        {
            Text[] all = parent.GetComponentsInChildren<Text>(true);
            foreach (Text text in all)
            {
                if (text.gameObject.name == targetName)
                {
                    return text;
                }
            }
            return null;
        }

        public static void ShowReward(string rewardType, int count1 = 0, int count2 = 0, string title = "", Sprite sprite = null)
        {
            m_Main.StartCoroutine(Co_ShowReward(rewardType, title, count1, count2, sprite));
        }

        static IEnumerator Co_ShowReward(string rewardType, string title, int count1, int count2, Sprite sprite)
        {
            string prefab = "CoinRewardUI";
            GameObject uiObj = null;
            Text txt;
            Image image;
            switch (rewardType)
            {
                case "coin":
                    prefab = "CoinRewardUI";
                    uiObj = ShowUI(prefab);
                    txt = FindText(uiObj, "text-amount");
                    txt.text = count1.ToString();
                    break;

                case "gem":
                    prefab = "GemRewardUI";
                    uiObj = ShowUI(prefab);
                    txt = FindText(uiObj, "text-amount");
                    txt.text = count1.ToString();
                    break;

                case "upgrade":
                    prefab = "UpgradeRewardUI";
                    uiObj = ShowUI(prefab);
                    txt = FindText(uiObj, "text-title");
                    txt.text = title;
                    image = FindImage(uiObj, "img-icon");
                    image.sprite = sprite;
                    break;

                case "turn":
                    prefab = "TurnRewardUI";
                    uiObj = ShowUI(prefab);
                    txt = FindText(uiObj, "text-amount");
                    txt.text = count1.ToString();
                    break;

                case "wheel":
                    prefab = "WheelRewardMsg";
                    uiObj = ShowUI(prefab);
                    txt = FindText(uiObj, "text-amount");
                    txt.text = count1.ToString();
                    break;
            }


            Canvas canvas = uiObj.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 150;
            }

            yield return new WaitForSeconds(3);
            RemoveUI(uiObj);
        }

        public static void ShowCoinReward(int count)
        {
            m_Main.StartCoroutine(Co_ShowCoinReward(count));
        }

        static IEnumerator Co_ShowCoinReward(int count)
        {
            GameObject uiObj = ShowUI("CoinRewardUI");
            Text txt = FindText(uiObj, "text-amount");
            txt.text = count.ToString();

            Canvas canvas = uiObj.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 150;
            }

            yield return new WaitForSeconds(3);
            RemoveUI(uiObj);
        }

        public static void ShowGemReward(int count)
        {
            m_Main.StartCoroutine(Co_ShowGemReward(count));
        }

        static IEnumerator Co_ShowGemReward(int count)
        {
            GameObject uiObj = ShowUI("GemRewardUI");
            Text txt = FindText(uiObj, "text-amount");
            txt.text = count.ToString();

            Canvas canvas = uiObj.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 150;
            }

            yield return new WaitForSeconds(3);
            RemoveUI(uiObj);
        }
        public static void ShowCoinGemReward(int coincount, int gemcount)
        {
            m_Main.StartCoroutine(Co_ShowCoinGemReward(coincount, gemcount));
        }

        static IEnumerator Co_ShowCoinGemReward(int coincount, int gemcount)
        {
            GameObject uiObj = ShowUI("CoinGemRewardUI");
            Text txtcoin = FindText(uiObj, "coin-text-amount");
            Text txtgem = FindText(uiObj, "gem-text-amount");
            txtcoin.text = coincount.ToString();
            txtgem.text = gemcount.ToString();

            Canvas canvas = uiObj.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 150;
            }

            yield return new WaitForSeconds(3);
            RemoveUI(uiObj);
        }

        public static void ShowUpgradeReward(Sprite img, string title)
        {
            m_Main.StartCoroutine(Co_ShowUpgradeReward(img, title));
        }

        static IEnumerator Co_ShowUpgradeReward(Sprite img, string title)
        {
            GameObject uiObj = ShowUI("UpgradeRewardUI");
            Text txt = FindText(uiObj, "text-title");
            txt.text = title;
            Image image = FindImage(uiObj, "img-icon");
            image.sprite = img;

            Canvas canvas = uiObj.GetComponentInChildren<Canvas>();
            if (canvas != null)
            {
                canvas.sortingOrder = 150;
            }

            yield return new WaitForSeconds(3);
            RemoveUI(uiObj);
        }
    }
}