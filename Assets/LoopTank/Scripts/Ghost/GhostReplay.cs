using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spielt eine aufgezeichnete Runde in Endlosschleife ab. Die Wiedergabe haengt an der
/// gemeinsamen Serverzeit, damit ein Ghost bei allen Spielern an derselben Stelle ist.
/// Funktioniert mit Rigidbody2D (2D-Szene, Ebene XY) und Rigidbody (3D-Szene, Ebene XZ).
/// </summary>
public class GhostReplay : MonoBehaviour
{
    public LapData source;
    public bool playing { get; private set; }

    /// <summary>Client-ID des Spielers, dessen Runde dieser Ghost nachfaehrt.</summary>
    public ulong BesitzerId { get; private set; }

    [Tooltip("Die Stärke des Rückstoßes, wenn der GhostTank von einer Kugel getroffen wird (nur 2D).")]
    public float bulletImpactForce = 50f;

    [Tooltip("Die Dauer in Sekunden, für die der GhostTank vom Replay abweicht, nachdem er getroffen wurde (nur 2D).")]
    public float impactDuration = 0.2f;

    private bool isInImpact = false;
    private float impactTimer = 0f;

    Rigidbody2D rb2D;
    Rigidbody rb3D;
    float hoehe;               // 3D: Y-Position, auf der der Ghost faehrt
    int i;                     // Index des naechsten Frames
    float t;                   // Replay-Zeit
    double startZeit = -1;     // Serverzeit des Starts; < 0 = lokale Zeit benutzen

    void Awake()
    {
        rb2D = GetComponent<Rigidbody2D>();
        rb3D = GetComponent<Rigidbody>();
        hoehe = transform.position.y;
    }

    /// <summary>Weltposition zu einem aufgezeichneten Punkt, passend zur Physik des Ghost-Prefabs.</summary>
    public static Vector3 WeltPosition(Vector2 ebenenPunkt, bool dreiD, float hoehe)
    {
        return dreiD ? new Vector3(ebenenPunkt.x, hoehe, ebenenPunkt.y) : new Vector3(ebenenPunkt.x, ebenenPunkt.y, 0f);
    }

    public static Quaternion WeltDrehung(float winkel, bool dreiD)
    {
        return dreiD ? Quaternion.Euler(0f, winkel, 0f) : Quaternion.Euler(0f, 0f, winkel);
    }

    public void Play(LapData lap, ulong besitzerId = 0, double startServerZeit = -1)
    {
        source = lap;
        BesitzerId = besitzerId;
        startZeit = startServerZeit;
        if (source == null || source.frames.Count < 2) { playing = false; return; }

        i = 1;
        t = 0f;
        Setzen(source.frames[0].pos, source.frames[0].rotZ, true);
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

    private void Setzen(Vector2 pos, float winkel, bool sofort)
    {
        if (rb2D != null)
        {
            if (sofort) { rb2D.position = pos; rb2D.rotation = winkel; }
            else { rb2D.MovePosition(pos); rb2D.MoveRotation(winkel); }
        }
        else if (rb3D != null)
        {
            // Aufgezeichnet ist nur die Lage in der Ebene. Die Hoehe kommt von der Strecke; die
            // bisherige Hoehe entscheidet an einer Bruecke, ob der Ghost oben oder unten faehrt.
            var strecke = TopDownRace.StreckenGenerator.Aktiv;
            if (strecke != null && strecke.BodenHoehe(WeltPosition(pos, true, hoehe), out float boden, out _)) hoehe = boden;

            Vector3 welt = WeltPosition(pos, true, hoehe);
            Quaternion drehung = WeltDrehung(winkel, true);
            if (sofort) { rb3D.position = welt; rb3D.rotation = drehung; transform.SetPositionAndRotation(welt, drehung); }
            else { rb3D.MovePosition(welt); rb3D.MoveRotation(drehung); }
        }
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
            Setzen(source.frames[0].pos, source.frames[0].rotZ, true);
            return;
        }

        while (i < source.frames.Count - 1 && source.frames[i].t < t) i++;

        var a = source.frames[i - 1];
        var b = source.frames[i];

        float seg = Mathf.InverseLerp(a.t, b.t, t);
        Setzen(Vector2.Lerp(a.pos, b.pos, seg), Mathf.LerpAngle(a.rotZ, b.rotZ, seg), false);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (rb2D != null && collision.gameObject.CompareTag("Bullet"))
        {
            if (isInImpact) return;

            isInImpact = true;
            impactTimer = impactDuration;

            Vector2 pushDirection = collision.contacts[0].normal;
            rb2D.AddForce(pushDirection * bulletImpactForce, ForceMode2D.Impulse);

            Destroy(collision.gameObject);
        }
    }
}
