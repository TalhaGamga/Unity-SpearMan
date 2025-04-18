using Unity.Cinemachine;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine;

public class CameraFeedbackSystem : MonoBehaviour
{
    [SerializeField] private CinemachineCamera _camera;
    [SerializeField] private PostProcessVolume _volume;

    private readonly ICameraEffectSystem<CinemachineCamera> _cameraEffects = new CameraEffectSystem();
    private readonly ICameraEffectSystem<PostProcessVolume> _visualEffects = new CamPostProcessingEffectSystem();

    public void InjectCameraEffect(ICameraEffect<CinemachineCamera> effect)
    {
        _cameraEffects.Inject(effect);
    }

    public void InjectVisualEffect(ICameraEffect<PostProcessVolume> effect)
    {
        _visualEffects.Inject(effect);
    }

    private void LateUpdate()
    {
        _cameraEffects.Tick(_camera);
        _visualEffects.Tick(_volume);
    }
}
