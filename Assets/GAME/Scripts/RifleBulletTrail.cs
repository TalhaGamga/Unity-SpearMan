using UnityEngine;

public class RifleBulletTrail : MonoBehaviour, IBulletTrail
{
    [SerializeField] private TrailRenderer _trailRenderer;
    [SerializeField] private float _speed = 40f;

    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private float _progress;
    private bool _isFiring;

    void Start()
    {
        _trailRenderer.Clear(); 
    }

    void Update()
    {
        if (!_isFiring)
        {
            _trailRenderer.enabled = false;
            return;
        }

        _progress += Time.deltaTime * _speed;
        transform.position = Vector3.Lerp(_startPosition, _targetPosition, _progress);

        if (_progress >= 1f)
        {
            _isFiring = false;
        }
    }

    public void VisualizeFire(Vector3 start, Vector3 end)
    {
        transform.position = start; 

        _trailRenderer.enabled = true;
        _startPosition = start;
        _targetPosition = end;
        _progress = 0;
        _isFiring = true;

        _trailRenderer.Clear(); 
    }
}
