using UnityEngine;

public class CameraFollowTarget : MonoBehaviour
{
    [SerializeField]
    private bool _autoUpdate = true;
    [SerializeField]
    private bool _fixedUpdate;

    private float   freezeDuration;
    private Vector3 _followPos;

    public Vector3 followPos => _followPos;

    void Start()
    {
        _followPos = transform.position;
    }

    void FixedUpdate()
    {
        if (!_fixedUpdate) return;

        if ((_autoUpdate) && (freezeDuration <= 0.0f))
        {
            _followPos = transform.position;
        }
    }

    void Update()
    {
        if (freezeDuration > 0.0f)
        {
            freezeDuration -= Time.deltaTime;
            if (freezeDuration <= 0)
            {
                _autoUpdate = true;
                _followPos = transform.position;
            }
        }
        if (_fixedUpdate) return;

        if ((_autoUpdate) && (freezeDuration <= 0.0f))
        {
            _followPos = transform.position;
        }
    }

    public void FreezeFollow(float duration)
    {
        freezeDuration = Mathf.Max(freezeDuration, duration);
    }
}
