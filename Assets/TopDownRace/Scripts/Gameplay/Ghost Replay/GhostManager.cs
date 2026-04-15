using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TopDownRace
{
    public class GhostManager : MonoBehaviour
    {
        public static GhostManager Instance { get; private set; }

        [Header("References")]
        public TMP_Text lapStatusText;
        public TMP_Text lastLapTimeText;
        public LapRecorder playerRecorder;
        public GameObject ghostPrefab;
        public Transform ghostParent;
        // Neue Referenz für den UI-Balken
        public GameObject lapStatusUIBar;

        public enum GhostMode { Off, LastLap, BestLap }

        [Header("Ghost Mode")]
        public GhostMode mode = GhostMode.LastLap;

        [Header("Lap Status Text Settings")]
        [Tooltip("Die Zeit in Sekunden, die der Lap Status Text voll sichtbar bleibt.")]
        public float lapStatusDisplayTime = 3.0f;
        [Tooltip("Die Zeit in Sekunden für den Ein- und Ausblendeffekt des Lap Status Textes.")]
        public float lapStatusFadeDuration = 0.5f;

        [Header("Last Lap Time Text Settings")]
        [Tooltip("Die Zeit in Sekunden, die der Last Lap Time Text voll sichtbar bleibt.")]
        public float lastLapTimeDisplayTime = 3.0f;
        [Tooltip("Die Zeit in Sekunden für den Ein- und Ausblendeffekt des Last Lap Time Textes.")]
        public float lastLapTimeFadeDuration = 0.5f;

        [Header("Limits")]
        public int maxGhosts = 2;

        [Header("Sounds")]
        [Tooltip("Der Sound, der abgespielt wird, wenn eine Runde erfolgreich beendet wurde.")]
        public AudioClip m_LapFinishedSound;
        [Tooltip("Die Lautstärke des 'Runde beendet'-Sounds.")]
        [Range(0.0f, 1.0f)]
        public float m_LapFinishedVolume = 1.0f;
        private AudioSource m_AudioSource;

        LapData lastLap;
        LapData bestLap;

        private List<GhostReplay> ghostInstances = new List<GhostReplay>();

        private int currentLapNumber = 0;
        private float lastLapTime = 0f;
        private float previousLapTime = 0f;

        // Coroutine-Referenzen, um sie gezielt zu stoppen
        private Coroutine lapStatusFadeRoutine;
        private Coroutine lastLapTimeFadeRoutine;

        // Timer
        public Timer roundTimer;
        private bool timerStarted = false;
        private bool timerUiShown = true;

        private bool hasSpawnedFirstGhost = false;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple GhostManager instances found! Destroying duplicate.");
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
        }

        void Start()
        {
            m_AudioSource = GetComponent<AudioSource>();
            if (m_AudioSource == null)
            {
                m_AudioSource = gameObject.AddComponent<AudioSource>();
                m_AudioSource.playOnAwake = false;
            }

            // Sicherstellen, dass die UI-Elemente zu Beginn deaktiviert sind, damit die Coroutine sie kontrollieren kann.
            if (lapStatusText != null)
            {
                lapStatusText.gameObject.SetActive(false);
            }
            if (lastLapTimeText != null)
            {
                lastLapTimeText.gameObject.SetActive(false);
            }
            if (lapStatusUIBar != null)
            {
                lapStatusUIBar.SetActive(false);
            }

            currentLapNumber = 0;
        }

        public void OnLapStarted()
        {
            playerRecorder.BeginLap();
            currentLapNumber++;

            // Starte die Anzeige für den Rundenstatus, dies wird jetzt immer beim Start der Runde gemacht.
            if (lapStatusText != null)
            {
                // Stoppe die vorherige Coroutine, falls sie noch läuft.
                if (lapStatusFadeRoutine != null) StopCoroutine(lapStatusFadeRoutine);

                // UI-Balken aktivieren, bevor die Coroutine startet
                if (lapStatusUIBar != null)
                {
                    lapStatusUIBar.SetActive(true);
                }

                lapStatusText.text = $"Lap {currentLapNumber} Started";
                lapStatusFadeRoutine = StartCoroutine(FadeText(lapStatusText, lapStatusFadeDuration, lapStatusDisplayTime, lapStatusUIBar));
            }
        }

        public void OnLapFinished(float currentLapTime)
        {
            playerRecorder.EndLap(currentLapTime);

            if (playerRecorder.currentLap == null || playerRecorder.currentLap.frames.Count < 2)
            {
                Debug.LogWarning("Current lap data is empty or invalid. Cannot create ghost.");
                return;
            }

            // Korrigierte Bedingung: Zeigt den Text nur an, wenn bereits eine vorherige Rundenzeit existiert (also ab Runde 2)
            if (lastLapTimeText != null && lastLapTime > 0)
            {
                if (lastLapTimeFadeRoutine != null) StopCoroutine(lastLapTimeFadeRoutine);

                float timeDifference = currentLapTime - lastLapTime;
                string statusText = "";
                //Color textColor = timeDifference < 0 ? Color.green : Color.red;

                // WIEDERHERGESTELLTE LOGIK: Anzeige der Differenz zur vorherigen Runde
                if (timeDifference < 0)
                {
                    statusText = $"Best Lap: {FormatTime(timeDifference)}";
                }
                else
                {
                    statusText = $"Previous Lap: {FormatTime(timeDifference)}";
                }

                //lastLapTimeText.color = textColor;
                lastLapTimeText.text = statusText;
                lastLapTimeFadeRoutine = StartCoroutine(FadeText(lastLapTimeText, lastLapTimeFadeDuration, lastLapTimeDisplayTime));
            }

            // Speichere die aktuelle Rundenzeit für den nächsten Vergleich
            lastLapTime = currentLapTime;

            if (m_LapFinishedSound != null && m_AudioSource != null)
            {
                m_AudioSource.PlayOneShot(m_LapFinishedSound, m_LapFinishedVolume);
            }

            lastLap = Clone(playerRecorder.currentLap);

            if (bestLap == null || currentLapTime < bestLap.lapTime)
            {
                bestLap = Clone(playerRecorder.currentLap);
            }

            LapData toReplay = null;
            if (mode == GhostMode.LastLap)
            {
                toReplay = lastLap;
            }
            else if (mode == GhostMode.BestLap)
            {
                toReplay = bestLap;
            }

            if (!hasSpawnedFirstGhost)
            {
                hasSpawnedFirstGhost = true;
                return;
            }

            if (toReplay != null)
            {
                StartCoroutine(SpawnAndActivateGhostDelayed(Clone(toReplay), 0.8f));
            }
        }

        private IEnumerator FadeText(TMP_Text textObject, float fadeDuration, float displayTime, GameObject associatedBar = null)
        {
            if (textObject == null) yield break;

            Color baseColor = textObject.color;
            Color startColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0);
            Color endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 1);

            // Fade In
            textObject.gameObject.SetActive(true);
            float timer = 0;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                textObject.color = Color.Lerp(startColor, endColor, timer / fadeDuration);
                yield return null;
            }
            textObject.color = endColor;

            // Wartezeit
            yield return new WaitForSeconds(displayTime);

            // Fade Out
            timer = 0;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                textObject.color = Color.Lerp(endColor, startColor, timer / fadeDuration);
                yield return null;
            }
            textObject.color = startColor;
            textObject.gameObject.SetActive(false);

            // Deaktiviere den zugehörigen Balken, wenn er existiert
            if (associatedBar != null)
            {
                associatedBar.SetActive(false);
            }
        }

        private string FormatTime(float time)
        {
            // Die absolute Zeit verwenden, da TimeSpan mit negativen Werten Probleme hat
            System.TimeSpan timeSpan = System.TimeSpan.FromSeconds(Mathf.Abs(time));
            // Hinzufügen des Vorzeichens für positive oder negative Zeiten
            string sign = time >= 0 ? "" : "";
            return $"{sign}{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
        }

        private IEnumerator SpawnAndActivateGhostDelayed(LapData ghostLap, float delaySeconds)
        {
            var go = Instantiate(
                ghostPrefab,
                playerRecorder.transform.position,
                playerRecorder.transform.rotation,
                ghostParent
            );
            var ghost = go.GetComponent<GhostReplay>();

            var spriteRenderers = ghost.GetComponentsInChildren<SpriteRenderer>();
            foreach (var item in spriteRenderers)
            {
                if (item != null)
                    item.gameObject.SetActive(true);
            }

            var collisionIgnorer = FindDeepChild(go.transform, "CollisionIgnorer");
            if (collisionIgnorer != null)
            {
                collisionIgnorer.gameObject.SetActive(true);
                StartCoroutine(DisableAfterSeconds(collisionIgnorer.gameObject, 1f, ghost));
            }

            ghostInstances.Add(ghost);
            if (ghostInstances.Count > maxGhosts)
            {
                Destroy(ghostInstances[0].gameObject);
                ghostInstances.RemoveAt(0);
            }

            yield return new WaitForSeconds(delaySeconds);

            if (ghost != null)
                ghost.Play(ghostLap);
        }

        private IEnumerator DisableAfterSeconds(GameObject obj, float seconds, GhostReplay ghost)
        {
            yield return new WaitForSeconds(seconds);
            if (obj != null)
            {
                obj.SetActive(false);
                obj.tag = "Untagged";

                var spriteRenderers = ghost.GetComponentsInChildren<SpriteRenderer>();
                foreach (var item in spriteRenderers)
                {
                    if (item != null)
                        item.gameObject.SetActive(true);
                }
            }
        }

        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var result = FindDeepChild(child, name);
                if (result != null)
                    return result;
            }
            return null;
        }

        LapData Clone(LapData src)
        {
            var c = ScriptableObject.CreateInstance<LapData>();
            c.lapTime = src.lapTime;
            foreach (var f in src.frames)
                c.frames.Add(new LapFrame { t = f.t, pos = f.pos, rotZ = f.rotZ });
            return c;
        }

        public void ClearAllGhosts()
        {
            foreach (var ghost in ghostInstances)
                if (ghost != null) Destroy(ghost.gameObject);
            ghostInstances.Clear();
        }

        public void RemoveGhost(GhostReplay ghost)
        {
            if (ghostInstances.Contains(ghost))
            {
                Destroy(ghost.gameObject);
                ghostInstances.Remove(ghost);
            }
        }

        public void SetMode(int m) { mode = (GhostMode)m; }

        public IReadOnlyList<GhostReplay> ActiveGhosts => ghostInstances;

        private IEnumerator BlinkSprite(GameObject spriteObj)
        {
            if (spriteObj == null) yield break;
            SpriteRenderer spriteRenderer = spriteObj.GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) yield break;

            for (int i = 0; i < 3; i++)
            {
                spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
                yield return new WaitForSeconds(0.1f);
                spriteRenderer.color = new Color(1f, 1f, 1f, 0f);
                yield return new WaitForSeconds(0.1f);
            }
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        }
    }
}