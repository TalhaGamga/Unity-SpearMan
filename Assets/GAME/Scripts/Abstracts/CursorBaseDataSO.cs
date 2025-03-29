using UnityEngine;

public class CursorBaseDataSO : ScriptableObject
{
    public Vector2 HotSpot => hotspot;

    [SerializeField] protected Vector2 hotspot;
}