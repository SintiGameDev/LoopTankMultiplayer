using System.Collections;
using TMPro;
using TopDownRace;
using UnityEngine;

/// <summary>
/// Lebenszeit des lokalen Spielers. Jede Runde gibt Zeit dazu (siehe GameControl).
/// Laeuft sie ab, endet das Rennen fuer diesen Spieler.
/// </summary>
public class Timer : MonoBehaviour
{
    [Tooltip("Die Dauer des Timers in Sekunden, einstellbar im Inspector.")]
    [SerializeField]
    private float timerDuration = 5.0f;

    [Tooltip("Wird auf TRUE gesetzt, wenn der Timer abgelaufen ist, und auf FALSE, wenn er gestartet wird.")]
    public bool TimerEnd { get; private set; } = false;

    private Coroutine timerCoroutine;

    public TextMeshProUGUI timerTextUI;

    [Tooltip("Der Prefab, der zusammen mit dem Timer aktiviert werden soll.")]
    public GameObject prefabToActivate;

    private float m_CurrentTime;

    /// <summary>Verbleibende Zeit in Sekunden.</summary>
    public float Restzeit => m_CurrentTime;

    /// <summary>True, solange der Timer laeuft.</summary>
    public bool Laeuft => timerCoroutine != null;
    private int m_LastSecondDisplayed;

    [Header("LeanTween Scale Animation")]
    [Tooltip("Der Skalierungsfaktor für den Tween-Effekt.")]
    [SerializeField]
    private float m_ScaleFactor = 1.2f;
    [Tooltip("Die Dauer des Tween-Effekts in Sekunden.")]
    [SerializeField]
    private float m_TweenDuration = 0.2f;
    [Tooltip("Der Easing-Typ für den Tween-Effekt.")]
    [SerializeField]
    private LeanTweenType m_EaseType = LeanTweenType.easeOutCubic;

    void Awake()
    {
        if (timerTextUI == null) timerTextUI = GetComponent<TextMeshProUGUI>();
        m_CurrentTime = timerDuration;
        m_LastSecondDisplayed = Mathf.FloorToInt(m_CurrentTime % 60);
    }

    public void StartTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        if (timerTextUI != null)
        {
            timerTextUI.gameObject.SetActive(true);
            timerTextUI.transform.localScale = Vector3.one;
        }
        else
        {
            Debug.LogError("Timer-UI-Text-Referenz ist null. Timer kann nicht gestartet werden.");
            return;
        }

        if (prefabToActivate != null)
        {
            prefabToActivate.SetActive(true);
        }

        TimerEnd = false;
        m_CurrentTime = timerDuration;
        m_LastSecondDisplayed = Mathf.FloorToInt(m_CurrentTime % 60) + 1;

        timerCoroutine = StartCoroutine(RunTimer());
        Debug.Log($"Timer gestartet für {timerDuration} Sekunden.");

        UpdateTimerUI(m_CurrentTime);
    }

    private IEnumerator RunTimer()
    {
        while (m_CurrentTime > 0)
        {
            UpdateTimerUI(m_CurrentTime);
            yield return null;
            m_CurrentTime -= Time.deltaTime;
        }

        m_CurrentTime = 0;
        UpdateTimerUI(m_CurrentTime);
        TimerEnd = true;
        timerCoroutine = null;
        Debug.Log("Timer beendet!");

        if (prefabToActivate != null)
        {
            prefabToActivate.SetActive(false);
        }

        // Was das Zeitende bedeutet (Einzelspieler: geschafft, Multiplayer: raus), entscheidet die Rennleitung.
        if (GameControl.m_Current != null)
        {
            GameControl.m_Current.ZeitAbgelaufen();
        }
    }

    private void UpdateTimerUI(float timeToDisplay)
    {
        if (timerTextUI != null)
        {
            int minutes = Mathf.FloorToInt(timeToDisplay / 60);
            int seconds = Mathf.FloorToInt(timeToDisplay % 60);
            timerTextUI.text = string.Format("{0:00}:{1:00}", minutes, seconds);

            if (seconds != m_LastSecondDisplayed)
            {
                LeanTween.cancel(timerTextUI.gameObject);
                timerTextUI.transform.localScale = Vector3.one;
                LeanTween.scale(timerTextUI.gameObject, Vector3.one * m_ScaleFactor, m_TweenDuration)
                    .setEase(m_EaseType)
                    .setLoopPingPong(1);

                m_LastSecondDisplayed = seconds;
            }
        }
    }

    public void AddTime(float secondsToAdd)
    {
        if (timerCoroutine == null) return;

        m_CurrentTime += secondsToAdd;
        Debug.Log($"Timer: {secondsToAdd} Sekunden hinzugefügt. Neue Zeit: {m_CurrentTime:F2}s");
        UpdateTimerUI(m_CurrentTime);
    }

    /// <summary>Haelt den Timer an; die letzte Zeit bleibt stehen.</summary>
    public void StopTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
            Debug.Log("Timer gestoppt.");
        }
    }
}
