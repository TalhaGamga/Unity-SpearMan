using UnityEngine;

public class CameraManager : MonoBehaviour, ICameraManager
{
    public ICameraController CurrentController => _cameraController;
    private ICameraController _cameraController;

    private void Awake()
    {
        ServiceLocator.ForSceneOf(this).Register<ICameraManager>(this);
    }

    private void Update()
    {
        _cameraController?.Tick();
    }

    public void ApplyImpulse(Vector2 direction, float strength)
    {
    }

    public void SetController(ICameraController controller)
    {
    }

    public void SetZoom(float amount, float duration)
    {
    }

    public void Shake(float intensity, float duration)
    {
    }

    public void SetCameraController(ICameraController controller)
    {
        _cameraController = controller;
    }
}