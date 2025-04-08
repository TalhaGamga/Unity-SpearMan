using UnityEngine;

public class FirearmCameraController : ICameraController
{
    public void OnActivate()
    {
    }

    public void OnDeactivate()
    {
    }
    public void Tick()
    {
        Debug.Log("FirearmCameraController Tick");
    }
    public void ApplyImpulse(Vector2 direction, float strength)
    {
    }

    public void SetZoom(float amount, float duration)
    {
    }

    public void Shake(float intensity, float duration)
    {
    }
}