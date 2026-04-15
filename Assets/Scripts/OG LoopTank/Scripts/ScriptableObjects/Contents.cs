using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopDownRace.ScriptableObjects
{
    [CreateAssetMenu(fileName = "Contents", menuName = "CustomObjects/Contents", order = 1)]
    public class Contents : ScriptableObject
    {
        //public Weapon[] m_Weapons;
        public GameObject[] m_LevelPrefabs;

    }
}
