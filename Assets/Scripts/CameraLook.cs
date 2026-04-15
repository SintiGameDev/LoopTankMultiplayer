using UnityEngine;
using Unity.Netcode;

public class CameraLook : NetworkBehaviour
{
    [Header("Maus")]
    [SerializeField] private float mausSensitivitaet = 2f;
    [SerializeField] private float maxBlickWinkelOben = 80f;
    [SerializeField] private float maxBlickWinkelUnten = 80f;

    private float vertikaleRotation = 0f;
    private Transform spielerKoerper;
    private Camera kamera;

    void Awake()
    {
        kamera = GetComponent<Camera>();

        // Kamera standardmaessig deaktivieren
        // Sie wird nur fuer den Owner eingeschaltet
        if (kamera != null)
            kamera.enabled = false;
    }

    public override void OnNetworkSpawn()
    {
        spielerKoerper = transform.parent;

        if (!IsOwner) return;

        // Nur die eigene Kamera einschalten
        if (kamera != null)
            kamera.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!IsOwner) return;

        float mausX = Input.GetAxis("Mouse X") * mausSensitivitaet;
        float mausY = Input.GetAxis("Mouse Y") * mausSensitivitaet;

        vertikaleRotation -= mausY;
        vertikaleRotation = Mathf.Clamp(vertikaleRotation,
                            -maxBlickWinkelOben, maxBlickWinkelUnten);
        transform.localRotation = Quaternion.Euler(vertikaleRotation, 0f, 0f);

        spielerKoerper.Rotate(Vector3.up * mausX);
    }
}
