using UnityEngine;
using System.Collections;
namespace TopDownRace
{
    public class InvisibaleAtStart : MonoBehaviour
    {

        // Use this for initialization
        void Start()
        {
            GetComponent<Renderer>().enabled = false;
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}