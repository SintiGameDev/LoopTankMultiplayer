using System.Collections;
using TMPro;
using TopDownRace;
using UnityEngine;
using System.Collections.Generic;

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
        // Finde das Timer-Text-Objekt in der Szene und weise es zu
        GameObject timerTextObject = GameObject.FindGameObjectWithTag("TimerText"); // WICHTIG: Prüfe, ob dein Tag "Timer" oder "TimerText" ist!
        if (timerTextObject != null)
        {
            timerTextUI = timerTextObject.GetComponent<TextMeshProUGUI>();
            if (timerTextUI == null)
            {
                Debug.LogError("GameObject mit Tag 'TimerText' gefunden, aber es hat keine TextMeshProUGUI-Komponente.", timerTextObject);
            }
            else
            {
                // Deaktiviere das UI-Element, bis der Timer startet
                timerTextUI.gameObject.SetActive(false);
                m_CurrentTime = timerDuration;
                timerTextUI.text = string.Format("{0:00}:{1:00}", Mathf.FloorToInt(m_CurrentTime / 60), Mathf.FloorToInt(m_CurrentTime % 60));
                m_LastSecondDisplayed = Mathf.FloorToInt(m_CurrentTime % 60);
                Debug.Log("Timer-UI-Text-Referenz wurde erfolgreich gesetzt.");
            }
        }
        else
        {
            Debug.LogError("Kein GameObject mit dem Tag 'TimerText' in der Szene gefunden. Der Timer-Text kann nicht aktualisiert werden.");
        }

        if (prefabToActivate != null)
        {
            prefabToActivate.SetActive(false);
        }
    }

    public void StartTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        // Stelle sicher, dass der Text nicht null ist, bevor du ihn aktivierst
        if (timerTextUI != null)
        {
            timerTextUI.gameObject.SetActive(true); // Aktiviere den Text
            timerTextUI.transform.localScale = Vector3.one;
        }
        else
        {
            Debug.LogError("Timer-UI-Text-Referenz ist null. Timer kann nicht gestartet werden.");
            return; // Beende die Methode, um einen NullReferenceException zu vermeiden
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
        Debug.Log("Timer beendet!");

        if (PlayerCar.m_Current != null)
        {
            PlayerCar.m_Current.m_Control = false;
        }

        if (UISystem.FindOpenUIByName("win-ui") == null)
        {
            UISystem.ShowUI("win-ui");
        }

        if (GameControl.m_Current != null)
        {
            GameControl.m_Current.m_WonRace = true;
        }
        else
        {
            Debug.LogError("GameControl.m_Current is null. Cannot set m_WonRace.");
        }

        if (prefabToActivate != null)
        {
            prefabToActivate.SetActive(false);
        }
    }

    private void UpdateTimerUI(float timeToDisplay)
    {
        if (UISystem.FindOpenUIByName("lose-ui") != null)
        {
            StopTimer();
            if (timerTextUI != null) timerTextUI.gameObject.SetActive(false);
            if (prefabToActivate != null) prefabToActivate.SetActive(false);
            return;
        }

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
        m_CurrentTime += secondsToAdd;
        timerDuration += secondsToAdd;
        Debug.Log($"Timer: {secondsToAdd} Sekunden hinzugefügt. Neue Zeit: {m_CurrentTime:F2}s");
        UpdateTimerUI(m_CurrentTime);
    }

    public void StopTimer()
    {
        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
            Debug.Log("Timer manuell gestoppt.");
            if (timerTextUI != null) timerTextUI.gameObject.SetActive(false);
            if (prefabToActivate != null) prefabToActivate.SetActive(false);
        }
    }
}