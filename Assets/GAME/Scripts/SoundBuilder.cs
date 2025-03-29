using UnityEngine;

public class SoundBuilder
{
    readonly SoundManager soundManager;
    Vector3 position = Vector3.zero;
    float pitchMin, pitchMax;
    bool randomPitch;

    public SoundBuilder(SoundManager soundManager)
    {
        this.soundManager = soundManager;
    }

    public SoundBuilder WithPosition(Vector3 position)
    {
        this.position = position;
        return this;
    }

    public SoundBuilder WithRandomPitch(float min = -0.5f, float max = 0.5f)
    {
        this.randomPitch = true;
        pitchMin = min;
        pitchMax = max;
        return this;
    }

    public void Play(SoundData soundData)
    {
        if (soundData == null)
        {
            Debug.LogError("SoundData is null");
            return;
        }

        if (!soundManager.CanPlaySound(soundData)) return;

        SoundEmitter soundEmitter = soundManager.Get();
        soundEmitter.Initialize(soundData);
        soundEmitter.transform.position = position;
        soundEmitter.transform.parent = soundManager.transform;

        if (randomPitch)
        {
            soundEmitter.WithRandomPitch(pitchMin, pitchMax);
        }

        if (soundData.frequentSound)
        {
            soundEmitter.Node = soundManager.FrequentSoundEmitters.AddLast(soundEmitter);
        }

        soundEmitter.Play();
    }
}