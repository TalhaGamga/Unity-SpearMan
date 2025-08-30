using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

[DefaultExecutionOrder(-100)]
public class CameraManager : MonoBehaviour
{
    [Header("Defaults (used by Shake())")]
    [SerializeField] private float defaultAmplitude = 1.6f;
    [SerializeField] private float defaultFrequency = 2.5f;
    [SerializeField] private float defaultDuration = 0.10f;
    [SerializeField] private NoiseSettings defaultNoise; // assign a NoiseSettings asset (e.g., Handheld_Wide)

    private static CameraManager _instance;
    private Coroutine _shakeCo;

    void OnEnable()
    {
        _instance = this;
        var p = GetOrCreatePerlin();
        if (p != null) { p.AmplitudeGain = 0f; p.FrequencyGain = 0f; }
    }
    void OnDisable() { if (_instance == this) _instance = null; }

    public static void Shake() =>
        _instance?.StartShake(_instance.defaultAmplitude, _instance.defaultFrequency, _instance.defaultDuration);

    /// <summary> Static: quick shake with custom values. Typical: (1.8f, 3f, 0.1f) </summary>
    public static void Shake(float amplitude, float frequency, float duration) =>
        _instance?.StartShake(amplitude, frequency, duration);

    // -------- internals --------
    private void StartShake(float amp, float freq, float dur)
    {
        var perlin = GetOrCreatePerlin();
        if (perlin == null)
        {
            Debug.LogWarning("[CameraManager] No Cinemachine camera found to shake.");
            return;
        }

        if (perlin.NoiseProfile == null && defaultNoise != null)
            perlin.NoiseProfile = defaultNoise;

        if (perlin.NoiseProfile == null)
        {
            Debug.LogWarning("[CameraManager] Perlin has no NoiseProfile. Assign one on the VCam or in CameraManager.");
            return;
        }

        if (_shakeCo != null) StopCoroutine(_shakeCo);
        _shakeCo = StartCoroutine(ShakeRoutine(perlin, amp, freq, dur));
    }

    private IEnumerator ShakeRoutine(CinemachineBasicMultiChannelPerlin perlin,
                                    float amp, float freq, float dur)
    {
        // Start burst
        perlin.AmplitudeGain = amp;
        perlin.FrequencyGain = freq;

        yield return new WaitForSeconds(dur);

        // Back to silent idle
        if (perlin != null)
        {
            perlin.AmplitudeGain = 0f;
            perlin.FrequencyGain = 0f;
        }
        _shakeCo = null;
    }


    private CinemachineBasicMultiChannelPerlin GetOrCreatePerlin()
    {
        // 1) If there’s already a Perlin component on any active Cinemachine camera, use it.
        var existing = FindFirstObjectByType<CinemachineBasicMultiChannelPerlin>();
        if (existing != null) return existing;

        // 2) Otherwise try to add it to the first Cinemachine Virtual Camera we find.
        var vcam = FindFirstObjectByType<CinemachineVirtualCamera>();
        if (vcam != null)
        {
            var p = vcam.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
            return p ?? vcam.AddCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
        }

        // (Optional) If you’re using the new CinemachineCamera (v3), try that too:
#if UNITY_CINEMACHINE_3_1_OR_NEWER
        var cmCam = FindFirstObjectByType<CinemachineCamera>();
        if (cmCam != null)
        {
            var p = cmCam.GetComponent<CinemachineBasicMultiChannelPerlin>();
            // In v3, Perlin is a pipeline component; safest is to add via the vcam API when available.
            return p; // if null, add Perlin to your Cinemachine camera in the inspector once.
        }
#endif
        return null;
    }
}
