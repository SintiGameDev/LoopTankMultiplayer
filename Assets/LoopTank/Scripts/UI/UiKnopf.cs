using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TopDownRace
{
    /// <summary>
    /// Knopf der Spiel-Oberflaeche: wird beim Ueberfahren etwas groesser und heller, beim
    /// Druecken kurz kleiner. Angelegt wird er ueber UiBau.Knopf.
    /// </summary>
    public class UiKnopf : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Button Button { get; private set; }
        public TextMeshProUGUI Beschriftung { get; private set; }

        private Image m_Flaeche;
        private Color m_Grundfarbe;
        private bool m_Darueber;
        private bool m_Gedrueckt;
        private float m_Groesse = 1f;
        private float m_Helligkeit;

        public void Einrichten(Button button, Image flaeche, TextMeshProUGUI beschriftung, Color grundfarbe)
        {
            Button = button;
            m_Flaeche = flaeche;
            Beschriftung = beschriftung;
            m_Grundfarbe = grundfarbe;
        }

        public bool Aktiv
        {
            get => Button.interactable;
            set => Button.interactable = value;
        }

        public string Text
        {
            get => Beschriftung.text;
            set => Beschriftung.text = value;
        }

        public void BeiKlick(UnityEngine.Events.UnityAction aktion)
        {
            Button.onClick.AddListener(aktion);
        }

        public void OnPointerEnter(PointerEventData daten) => m_Darueber = true;
        public void OnPointerExit(PointerEventData daten) { m_Darueber = false; m_Gedrueckt = false; }
        public void OnPointerDown(PointerEventData daten) => m_Gedrueckt = true;
        public void OnPointerUp(PointerEventData daten) => m_Gedrueckt = false;

        private void OnDisable()
        {
            m_Darueber = false;
            m_Gedrueckt = false;
            m_Groesse = 1f;
            m_Helligkeit = 0f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            bool aktiv = Button != null && Button.interactable;
            float zielGroesse = !aktiv ? 1f : m_Gedrueckt ? 0.96f : m_Darueber ? 1.045f : 1f;
            float zielHell = aktiv && m_Darueber ? 1f : 0f;

            float folgen = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            m_Groesse = Mathf.Lerp(m_Groesse, zielGroesse, folgen);
            m_Helligkeit = Mathf.Lerp(m_Helligkeit, zielHell, folgen);
            transform.localScale = new Vector3(m_Groesse, m_Groesse, 1f);

            if (m_Flaeche != null)
            {
                Color farbe = Color.Lerp(m_Grundfarbe, Color.white, 0.22f * m_Helligkeit);
                farbe.a = aktiv ? m_Grundfarbe.a : m_Grundfarbe.a * 0.35f;
                m_Flaeche.color = farbe;
            }
            if (Beschriftung != null) Beschriftung.alpha = aktiv ? 1f : 0.5f;
        }
    }
}
