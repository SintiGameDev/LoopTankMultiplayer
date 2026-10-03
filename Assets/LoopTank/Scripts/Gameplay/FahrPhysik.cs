using UnityEngine;

namespace TopDownRace
{
    /// <summary>
    /// Gemeinsame Schnittstelle der Fahrphysik. PlayerCar kennt nur diese Klasse und weiss nicht,
    /// ob darunter 2D-Physik (CarPhysics, Ebene XY) oder 3D-Physik (CarPhysics3D, Ebene XZ) laeuft.
    /// </summary>
    public abstract class FahrPhysik : MonoBehaviour
    {
        [HideInInspector]
        public float m_InputAccelerate = 0;
        [HideInInspector]
        public float m_InputSteer = 0;

        /// <summary>Rohe Eingabe -1..1 (vorwaerts positiv). Bleibt stehen, bis PlayerCar sie neu setzt.</summary>
        [HideInInspector]
        public float m_Gas = 0;
        /// <summary>Rohe Eingabe -1..1 (rechts positiv).</summary>
        [HideInInspector]
        public float m_Lenkung = 0;

        /// <summary>True, solange die Drift-Taste gehalten wird. Wirkt nur, wenn die Physik driften kann.</summary>
        [HideInInspector]
        public bool m_Drift = false;

        /// <summary>Ob diese Physik einen Drift kennt. Dann liegt er auf der Leertaste und der Boost auf Shift.</summary>
        public virtual bool KannDriften => false;

        [Tooltip("Die Kraft, mit der der Panzer beschleunigt.")]
        public float m_SpeedForce = 80;

        /// <summary>Aktuelle Geschwindigkeit in Einheiten pro Sekunde.</summary>
        public abstract float Tempo { get; }

        /// <summary>Bringt den Panzer sofort zum Stehen.</summary>
        public abstract void Anhalten();

        /// <summary>Euler-Drehung fuer den Turm: grad &gt; 0 dreht von oben gesehen im Uhrzeigersinn.</summary>
        public abstract Vector3 TurmDrehung(float grad);
    }
}
