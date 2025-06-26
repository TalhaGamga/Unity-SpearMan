using UnityEngine;

public struct AnimationFrame
{
    public string EventKey;
    public string StateName;
    public float NormalizedTime;
    public int Layer;
    public Animator SourceAnimator;

    public AnimationFrame(string key, string state = "", float time = 0f, int layer = 0, Animator source = null)
    {
        EventKey = key;
        StateName = state;
        NormalizedTime = time;
        Layer = layer;
        SourceAnimator = source;
    }
}
