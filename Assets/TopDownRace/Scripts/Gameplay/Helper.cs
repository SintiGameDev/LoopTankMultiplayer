using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace TopDownRace
{
    public class Helper : MonoBehaviour
    {

        // Use this for initialization
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        public static Vector2 ToVector2(Vector3 v)
        {
            return (new Vector2(v.x, v.y));
        }

        public static Vector3 ToVector3(Vector2 v)
        {
            return (new Vector3(v.x, v.y, 0));
        }

        public static float HorizentalDistance(Vector3 v1, Vector3 v2)
        {
            v1.z = 0;
            v2.z = 0;
            return (Vector3.Distance(v1, v2));

        }

        public static Vector3 RotatedLenght(float angle, float lenght)
        {
            return (Quaternion.Euler(0, 0, angle) * (lenght * Vector3.right));
        }

        public static Vector3 RotatedVector(float angle, Vector3 vector)
        {
            return (Quaternion.Euler(0, 0, angle) * (vector));
        }
    }
}