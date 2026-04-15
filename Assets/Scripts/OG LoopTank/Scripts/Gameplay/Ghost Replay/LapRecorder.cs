using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class LapRecorder : MonoBehaviour
{
    public LapData currentLap;   // wird zur Laufzeit befüllt (instanziiert)
    public float sampleInterval = 0.02f; // 50 Hz; kann höher/niedriger

    Rigidbody2D rb;
    float t;
    float nextSample;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        // zur Sicherheit eine Laufzeit-Instanz erstellen (keine Asset-Verschmutzung)
        currentLap = ScriptableObject.CreateInstance<LapData>();
    }

    public void BeginLap()
    {
        if (currentLap == null)
            currentLap = ScriptableObject.CreateInstance<LapData>();
        currentLap.Clear();
        t = 0f; nextSample = 0f;
        enabled = true;
    }

    public void EndLap(float lapTime)
    {
        if (currentLap == null)
            currentLap = ScriptableObject.CreateInstance<LapData>();
        currentLap.lapTime = lapTime;
        enabled = false;
    }

    void OnEnable() { t = 0f; nextSample = 0f; }
    void OnDisable() { } // nichts

    void FixedUpdate()
    {
        t += Time.fixedDeltaTime;
        if (t < nextSample) return;
        
        currentLap.frames.Add(new LapFrame
        {
            t = t,
            pos = rb.position,
            rotZ = rb.rotation
        });

        nextSample += sampleInterval;
        //Debug.Log($"LapRecorder: Sampled frame at t={t:F2} pos={rb.position} rotZ={rb.rotation}");
    }
}
