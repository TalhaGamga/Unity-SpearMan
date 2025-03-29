using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public class FirearmCursorHandler : MonoBehaviour, ITickable
{
    [SerializeField] private RectTransform _cursorRect;

    private Sprite _dot;
    private Sprite _inner;
    private Sprite _expanding;
    private Sprite _reload;

    [SerializeField] private Image _dotImage;
    [SerializeField] private Image _innerImage;
    [SerializeField] private Image _expandingImage;
    [SerializeField] private Image _reloadImage;

    [SerializeField] RectTransform _expandingRect;

    private Tween _recoilTween;

    public void AnimateRecoil(float recoilAmount, float recoveryTime)
    {
        if (_expanding == null) return;

        _recoilTween?.Kill();

        Transform expandingTransform = _expandingRect.transform;

        Vector3 recoilScale = Vector3.one * (2f + recoilAmount);

        expandingTransform.localScale = recoilScale;

        _recoilTween = expandingTransform
            .DOScale(Vector3.one, recoveryTime)
            .SetEase(Ease.OutQuad);
    }

    public void SetCursorData(FirearmCursorDataSO cursorDataSo) 
    {
        if (cursorDataSo == null) return;
        _dot = cursorDataSo.Dot;
        _inner = cursorDataSo.Inner;
        _expanding = cursorDataSo.Expanding;
        _reload = cursorDataSo.Reload;

        _dotImage.sprite = _dot;
        _innerImage.sprite = _inner;
        _expandingImage.sprite = _expanding;
        _reloadImage.sprite = _reload;
    }

    public void Tick()
    {
        _cursorRect.transform.position = Input.mousePosition;
    }
}