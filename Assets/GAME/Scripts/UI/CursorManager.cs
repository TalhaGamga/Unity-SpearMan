using UnityEngine;

public class CursorManager : MonoBehaviour
{
    [Header("Cursor Modules")]
    [SerializeField] private FirearmCursorHandler _firearmCursorHandler;

    private ITickable _cursorTicker;

    private void Awake()
    {
        ServiceLocator.Global.Register<CursorManager>(this);
    }

    public void SetFirearmCursor(FirearmCursorDataSO cursorDataSO)
    {
        _firearmCursorHandler.SetCursorData(cursorDataSO);
        _cursorTicker = _firearmCursorHandler;
    }

    public void AnimateFiring(float recoilAmount, float recoveryDelay)
    {
        _firearmCursorHandler.AnimateRecoil(recoilAmount, recoveryDelay);
    }

    private void Update()
    {
        _cursorTicker.Tick();
    }
}
