using UnityEngine;
using System; // Notwendig für Action

public class Score : MonoBehaviour
{
    // Statische Instanz des Score-Skripts, für einfachen Zugriff von überall.
    public static Score Instance { get; private set; }

    // Dies speichert den aktuellen Punktestand.
    // [SerializeField] macht es im Inspector sichtbar, obwohl es private ist (gute Praxis).
    [SerializeField]
    private int currentScore = 0;

    // Eine Property, um den Punktestand von anderen Skripten aus lesbar zu machen.
    // Der Setter ist privat, damit der Punktestand nur über die AddScore-Methode geändert werden kann.
    public int CurrentScore
    {
        get { return currentScore; }
        private set
        {
            currentScore = value;
            // Löse das Event aus, wenn sich der Punktestand ändert.
            // Das ist super nützlich, wenn du z.B. eine UI hast, die den Score anzeigt.
            OnScoreChanged?.Invoke(currentScore);
        }
    }

    // Ein Event, das ausgelöst wird, wenn sich der Punktestand ändert.
    // Andere Skripte können sich hier registrieren, um auf Änderungen zu reagieren (z.B. UI-Aktualisierung).
    public static event Action<int> OnScoreChanged;

    void Awake()
    {
        // Sicherstellen, dass nur eine Instanz dieses Score-Managers existiert.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject); // Zerstöre doppelte Instanzen
        }
        else
        {
            Instance = this; // Setze diese Instanz als die globale Instanz
            // Optional: Wenn du den Score über Szenenwechsel hinweg beibehalten möchtest
            // DontDestroyOnLoad(gameObject); 
        }

        // Setze den Punktestand beim Start auf 0.
        // Die Property wird verwendet, damit das OnScoreChanged-Event auch beim Initialisieren ausgelöst wird.
        CurrentScore = 0;
    }

    /// <summary>
    /// Erhöht den Punktestand um den angegebenen Wert.
    /// Dies ist die Methode, die du aufrufen solltest, wenn ein Ghost erfolgreich gespawnt wird.
    /// </summary>
    /// <param name="amount">Der Wert, um den der Punktestand erhöht werden soll. Standard ist 1.</param>
    public void AddScore(int amount = 1)
    {
        // Der Setter der CurrentScore-Property wird verwendet, 
        // der automatisch das OnScoreChanged-Event auslöst.
        CurrentScore += amount;
        Debug.Log($"Score erhöht! Neuer Punktestand: {CurrentScore}");
    }

    /// <summary>
    /// Setzt den Punktestand auf 0 zurück.
    /// Nützlich am Anfang eines neuen Spiels oder einer neuen Runde.
    /// </summary>
    public void ResetScore()
    {
        // Der Setter der CurrentScore-Property wird verwendet.
        CurrentScore = 0;
        Debug.Log("Punktestand zurückgesetzt.");
    }

    // Beispielnutzung für Debugging (kann entfernt werden)
    void Update()
    {
        // Nur zum Testen: Drücke 'S', um den Score zu erhöhen
        // und 'R', um ihn zurückzusetzen.
        if (Input.GetKeyDown(KeyCode.S))
        {
            AddScore(); // Fügt 1 Punkt hinzu
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetScore();
        }
    }
}