using UnityEngine;

/// <summary>
/// Zeichnet die Fahrt des eigenen Panzers auf. Gespeichert wird immer eine Position auf der
/// Fahr-Ebene plus ein Drehwinkel: in 2D (x, y) und die Z-Drehung, in 3D (x, z) und die Y-Drehung.
/// GhostReplay spielt die Daten mit derselben Zuordnung wieder ab.
/// </summary>
public class LapRecorder : MonoBehaviour
{
    public LapData currentLap;   // wird zur Laufzeit befuellt (instanziiert)
    public float sampleInterval = 0.02f; // 50 Hz

    Rigidbody2D rb2D;
    Rigidbody rb3D;
    float t;
    float nextSample;

    void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        rb3D = GetComponent<Rigidbody>();
        // Laufzeit-Instanz erstellen (keine Asset-Verschmutzung)
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

    void FixedUpdate()
    {
        t += Time.fixedDeltaTime;
        if (t < nextSample) return;

        Vector2 pos;
        float winkel;
        if (rb2D != null)
        {
            pos = rb2D.position;
            winkel = rb2D.rotation;
        }
        else if (rb3D != null)
        {
            pos = new Vector2(rb3D.position.x, rb3D.position.z);
            winkel = rb3D.rotation.eulerAngles.y;
        }
        else
        {
            return;
        }

        currentLap.frames.Add(new LapFrame { t = t, pos = pos, rotZ = winkel });
        nextSample += sampleInterval;
    }
}
