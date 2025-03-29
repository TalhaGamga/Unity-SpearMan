using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/AimSystem")]
public class AimSystemSO : ScriptableObject, IAimSystem
{
    private Vector3 _aimDirection = Vector3.right;
    private Transform _weaponTransform;
    private Camera _mainCamera;
    private Vector3 _worldMousePos;
    private float _kickBack;
    private float _kickbackDelay;

    public void Init(Transform weaponTransform, Camera camera)
    {
        _weaponTransform = weaponTransform;
        _mainCamera = camera;
        _kickBack = 0;
        _kickbackDelay = 0;
    }

    public void UpdateAim(Vector2 aimTarget)
    {
        float zDepth = Mathf.Abs(_mainCamera.transform.position.z - _weaponTransform.position.z);

        _worldMousePos = _mainCamera.ScreenToWorldPoint(new Vector3(aimTarget.x, aimTarget.y, zDepth));

        Vector2 weaponPos = new Vector2(_weaponTransform.position.x, _weaponTransform.position.y);

        Vector2 direction = (_worldMousePos - (Vector3)weaponPos).normalized;

        _aimDirection = direction;
    }

    public Quaternion GetAimRotation()
    {
        float angle = Mathf.Atan2(_aimDirection.y + _kickBack, _aimDirection.x) * Mathf.Rad2Deg;

        return Quaternion.Euler(0, 0, angle);
    }

    public void ApplyKickback(float strength, float kickbackDelay)
    {
        _kickBack = strength;
        _kickbackDelay = kickbackDelay;
    }

    public void RecoveryKickback()
    {
        _kickBack = Mathf.Lerp(_kickBack, 0, _kickbackDelay * 0.05f);
    }
}