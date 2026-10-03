using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TopDownRace
{
    /// <summary>
    /// Baukasten fuer die Oberflaeche. Menue, Lobby, HUD und Ladebildschirm werden komplett im
    /// Code aufgebaut: Es gibt keine Verdrahtung im Inspector, die kaputtgehen kann, und alle
    /// Oberflaechen teilen sich dieselben Farben, Formen und Animationen.
    /// </summary>
    public static class UiBau
    {
        // Farben des Spiels
        public static readonly Color Dunkel = new Color(0.03f, 0.06f, 0.12f);
        public static readonly Color Flaeche = new Color(0.07f, 0.12f, 0.21f, 0.92f);
        public static readonly Color FlaecheHell = new Color(0.13f, 0.20f, 0.32f, 0.95f);
        public static readonly Color Akzent = new Color(0.10f, 0.83f, 0.85f);
        public static readonly Color Gut = new Color(0.45f, 1.00f, 0.55f);
        public static readonly Color Warnung = new Color(1.00f, 0.42f, 0.36f);
        public static readonly Color Hell = new Color(0.94f, 0.97f, 1.00f);
        public static readonly Color Gedimmt = new Color(0.60f, 0.68f, 0.78f);

        private static Sprite s_Rund;

        /// <summary>Weisses Rechteck mit runden Ecken, als 9-Slice. Wird einmal im Speicher erzeugt.</summary>
        public static Sprite RundSprite
        {
            get
            {
                if (s_Rund != null) return s_Rund;

                const int groesse = 64;
                const float radius = 18f;
                var textur = new Texture2D(groesse, groesse, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                };
                for (int y = 0; y < groesse; y++)
                {
                    for (int x = 0; x < groesse; x++)
                    {
                        // Abstand zur naechsten Ecken-Mitte; ausserhalb des Radius wird es durchsichtig
                        float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (groesse - radius), 0f);
                        float dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (groesse - radius), 0f);
                        float abstand = Mathf.Sqrt(dx * dx + dy * dy);
                        textur.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(radius - abstand + 0.5f)));
                    }
                }
                textur.Apply();

                s_Rund = Sprite.Create(textur, new Rect(0, 0, groesse, groesse), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
                s_Rund.hideFlags = HideFlags.HideAndDontSave;
                return s_Rund;
            }
        }

        /// <summary>Canvas, das sich an jede Aufloesung anpasst (Bezug 1920 x 1080).</summary>
        public static Canvas Leinwand(string name, int reihenfolge, Transform eltern = null)
        {
            var objekt = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (eltern != null) objekt.transform.SetParent(eltern, false);
            objekt.layer = LayerMask.NameToLayer("UI");

            var canvas = objekt.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = reihenfolge;

            var scaler = objekt.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>Ohne EventSystem reagiert kein Knopf. Legt eines an, falls die Szene keines hat.</summary>
        public static void EventSystemSicherstellen()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        public static RectTransform Rechteck(string name, Transform eltern)
        {
            var objekt = new GameObject(name, typeof(RectTransform));
            objekt.layer = eltern.gameObject.layer;
            objekt.transform.SetParent(eltern, false);
            return (RectTransform)objekt.transform;
        }

        /// <summary>Farbige Flaeche, wahlweise mit runden Ecken.</summary>
        public static Image Bild(string name, Transform eltern, Color farbe, bool rund = true)
        {
            RectTransform rect = Rechteck(name, eltern);
            var bild = rect.gameObject.AddComponent<Image>();
            bild.color = farbe;
            bild.raycastTarget = false;
            if (rund)
            {
                bild.sprite = RundSprite;
                bild.type = Image.Type.Sliced;
                bild.pixelsPerUnitMultiplier = 1f;
            }
            return bild;
        }

        public static TextMeshProUGUI Text(string name, Transform eltern, string inhalt, float groesse, Color farbe,
            TextAlignmentOptions ausrichtung = TextAlignmentOptions.Center, FontStyles stil = FontStyles.Normal)
        {
            RectTransform rect = Rechteck(name, eltern);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = inhalt;
            text.fontSize = groesse;
            text.color = farbe;
            text.alignment = ausrichtung;
            text.fontStyle = stil;
            text.raycastTarget = false;
            text.richText = true;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }

        /// <summary>Knopf mit weichem Hover- und Klick-Verhalten. primaer = in Akzentfarbe.</summary>
        public static UiKnopf Knopf(string name, Transform eltern, string beschriftung, Vector2 groesse, bool primaer)
        {
            Image flaeche = Bild(name, eltern, primaer ? Akzent : FlaecheHell);
            flaeche.raycastTarget = true;
            flaeche.rectTransform.sizeDelta = groesse;

            var knopf = flaeche.gameObject.AddComponent<Button>();
            knopf.targetGraphic = flaeche;
            knopf.transition = Selectable.Transition.None;   // die Animation macht UiKnopf

            TextMeshProUGUI text = Text("Text", flaeche.transform, beschriftung, Mathf.Min(40f, groesse.y * 0.42f),
                primaer ? Dunkel : Hell, TextAlignmentOptions.Center, FontStyles.Bold);
            Strecken(text.rectTransform);

            var ui = flaeche.gameObject.AddComponent<UiKnopf>();
            ui.Einrichten(knopf, flaeche, text, primaer ? Akzent : FlaecheHell);
            return ui;
        }

        /// <summary>Fuellt das Elternobjekt ganz aus, optional mit Rand.</summary>
        public static void Strecken(RectTransform rect, float rand = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(rand, rand);
            rect.offsetMax = new Vector2(-rand, -rand);
        }

        /// <summary>Feste Groesse an einem Ankerpunkt (0..1 im Elternobjekt). Der Drehpunkt liegt am selben Punkt.</summary>
        public static void Setzen(RectTransform rect, Vector2 anker, Vector2 position, Vector2 groesse)
        {
            rect.anchorMin = anker;
            rect.anchorMax = anker;
            rect.pivot = anker;
            rect.anchoredPosition = position;
            rect.sizeDelta = groesse;
        }

        /// <summary>Wie Setzen, aber mit eigenem Drehpunkt.</summary>
        public static void Setzen(RectTransform rect, Vector2 anker, Vector2 drehpunkt, Vector2 position, Vector2 groesse)
        {
            rect.anchorMin = anker;
            rect.anchorMax = anker;
            rect.pivot = drehpunkt;
            rect.anchoredPosition = position;
            rect.sizeDelta = groesse;
        }

        // ------------------------------------------------------------------
        // Animationen (laufen unabhaengig von Time.timeScale)
        // ------------------------------------------------------------------

        public static float WeichAus(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        public static float WeichEin(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        /// <summary>
        /// Schiebt ein Element von einer Position zur anderen und blendet es dabei ein oder aus.
        /// Das ist der "Swipe" zwischen den Menueseiten.
        /// </summary>
        public static IEnumerator Schieben(RectTransform rect, CanvasGroup gruppe, Vector2 von, Vector2 nach, float alphaVon, float alphaNach, float dauer, float verzoegerung = 0f)
        {
            if (gruppe != null) gruppe.alpha = alphaVon;
            rect.anchoredPosition = von;

            float warten = 0f;
            while (warten < verzoegerung)
            {
                warten += Time.unscaledDeltaTime;
                yield return null;
            }

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / Mathf.Max(0.01f, dauer);
                float k = WeichAus(t);
                rect.anchoredPosition = Vector2.LerpUnclamped(von, nach, k);
                if (gruppe != null) gruppe.alpha = Mathf.Lerp(alphaVon, alphaNach, k);
                yield return null;
            }
            rect.anchoredPosition = nach;
            if (gruppe != null) gruppe.alpha = alphaNach;
        }
    }
}
