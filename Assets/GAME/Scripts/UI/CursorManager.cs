using UnityEngine;

public class CursorManager : PersistentSingleton<CursorManager>
{
    [Header("Cursor Modules")]
    [SerializeField] private FirearmCursorHandler _firearmCursorHandler;

    private ITickable _cursorTicker;

    public void SetFirearmCursor(FirearmCursorDataSO cursorDataSO)
    {
        _firearmCursorHandler.SetCursorData(cursorDataSO);
        _cursorTicker = _firearmCursorHandler;
    }

    public void AnimateFiring(float recoilAmount, float recoveryTime)
    {
        _firearmCursorHandler.AnimateRecoil(recoilAmount, recoveryTime);
    }

    private void Update()
    {
        _cursorTicker.Tick();
    }
}
