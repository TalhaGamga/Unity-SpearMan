using System;
using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/Weapon/Firearm/AimSystem")]
public class AimSystemSO : ScriptableObject, IAimSystem
{
    private Vector3 _aimDirection = Vector3.right;
    private Camera _mainCamera;
    private Vector3 _worldMousePos;
    private float _kickBack;
    private float _kickbackDelay;

    public event Action<Vector2> OnAiming;

    public void Init(Camera camera)
    {
        _mainCamera = camera;
        _kickBack = 0;
        _kickbackDelay = 0;
    }

    public void TakeAimInput(Vector2 aimTarget, Transform weapon)
    {
        float zDepth = Mathf.Abs(_mainCamera.transform.position.z - weapon.position.z);

        _worldMousePos = _mainCamera.ScreenToWorldPoint(new Vector3(aimTarget.x, aimTarget.y, zDepth));

        Vector2 weaponPos = new Vector2(weapon.position.x, weapon.position.y);

        _aimDirection = (_worldMousePos - (Vector3)weaponPos).normalized;
    }

    public void Tick()
    {
        OnAiming?.Invoke(_aimDirection);
    }
}