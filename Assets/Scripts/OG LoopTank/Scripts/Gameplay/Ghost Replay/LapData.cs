using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class LapFrame
{
    public float t;                 // Zeit seit Rundenstart (Sekunden)
    public Vector2 pos;
    public float rotZ;
}

[CreateAssetMenu(menuName = "Racing/LapData")]
public class LapData : ScriptableObject
{
    public List<LapFrame> frames = new List<LapFrame>();
    public float lapTime;

    public void Clear() { frames.Clear(); lapTime = 0f; }
}
