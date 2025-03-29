using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/AimSystem")]
public class AimSystemSO : ScriptableObject, IAimSystem
{
    private Vector3 _aimDirection = Vector3.right;
    private Transform _weaponTransform;
    private Camera _mainCamera;
    private Vector3 _worldMousePos;
    private float _kickBack;
    private float _recoveryDelay;

    public void Init(Transform weaponTransform, Camera camera)
    {
        _weaponTransform = weaponTransform;
        _mainCamera = camera;
        _kickBack = 0;
        _recoveryDelay = 0;
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

    public void ApplyKickback(float strength, float recoveryDelay)
    {
        _kickBack = strength;
        _recoveryDelay = recoveryDelay;
    }

    public void Tick()
    {
        _kickBack = Mathf.Lerp(_kickBack, 0, _recoveryDelay * 0.05f);
    }
}