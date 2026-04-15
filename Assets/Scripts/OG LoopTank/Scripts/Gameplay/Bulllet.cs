using UnityEngine;

public class Bullet : MonoBehaviour
{
    private Rigidbody2D rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogError("Bullet-Script ben�tigt eine Rigidbody2D-Komponente auf demselben GameObject!", this);
            enabled = false; // Deaktiviere das Skript, wenn kein Rigidbody2D gefunden wird
        }
    }

    void Update()
    {
        // Stelle sicher, dass wir eine Rigidbody2D haben und sich die Kugel bewegt
        if (rb != null && rb.linearVelocity.magnitude > 0.01f)
        {
            // Berechne den Winkel in Grad basierend auf der Bewegungsrichtung
            // Mathf.Atan2 gibt den Winkel in Radianten zwischen der X-Achse und dem Vektor zur�ck
            // Wir wandeln Radianten in Grad um und addieren/subtrahieren 90,
            // da die "obere" Seite eines 2D-Sprites (Vector2.up) oft als "vorne" betrachtet wird.
            float angle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;

            // Setze die Z-Rotation des GameObjects auf diesen Winkel.
            // Eventuell -90f oder +90f n�tig, je nach Standardausrichtung deines Bullet-Sprites.
            // Wenn dein Sprite nach rechts (X-Achse) ausgerichtet ist, ist es nur 'angle'.
            // Wenn dein Sprite nach oben (Y-Achse) ausgerichtet ist, ist es 'angle - 90f'.
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // Optional: F�ge hier Logik f�r Kollisionen hinzu, falls die Kugel Schaden anrichten oder zerst�rt werden soll
    // private void OnTriggerEnter2D(Collider2D other)
    // {
    //     // Beispiel: Kugel verschwindet bei Kollision mit etwas anderem als dem Spieler
    //     if (other.gameObject.tag != "PlayerCar") // Annahme, dein Spieler hat den Tag "PlayerCar"
    //     // und vermeidet, dass die Kugel sofort mit dem Spieler kollidiert, wenn sie gespawnt wird
    //     {
    //         Debug.Log("Kugel kollidiert mit: " + other.name);
    //         Destroy(gameObject); // Zerst�re die Kugel bei Kollision
    //     }
    // }
}