using System;
using Unity.Cinemachine;
using UnityEngine;

// This will be optimized.
public class ImpulseShakeEffect : ICameraEffect<CinemachineCamera>
{
    private readonly Vector3 velocity;
    private readonly float force;
    private readonly float duration;
    private readonly CinemachineImpulseSource sourcePrefab;

    private float timeAlive;
    private bool triggered;

    public ImpulseShakeEffect(CinemachineImpulseSource prefab, Vector3 velocity, float force, float duration)
    {
        this.velocity = velocity;
        this.force = force;
        this.duration = duration;
        this.sourcePrefab = prefab;
    }

    public bool IsFinished => timeAlive >= duration;

    public void Tick(float deltaTime)
    {
        timeAlive += deltaTime;
    }

    public void Apply(CinemachineCamera cam)
    {
        if (triggered || sourcePrefab == null) return;

        var instance = UnityEngine.Object.Instantiate(sourcePrefab, cam.transform.position, Quaternion.identity);

        instance.GenerateImpulseAt(cam.transform.position, velocity * force);

        UnityEngine.Object.Destroy(instance.gameObject, 1f);

        triggered = true;
    }
}
