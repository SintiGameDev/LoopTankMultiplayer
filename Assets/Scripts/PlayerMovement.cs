using UnityEngine;
using Unity.Netcode;

public class PlayerMovement : NetworkBehaviour
{
    [Header("Bewegung")]
    [SerializeField] private float bewegungsGeschwindigkeit = 5f;
    [SerializeField] private float sprintGeschwindigkeit = 9f;
    [SerializeField] private float sprungKraft = 5f;
    [SerializeField] private float schwerkraft = -20f;

    [Header("Boden-Erkennung")]
    [SerializeField] private Transform bodenPunkt;
    [SerializeField] private float bodenRadius = 0.25f;
    [SerializeField] private LayerMask bodenLayer;

    private CharacterController characterController;
    private Camera spielerKamera;
    private float vertikaleGeschwindigkeit = 0f;

    public override void OnNetworkSpawn()
    {
        characterController = GetComponent<CharacterController>();

        if (!IsOwner)
        {
            characterController.enabled = false;
            enabled = false;
            return;
        }

        // Kamera im eigenen Spieler-Objekt suchen
        spielerKamera = GetComponentInChildren<Camera>();

        if (spielerKamera == null)
            Debug.LogWarning("[PlayerMovement] Keine Kamera im Spieler gefunden.");

        characterController.enabled = true;
    }

    void Update()
    {
        if (!IsOwner) return;
        if (characterController == null) return;

        BewegungenVerarbeiten();
    }

    private void BewegungenVerarbeiten()
    {
        // Boden-Erkennung
        bool istAmBoden = characterController.isGrounded;
        if (istAmBoden && vertikaleGeschwindigkeit < 0f)
            vertikaleGeschwindigkeit = -2f;

        // Eingabe
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertikal   = Input.GetAxisRaw("Vertical");
        bool sprint      = Input.GetKey(KeyCode.LeftShift);
        float aktuelleGeschwindigkeit = sprint ? sprintGeschwindigkeit : bewegungsGeschwindigkeit;

        // Bewegungsrichtung relativ zur Kamera berechnen
        Vector3 richtung = Vector3.zero;

        if (spielerKamera != null)
        {
            // Kamera-Vorwaerts auf die horizontale Ebene projizieren
            Vector3 vorwaerts = spielerKamera.transform.forward;
            Vector3 rechts    = spielerKamera.transform.right;

            vorwaerts.y = 0f;
            rechts.y    = 0f;

            vorwaerts.Normalize();
            rechts.Normalize();

            richtung = vorwaerts * vertikal + rechts * horizontal;
        }
        else
        {
            // Fallback: Spieler-Transform verwenden
            richtung = transform.forward * vertikal + transform.right * horizontal;
        }

        // Diagonale Bewegung normalisieren
        if (richtung.magnitude > 1f)
            richtung.Normalize();

        // Sprung
        if (Input.GetKeyDown(KeyCode.Space) && istAmBoden)
            vertikaleGeschwindigkeit = Mathf.Sqrt(sprungKraft * -2f * schwerkraft);

        // Schwerkraft akkumulieren
        vertikaleGeschwindigkeit += schwerkraft * Time.deltaTime;

        // Finale Bewegung zusammensetzen
        Vector3 bewegung = richtung * aktuelleGeschwindigkeit;
        bewegung.y = vertikaleGeschwindigkeit;

        characterController.Move(bewegung * Time.deltaTime);
    }
}
