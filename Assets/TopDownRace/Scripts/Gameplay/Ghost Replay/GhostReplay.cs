using UnityEngine;
using System.Collections; // Füge dies hinzu, wenn du Coroutinen wie WaitForSeconds verwendest

[RequireComponent(typeof(Rigidbody2D))]
public class GhostReplay : MonoBehaviour
{
    public LapData source;       // zugewiesen bei Start
    public bool playing { get; private set; }

    Rigidbody2D rb;
    int i;                     // Index des nächsten Frames
    float t;                   // Replay-Zeit

    // NEU: Stärke des Rückstoßes
    [Tooltip("Die Stärke des Rückstoßes, wenn der GhostTank von einer Kugel getroffen wird.")]
    public float bulletImpactForce = 50f;

    // NEU: Dauer des Rückstoßes / wie lange er von der Replay-Bahn abweicht
    [Tooltip("Die Dauer in Sekunden, für die der GhostTank vom Replay abweicht, nachdem er getroffen wurde.")]
    public float impactDuration = 0.2f; // Z.B. 0.2 Sekunden

    // NEU: Variable, um den Impact-Zustand zu verfolgen
    private bool isInImpact = false;
    private float impactTimer = 0f;

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    public void Play(LapData lap)
    {
        source = lap;
        if (source == null || source.frames.Count < 2) { playing = false; return; }

        // Initialisiere das Replay. Hier wird die Position und Rotation gesetzt.
        i = 1;
        t = 0f;
        var f0 = source.frames[0];
        rb.position = f0.pos;
        rb.rotation = f0.rotZ;
        playing = true;
        gameObject.SetActive(true);
        isInImpact = false; // Sicherstellen, dass der Impact-Zustand beim Start zurückgesetzt wird
    }

    public void Stop()
    {
        playing = false;
        gameObject.SetActive(false);
        isInImpact = false; // Impact-Zustand auch beim Stoppen zurücksetzen
    }

    void FixedUpdate()
    {
        if (!playing) return;

        if (isInImpact)
        {
            // Wenn im Impact-Modus, lass die Physik den Tank bewegen.
            // Zähle den Timer herunter
            impactTimer -= Time.fixedDeltaTime;
            if (impactTimer <= 0f)
            {
                // Impact-Dauer ist abgelaufen, kehre zum Replay zurück.
                // Wir müssen den nächsten Replay-Frame finden, der dem aktuellen Zeitpunkt 't' am nächsten liegt,
                // und die aktuelle Position des Rigidbody als neuen Startpunkt für das Replay setzen,
                // um einen Ruck zu vermeiden.
                isInImpact = false;

                // Finden Sie den Frame, der am besten zu unserer aktuellen Replay-Zeit passt
                // (wir setzen 't' hier nicht zurück, damit das Replay an der richtigen Stelle fortgesetzt wird)
                while (i < source.frames.Count && source.frames[i].t < t) i++;
                if (i >= source.frames.Count) // Falls wir über das Ende hinaus sind (unwahrscheinlich nach Looping-Check)
                {
                    t = 0f;
                    i = 1;
                }
                // Setze die Replay-Startposition auf die aktuelle Position des GhostTanks,
                // um einen Sprung zu vermeiden.
                // Wichtig: Das LapData.Frame müsste hier aktualisiert werden oder wir ignorieren einfach den ersten Frame
                // und lassen das Replay von der aktuellen Position aus interpolieren.
                // Für dieses Szenario lassen wir das Replay einfach von der aktuellen Zeit 't' aus weiterlaufen.
                // Die nächste Interpolation wird dann von 'a' zu 'b' gehen, wo 'a' ein früherer Frame ist.
                // Das kann einen kleinen Sprung verursachen, wenn die aktuelle Position weit vom interpolierten 'a' entfernt ist.
                // Eine bessere Lösung wäre, LapData anzupassen oder den Replay-Index 'i' zurückzusetzen und von der aktuellen Position zu interpolieren.
                // Für "fährt weiter ganz normal" ist es am einfachsten, die Zeit und den Index beizubehalten.
            }
            // Beende FixedUpdate, damit das Replay nicht sofort wieder überschreibt
            return;
        }

        t += Time.fixedDeltaTime;

        // ENDE? Dann Loopen!
        if (t >= source.frames[^1].t)
        {
            t = 0f;
            i = 1;
            var f0 = source.frames[0];
            rb.position = f0.pos;
            rb.rotation = f0.rotZ;
            return;
        }

        while (i < source.frames.Count && source.frames[i].t < t) i++;

        var a = source.frames[i - 1];
        var b = source.frames[i];

        float seg = Mathf.InverseLerp(a.t, b.t, t);
        Vector2 pos = Vector2.Lerp(a.pos, b.pos, seg);
        float rot = Mathf.LerpAngle(a.rotZ, b.rotZ, seg);

        rb.MovePosition(pos);
        rb.MoveRotation(rot);
    }

    // NEU: Kollisionserkennung für Kugeln
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Prüfen, ob das kollidierende Objekt den Tag "Bullet" hat
        if (collision.gameObject.CompareTag("Bullet"))
        {
            // Debug-Meldung, um zu sehen, ob die Kollision erkannt wird
            Debug.Log("GhostTank wurde von Kugel getroffen!", this);

            // Verhindere Mehrfachstöße, wenn bereits ein Impact aktiv ist
            if (isInImpact) return;

            isInImpact = true;
            impactTimer = impactDuration; // Setze den Timer für die Impact-Dauer

            // Berechne die Richtung weg von der Kugel
            // Die Kugel trifft den GhostTank. Wir wollen den GhostTank wegstoßen.
            // Die Normale des Kontakts zeigt aus dem Kollisionspunkt heraus.
            Vector2 pushDirection = collision.contacts[0].normal;

            // Wende die Kraft an
            // Wir verwenden ForceMode2D.Impulse für einen sofortigen Stoß
            rb.AddForce(pushDirection * bulletImpactForce, ForceMode2D.Impulse);

            // Optional: Zerstöre die Kugel, wenn sie den GhostTank trifft
            Destroy(collision.gameObject);
        }
    }
}