using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spielt eine aufgezeichnete Runde in Endlosschleife ab. Die Wiedergabe haengt an der
/// gemeinsamen Serverzeit, damit ein Ghost bei allen Spielern an derselben Stelle ist.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class GhostReplay : MonoBehaviour
{
    public LapData source;
    public bool playing { get; private set; }

    /// <summary>Client-ID des Spielers, dessen Runde dieser Ghost nachfaehrt.</summary>
    public ulong BesitzerId { get; private set; }

    [Tooltip("Die Stärke des Rückstoßes, wenn der GhostTank von einer Kugel getroffen wird.")]
    public float bulletImpactForce = 50f;

    [Tooltip("Die Dauer in Sekunden, für die der GhostTank vom Replay abweicht, nachdem er getroffen wurde.")]
    public float impactDuration = 0.2f;

    private bool isInImpact = false;
    private float impactTimer = 0f;

    Rigidbody2D rb;
    int i;                     // Index des naechsten Frames
    float t;                   // Replay-Zeit
    double startZeit = -1;     // Serverzeit des Starts; < 0 = lokale Zeit benutzen

    void Awake() { rb = GetComponent<Rigidbody2D>(); }

    public void Play(LapData lap, ulong besitzerId = 0, double startServerZeit = -1)
    {
        source = lap;
        BesitzerId = besitzerId;
        startZeit = startServerZeit;
        if (source == null || source.frames.Count < 2) { playing = false; return; }

        i = 1;
        t = 0f;
        var f0 = source.frames[0];
        rb.position = f0.pos;
        rb.rotation = f0.rotZ;
        playing = true;
        gameObject.SetActive(true);
        isInImpact = false;
    }

    public void Stop()
    {
        playing = false;
        gameObject.SetActive(false);
        isInImpact = false;
    }

    void FixedUpdate()
    {
        if (!playing) return;

        if (isInImpact)
        {
            // Im Impact-Modus bewegt die Physik den Ghost, danach geht das Replay normal weiter.
            impactTimer -= Time.fixedDeltaTime;
            if (impactTimer <= 0f) isInImpact = false;
            return;
        }

        float dauer = source.frames[source.frames.Count - 1].t;
        if (dauer <= 0f) return;

        float vorher = t;
        var nm = NetworkManager.Singleton;
        if (startZeit >= 0 && nm != null && nm.IsListening)
        {
            double seitStart = nm.ServerTime.Time - startZeit;
            if (seitStart < 0) return;   // wartet am Startpunkt, bis er losfaehrt
            t = (float)(seitStart % dauer);
        }
        else
        {
            t += Time.fixedDeltaTime;
            if (t >= dauer) t -= dauer;
        }

        // Neue Schleife: hart an den Anfang setzen statt quer ueber die Strecke zu gleiten.
        if (t < vorher)
        {
            i = 1;
            var f0 = source.frames[0];
            rb.position = f0.pos;
            rb.rotation = f0.rotZ;
            return;
        }

        while (i < source.frames.Count - 1 && source.frames[i].t < t) i++;

        var a = source.frames[i - 1];
        var b = source.frames[i];

        float seg = Mathf.InverseLerp(a.t, b.t, t);
        Vector2 pos = Vector2.Lerp(a.pos, b.pos, seg);
        float rot = Mathf.LerpAngle(a.rotZ, b.rotZ, seg);

        rb.MovePosition(pos);
        rb.MoveRotation(rot);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Bullet"))
        {
            if (isInImpact) return;

            isInImpact = true;
            impactTimer = impactDuration;

            Vector2 pushDirection = collision.contacts[0].normal;
            rb.AddForce(pushDirection * bulletImpactForce, ForceMode2D.Impulse);

            Destroy(collision.gameObject);
        }
    }
}
