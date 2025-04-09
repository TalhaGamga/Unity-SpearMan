using UnityEngine;

public class CamMidPointAnchor : MonoBehaviour
{
    public Transform TargetA;
    public Transform TargetB;

    private void Update()
    {
        if (TargetA && TargetB)
        {
            transform.position = Vector3.Lerp(TargetA.position, TargetB.position, 0.5f);
        }
    }
}