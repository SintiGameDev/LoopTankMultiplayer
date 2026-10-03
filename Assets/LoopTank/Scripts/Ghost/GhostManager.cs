using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace TopDownRace
{
    /// <summary>
    /// Verwaltet die Rundenaufnahme des lokalen Spielers und die Ghosts aller Spieler.
    /// Die Aufnahme ist lokal; fertige Runden gehen ueber PlayerCar an alle Clients und
    /// kommen dort (auch beim Absender) in GhostStarten wieder an.
    /// </summary>
    public class GhostManager : MonoBehaviour
    {
        public static GhostManager Instance { get; private set; }

        [Header("References")]
        public TMP_Text lapStatusText;
        public TMP_Text lastLapTimeText;
        public LapRecorder playerRecorder;
        public GameObject ghostPrefab;
        public Transform ghostParent;
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
        [Tooltip("Maximale Anzahl Ghosts pro Spieler. Ist sie erreicht, verschwindet der älteste.")]
        public int maxGhosts = 2;

        [Header("Multiplayer")]
        [Tooltip("Sekunden, die ein neuer Ghost am Start wartet, bevor er losfährt.")]
        public float ghostStartVerzoegerung = 0.8f;
        [Tooltip("Deckkraft eigener Ghosts, wenn sie für einen selbst harmlos sind.")]
        [Range(0.1f, 1f)]
        public float harmloseGhostDeckkraft = 0.35f;

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

        private Coroutine lapStatusFadeRoutine;
        private Coroutine lastLapTimeFadeRoutine;

        public Timer roundTimer;

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

            if (lapStatusText != null && lapStatusFadeRoutine == null)
            {
                lapStatusText.gameObject.SetActive(false);
            }
            if (lastLapTimeText != null)
            {
                lastLapTimeText.gameObject.SetActive(false);
            }
            if (lapStatusUIBar != null && lapStatusFadeRoutine == null)
            {
                lapStatusUIBar.SetActive(false);
            }
        }

        // ------------------------------------------------------------------
        // Runden des lokalen Spielers
        // ------------------------------------------------------------------

        public void OnLapStarted()
        {
            if (playerRecorder == null) return;

            playerRecorder.BeginLap();
            currentLapNumber++;

            if (lapStatusText != null)
            {
                if (lapStatusFadeRoutine != null) StopCoroutine(lapStatusFadeRoutine);

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
            if (playerRecorder == null) return;

            playerRecorder.EndLap(currentLapTime);

            if (playerRecorder.currentLap == null || playerRecorder.currentLap.frames.Count < 2)
            {
                Debug.LogWarning("Current lap data is empty or invalid. Cannot create ghost.");
                return;
            }

            // Erst ab Runde 2 gibt es eine vorherige Rundenzeit zum Vergleichen
            if (lastLapTimeText != null && lastLapTime > 0)
            {
                if (lastLapTimeFadeRoutine != null) StopCoroutine(lastLapTimeFadeRoutine);

                float timeDifference = currentLapTime - lastLapTime;
                string statusText;
                if (timeDifference < 0)
                {
                    statusText = $"Best Lap: {FormatTime(timeDifference)}";
                }
                else
                {
                    statusText = $"Previous Lap: {FormatTime(timeDifference)}";
                }

                lastLapTimeText.text = statusText;
                lastLapTimeFadeRoutine = StartCoroutine(FadeText(lastLapTimeText, lastLapTimeFadeDuration, lastLapTimeDisplayTime));
            }

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

            // Die erste Zielueberquerung ist nur die Anfahrt vom Startplatz, kein ganzer Loop.
            if (!hasSpawnedFirstGhost)
            {
                hasSpawnedFirstGhost = true;
                return;
            }

            if (toReplay != null && PlayerCar.m_Current != null)
            {
                PlayerCar.m_Current.RundeAlsGhostSenden(toReplay, ghostStartVerzoegerung);
            }
        }

        // ------------------------------------------------------------------
        // Ghosts aller Spieler
        // ------------------------------------------------------------------

        /// <summary>
        /// Erzeugt den Ghost eines Spielers aus den uebertragenen Punkten (x, y, Drehung).
        /// Laeuft auf jedem Client, auch bei dem, dessen Runde es ist.
        /// </summary>
        public void GhostStarten(ulong besitzerId, int spielerSlot, Vector3[] punkte, float intervall, double startZeit)
        {
            if (ghostPrefab == null || punkte == null || punkte.Length < 2) return;

            var lap = ScriptableObject.CreateInstance<LapData>();
            for (int n = 0; n < punkte.Length; n++)
            {
                lap.frames.Add(new LapFrame { t = n * intervall, pos = new Vector2(punkte[n].x, punkte[n].y), rotZ = punkte[n].z });
            }
            lap.lapTime = lap.frames[lap.frames.Count - 1].t;

            var go = Instantiate(
                ghostPrefab,
                new Vector3(punkte[0].x, punkte[0].y, 0f),
                Quaternion.Euler(0f, 0f, punkte[0].z),
                ghostParent
            );
            var ghost = go.GetComponent<GhostReplay>();

            // Kurz nach dem Erscheinen ist der Ghost harmlos, damit niemand direkt in ihm steht.
            var collisionIgnorer = FindDeepChild(go.transform, "CollisionIgnorer");
            if (collisionIgnorer != null)
            {
                collisionIgnorer.gameObject.SetActive(true);
                StartCoroutine(DisableAfterSeconds(collisionIgnorer.gameObject, 1f));
            }

            bool toedlich = GameControl.m_Current == null || GameControl.m_Current.IstGhostToedlich(besitzerId);
            Color farbe = SpielerFarben.Farbe(spielerSlot);
            farbe.a = toedlich ? 1f : harmloseGhostDeckkraft;
            foreach (var sr in go.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr.CompareTag("TankBody") || sr.CompareTag("TankTop")) sr.color = farbe;
            }

            ghost.Play(lap, besitzerId, startZeit);
            ghostInstances.Add(ghost);

            // Limit pro Spieler: der aelteste Ghost desselben Besitzers verschwindet.
            int anzahl = 0;
            foreach (var g in ghostInstances) if (g != null && g.BesitzerId == besitzerId) anzahl++;
            if (anzahl > maxGhosts)
            {
                for (int n = 0; n < ghostInstances.Count; n++)
                {
                    var alt = ghostInstances[n];
                    if (alt != null && alt != ghost && alt.BesitzerId == besitzerId)
                    {
                        Destroy(alt.gameObject);
                        ghostInstances.RemoveAt(n);
                        break;
                    }
                }
            }
        }

        /// <summary>Entfernt alle Ghosts eines Spielers, z. B. wenn er ausgeschieden ist.</summary>
        public void GhostsEntfernen(ulong besitzerId)
        {
            for (int n = ghostInstances.Count - 1; n >= 0; n--)
            {
                var ghost = ghostInstances[n];
                if (ghost == null)
                {
                    ghostInstances.RemoveAt(n);
                }
                else if (ghost.BesitzerId == besitzerId)
                {
                    Destroy(ghost.gameObject);
                    ghostInstances.RemoveAt(n);
                }
            }
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

        // ------------------------------------------------------------------
        // Hilfen
        // ------------------------------------------------------------------

        private IEnumerator FadeText(TMP_Text textObject, float fadeDuration, float displayTime, GameObject associatedBar = null)
        {
            if (textObject == null) yield break;

            Color baseColor = textObject.color;
            Color startColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0);
            Color endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 1);

            textObject.gameObject.SetActive(true);
            float timer = 0;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                textObject.color = Color.Lerp(startColor, endColor, timer / fadeDuration);
                yield return null;
            }
            textObject.color = endColor;

            yield return new WaitForSeconds(displayTime);

            timer = 0;
            while (timer < fadeDuration)
            {
                timer += Time.deltaTime;
                textObject.color = Color.Lerp(endColor, startColor, timer / fadeDuration);
                yield return null;
            }
            textObject.color = startColor;
            textObject.gameObject.SetActive(false);

            if (associatedBar != null)
            {
                associatedBar.SetActive(false);
            }
        }

        private string FormatTime(float time)
        {
            System.TimeSpan timeSpan = System.TimeSpan.FromSeconds(Mathf.Abs(time));
            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}.{timeSpan.Milliseconds:D3}";
        }

        private IEnumerator DisableAfterSeconds(GameObject obj, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (obj != null)
            {
                obj.SetActive(false);
                obj.tag = "Untagged";
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
    }
}
