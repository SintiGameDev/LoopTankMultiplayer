using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Bildeffekt der 3D-Kamera fuer das Geschwindigkeitsgefuehl: radiale Unschaerfe,
    /// Geschwindigkeitsstreifen an den Bildseiten, Farbsaum und Vignette. Alles waechst mit dem
    /// Tempo und laesst die Bildmitte scharf, damit das Zielen nicht leidet.
    ///
    /// Die Staerke (0 = aus, 1 = Hoechsttempo, darueber = Boost) setzt VerfolgerKamera jedes Bild.
    /// Funktioniert mit der Built-in Render Pipeline (OnRenderImage). Unter URP waere dafuer
    /// eine Renderer Feature noetig.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class TempoEffekt : MonoBehaviour
    {
        [Tooltip("Ab welchem Anteil des Höchsttempos der Effekt beginnt. Darunter ist das Bild unverändert.")]
        [Range(0f, 0.95f)]
        public float m_AbTempo = 0.45f;

        [Header("Stärke bei Höchsttempo (0 = aus)")]
        [Range(0f, 1.5f)] public float m_Unschaerfe = 0.7f;
        [Range(0f, 1.5f)] public float m_Streifen = 0.8f;
        [Range(0f, 1.5f)] public float m_Farbsaum = 0.6f;
        [Range(0f, 1f)] public float m_Vignette = 0.3f;

        [Header("Streifen")]
        [Tooltip("In wie viele Fächer das Bild rund um die Mitte geteilt wird. Mehr = feinere Streifen.")]
        public float m_StreifenAnzahl = 140f;
        [Tooltip("Anteil der Fächer, die einen Streifen tragen.")]
        [Range(0.05f, 1f)]
        public float m_StreifenDichte = 0.3f;
        [Tooltip("Länge eines Streifens. Klein = kurze Funken, groß = lange Striche.")]
        [Range(0.07f, 1f)]
        public float m_StreifenLaenge = 0.55f;
        [Tooltip("Breite eines Streifens. Klein = haarfein, groß = breite Bänder.")]
        [Range(0.05f, 1f)]
        public float m_StreifenBreite = 0.6f;
        [Tooltip("Wie weit die Streifen zur Bildmitte reichen. 0 = bis in die Mitte, 0,9 = nur am äußersten Rand.")]
        [Range(0f, 0.9f)]
        public float m_StreifenBereich = 0.5f;
        [Tooltip("Farbe der Streifen. Der Alpha-Wert ist die Helligkeit.")]
        public Color m_StreifenFarbe = new Color(1f, 1f, 1f, 0.6f);
        [Tooltip("Wie schnell die Streifen nach außen laufen.")]
        public float m_StreifenTempo = 1.6f;

        [Header("Boost und Drift")]
        [Tooltip("Zusätzliche Stärke, solange der Boost aktiv ist.")]
        public float m_BoostZuschlag = 0.5f;
        [Tooltip("Zusätzliche Stärke im vollen Drift.")]
        public float m_DriftZuschlag = 0.2f;

        /// <summary>Tempo als Anteil des Hoechsttempos (darf ueber 1 liegen).</summary>
        [HideInInspector] public float TempoAnteil;
        /// <summary>0..1, weich ein- und ausgeblendet.</summary>
        [HideInInspector] public float BoostAnteil;
        /// <summary>0..1</summary>
        [HideInInspector] public float DriftAnteil;

        private Material m_Material;
        private float m_Lauf;

        private void OnEnable()
        {
            Shader shader = Resources.Load<Shader>("TempoEffekt");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogWarning("[TempoEffekt] Shader 'TempoEffekt' fehlt oder wird nicht unterstützt, der Effekt bleibt aus.", this);
                enabled = false;
                return;
            }
            m_Material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void OnDisable()
        {
            if (m_Material != null) Destroy(m_Material);
            m_Material = null;
        }

        private void OnRenderImage(RenderTexture quelle, RenderTexture ziel)
        {
            // Unterhalb der Schwelle 0, bei Hoechsttempo 1, mit Boost und Drift etwas darueber.
            float tempo = Mathf.InverseLerp(m_AbTempo, 1f, TempoAnteil);
            float staerke = Mathf.SmoothStep(0f, 1f, tempo) * (1f + m_BoostZuschlag * BoostAnteil + m_DriftZuschlag * DriftAnteil);

            if (m_Material == null || staerke < 0.005f)
            {
                Graphics.Blit(quelle, ziel);
                return;
            }

            m_Lauf += Time.deltaTime * m_StreifenTempo * Mathf.Lerp(0.6f, 1.4f, Mathf.Clamp01(tempo));

            m_Material.SetFloat("_Unschaerfe", m_Unschaerfe * staerke);
            m_Material.SetFloat("_Streifen", m_Streifen * staerke);
            m_Material.SetFloat("_Farbsaum", m_Farbsaum * staerke);
            m_Material.SetFloat("_Vignette", m_Vignette * Mathf.Min(staerke, 1.3f));
            m_Material.SetFloat("_Lauf", m_Lauf);
            m_Material.SetFloat("_Seitenverhaeltnis", (float)quelle.width / Mathf.Max(1, quelle.height));
            m_Material.SetFloat("_StreifenAnzahl", m_StreifenAnzahl);
            m_Material.SetFloat("_StreifenDichte", m_StreifenDichte);
            m_Material.SetFloat("_StreifenLaenge", m_StreifenLaenge);
            m_Material.SetFloat("_StreifenBreite", m_StreifenBreite);
            m_Material.SetFloat("_StreifenBereich", m_StreifenBereich);
            m_Material.SetColor("_StreifenFarbe", m_StreifenFarbe);

            Graphics.Blit(quelle, ziel, m_Material);
        }
    }
}
