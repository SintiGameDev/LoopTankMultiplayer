using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownRace
{
    public class CameraFollow : MonoBehaviour
    {
        private Vector3 m_Offset = new Vector3(0, 0, -10);
        public float m_SmoothTime;
        private Vector3 m_Velocity = Vector3.zero;

        [SerializeField]
        private Transform m_Target;

        // NEU: Methode zum Setzen des Ziels von einem anderen Skript aus
        public void SetTarget(Transform newTarget)
        {
            m_Target = newTarget;
        }

        void FixedUpdate()
        {
            // Führe eine Null-Prüfung durch, um den Fehler zu vermeiden
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