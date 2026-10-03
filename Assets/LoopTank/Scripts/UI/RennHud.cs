using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TopDownRace
{
    /// <summary>
    /// Kleine Multiplayer-Anzeige ueber dem Rennen: Spielerliste mit Runden und ein Banner
    /// fuer Meldungen wie "Du bist raus". Baut sich beim Start selbst auf, es muss also
    /// nichts im Inspector verdrahtet werden.
    /// </summary>
    public class RennHud : MonoBehaviour
    {
        public static RennHud Instanz { get; private set; }

        [Tooltip("Optional. Ohne Angabe wird die Standard-Schrift von TextMesh Pro benutzt.")]
        public TMP_FontAsset schrift;

        private TextMeshProUGUI m_Spielerliste;
        private TextMeshProUGUI m_Banner;
        private readonly StringBuilder m_Text = new StringBuilder();
        private float m_NaechsteAktualisierung;

        private void Awake()
        {
            Instanz = this;

            var canvasObjekt = new GameObject("RennHud-Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObjekt.transform.SetParent(transform, false);
            var canvas = canvasObjekt.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = canvasObjekt.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            m_Spielerliste = TextErzeugen(canvasObjekt.transform, "Spielerliste", 30, TextAlignmentOptions.TopLeft);
            var liste = m_Spielerliste.rectTransform;
            liste.anchorMin = liste.anchorMax = liste.pivot = new Vector2(0, 1);
            liste.anchoredPosition = new Vector2(30, -30);
            liste.sizeDelta = new Vector2(600, 300);

            m_Banner = TextErzeugen(canvasObjekt.transform, "Banner", 44, TextAlignmentOptions.Center);
            var banner = m_Banner.rectTransform;
            banner.anchorMin = banner.anchorMax = banner.pivot = new Vector2(0.5f, 1);
            banner.anchoredPosition = new Vector2(0, -190);
            banner.sizeDelta = new Vector2(1500, 80);
            m_Banner.text = "";
        }

        private void OnDestroy()
        {
            if (Instanz == this) Instanz = null;
        }

        private TextMeshProUGUI TextErzeugen(Transform eltern, string name, float groesse, TextAlignmentOptions ausrichtung)
        {
            var objekt = new GameObject(name, typeof(RectTransform));
            objekt.transform.SetParent(eltern, false);
            var text = objekt.AddComponent<TextMeshProUGUI>();
            if (schrift != null) text.font = schrift;
            text.fontSize = groesse;
            text.alignment = ausrichtung;
            text.raycastTarget = false;
            text.richText = true;
            text.outlineWidth = 0.2f;
            text.outlineColor = new Color32(0, 0, 0, 255);
            return text;
        }

        public void BannerZeigen(string text)
        {
            if (m_Banner != null) m_Banner.text = text;
        }

        private void Update()
        {
            if (Time.unscaledTime < m_NaechsteAktualisierung) return;
            m_NaechsteAktualisierung = Time.unscaledTime + 0.25f;

            var leitung = GameControl.m_Current;
            if (leitung == null || !leitung.IsSpawned)
            {
                m_Spielerliste.text = "";
                return;
            }

            if (leitung.Phase.Value == RennPhase.Warten)
            {
                m_Spielerliste.text = "";
                BannerZeigen("Warte auf Mitspieler ...");
                return;
            }
            if (m_Banner.text == "Warte auf Mitspieler ...") BannerZeigen("");

            // Im Einzelspieler gibt es niemanden aufzulisten.
            if (leitung.IstEinzelspieler)
            {
                m_Spielerliste.text = "";
                return;
            }

            m_Text.Clear();
            for (int slot = 0; slot < NetzwerkSitzung.MaxSpieler; slot++)
            {
                foreach (var panzer in PlayerCar.Alle)
                {
                    if (panzer == null || !panzer.IsSpawned || panzer.SpielerSlot.Value != slot) continue;

                    string name = SpielerFarben.Name(slot) + (panzer.IsOwner ? " - du" : "");
                    string zeile = name + ":  " + panzer.Runden.Value + " Runden";
                    if (panzer.Lebt.Value)
                        m_Text.Append("<color=#").Append(SpielerFarben.Hex(slot)).Append(">").Append(zeile).Append("</color>\n");
                    else
                        m_Text.Append("<color=#777777><s>").Append(zeile).Append("</s>  raus</color>\n");
                }
            }
            m_Spielerliste.text = m_Text.ToString();
        }
    }
}
