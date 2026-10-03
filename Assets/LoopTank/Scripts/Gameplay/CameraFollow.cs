using UnityEngine;

namespace TopDownRace
{
    /// <summary>Kamera der 2D-Rennszene: schaut von oben und eilt dem Panzer etwas voraus.</summary>
    public class CameraFollow : MonoBehaviour
    {
        private Vector3 m_Offset = new Vector3(0, 0, -10);
        public float m_SmoothTime;
        private Vector3 m_Velocity = Vector3.zero;

        [SerializeField]
        protected Transform m_Target;

        protected virtual void Awake()
        {
            // Ein im Inspector eingetragenes Prefab ist kein Objekt der Szene, dem man folgen koennte.
            if (m_Target != null && !m_Target.gameObject.scene.IsValid()) m_Target = null;
        }

        /// <summary>Setzt das Ziel; wird von der Rennleitung aufgerufen, sobald die Panzer gespawnt sind.</summary>
        public virtual void SetTarget(Transform newTarget)
        {
            m_Target = newTarget;
        }

        protected virtual void FixedUpdate()
        {
            if (m_Target == null)
            {
                return;
            }

            Vector3 targetPosition = m_Target.position + m_Offset;
            Vector3 forwardOffset = 20.0f * (m_Target.rotation * Vector3.right);

            transform.position = Vector3.SmoothDamp(transform.position, targetPosition + forwardOffset, ref m_Velocity, m_SmoothTime);
        }
    }
}
