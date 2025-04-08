using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/RecoilSystem")]
public class RecoilSystemSO : ScriptableObject, IRecoilSystem
{
    public event Action<float, float> OnKickback;

    private float _lastTimeFired = 0f;
    private float _recoilTimer = 0f;
    private float _currentRecoil = 0f;

    [SerializeField] private float _incrementRate = 0.1f;
    [SerializeField] private float _maxRecoil = 0.5f;
    [SerializeField] private float _shakeDuration = 0.08f;
    [SerializeField] private float _initialPunch = 0.2f;

    private float _recoveryDelay;
    private bool _isFirstShot = true;

    public void Init()
    {
        _lastTimeFired = 0f;
        _recoilTimer = 0f;
        _currentRecoil = 0f;
        _recoilTimer = 0f;
        _recoveryDelay = 0f;
        _isFirstShot = true;
    }

    public void KickBack()
    {
        float timeSinceLastShot = Time.time - _lastTimeFired;
        _lastTimeFired = Time.time;

        if (Time.time - _recoilTimer < 0.1f) return;
        _recoilTimer = Time.time;

        float frequencyFactor = Mathf.Clamp01(1f - (timeSinceLastShot / 0.5f));

        if (_isFirstShot || timeSinceLastShot > 0.3f)
        {
            _currentRecoil = _initialPunch;
            _isFirstShot = false;
        }

        else
        {
            _currentRecoil += _incrementRate * frequencyFactor;
            _currentRecoil = Mathf.Clamp(_currentRecoil, 0f, _maxRecoil);
        }

        float recoilStrength = Mathf.Lerp(0.1f, _maxRecoil, (_currentRecoil / _maxRecoil) * 0.01f);
        float punchStrength = recoilStrength * 0.5f;

        _recoveryDelay = Mathf.Lerp(0.1f, 0.6f, frequencyFactor);

        OnKickback?.Invoke(recoilStrength, _recoveryDelay);
    }
}