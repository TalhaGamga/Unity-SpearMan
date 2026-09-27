using System.Collections.Generic;
using UnityEngine;

namespace Combat
{
    /// <summary>
    /// How bright a one-shot impact is, how long it lasts and how it goes -
    /// a few numbers over every particle system under it, laid on top of what
    /// each system was authored with rather than replacing it.
    /// </summary>
    /// <remarks>
    /// An impact is five systems with five lifetimes, five alpha ramps and
    /// five materials, each tuned against the others; changing how long the
    /// whole thing lasts by hand means finding and scaling every one of them,
    /// and getting one wrong leaves a spark behind or a flash cut short. Here
    /// each is a single number, and every system keeps its own character
    /// under it: Duration stretches all of the lifetimes together, so the
    /// flash still goes before the chips; the fade reshapes the last stretch
    /// of each system's own ramp, so a layer authored to fade early still
    /// fades first.
    ///
    /// At their defaults nothing changes: every value multiplies or reshapes
    /// the authored one by exactly nothing. A layer with no colour over
    /// lifetime has no fade to reshape and keeps its own.
    ///
    /// The values are applied every time the instance is enabled, which is
    /// every time the pool hands it out, and always against what the systems
    /// were authored with - captured once, when the instance is created - so a
    /// pooled instance never compounds its own changes.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ImpactTuning : MonoBehaviour
    {
        /// <summary>Fade keys a gradient gets; Unity allows eight alpha keys in all.</summary>
        private const int FadeKeys = 6;

        /// <summary>Samples along a rebuilt size curve.</summary>
        private const int SizeSamples = 24;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        [Tooltip("How bright the whole impact is, against how it was authored. " +
            "It multiplies every layer's light, so above 1 more of it crosses " +
            "the bloom threshold and glows; below 1 it dims, down to nothing " +
            "at 0.\n\n" +
            "Size is not this: that is the hit cue's Scale, in the weapon's " +
            "visual pack.")]
        [Range(0f, 4f)] public float Intensity = 1f;

        [Tooltip("How long the impact lasts, against how it was authored: 2 is " +
            "twice as long. Every layer's lifetime is stretched together, so " +
            "the flash still goes first and the chips last.\n\n" +
            "The burst keeps its speed - the sparks leave the contact as fast " +
            "as before and simply hang in the air longer as they slow - and " +
            "everything a layer does over its life, growing, shrinking and " +
            "fading, is spread over the longer life.")]
        [Range(0.25f, 4f)] public float Duration = 1f;

        [Tooltip("How much of each layer's life is spent fading out, against " +
            "how it was authored. Above 1 the fade starts earlier and takes " +
            "longer, which is softer; below 1 the layer holds its brightness " +
            "for longer and then goes quickly, which is harder. A layer can " +
            "never spend more than its whole life fading.")]
        [Range(0.25f, 3f)] public float FadeLength = 1f;

        [Tooltip("How the fade-out runs from full to gone. Linear is the " +
            "authored, even fall. Soft drops quickly and leaves a faint " +
            "trail; Smooth eases in and out, with no edge at either end; " +
            "Hard holds on and goes at the end. Custom uses the Fade Curve " +
            "below.")]
        public ImpactFadeShape FadeShape = ImpactFadeShape.Linear;

        [Tooltip("Custom only. Left to right is the fade's own time, from where " +
            "it starts to the particle's death; top to bottom is full to gone. " +
            "End it at 0, or the layers are still showing on their last frame " +
            "and blink out instead of fading.")]
        public AnimationCurve FadeCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);

        [Tooltip("How much the layers shrink as they fade. 0 keeps each layer's " +
            "authored size; 1 collapses it to nothing along with its light, on " +
            "the same curve, so it goes out as a point rather than a dimming " +
            "shape.")]
        [Range(0f, 1f)] public float FadeShrink = 0f;

        /// <summary>One system as it was authored, before any tuning.</summary>
        private struct Layer
        {
            public ParticleSystem System;
            public ParticleSystemRenderer Renderer;
            public ParticleSystem.MinMaxCurve Lifetime;
            public bool ColourEnabled;
            public ParticleSystem.MinMaxGradient Colour;
            public bool SizeEnabled;
            public ParticleSystem.MinMaxCurve Size;
            public bool HasIntensity;
            public float Intensity;
        }

        private Layer[] _layers;
        private MaterialPropertyBlock _block;

        private void Awake() => capture();

        private void OnEnable()
        {
#if UNITY_EDITOR
            pullFromPrefab();
#endif
            Apply();
        }

        /// <summary>
        /// Writes the values onto the systems. Done on every enable; call it
        /// again after changing a value on a live instance.
        /// </summary>
        public void Apply()
        {
            if (_layers == null)
                capture();

            for (int i = 0; i < _layers.Length; i++)
                apply(_layers[i]);
        }

        /// <summary>
        /// How long the impact needs to stay out for its longest-lived
        /// particle to finish at this Duration, in seconds, plus a frame.
        /// </summary>
        /// <remarks>
        /// Read before anything is spawned - from the prefab itself, whose
        /// systems still hold what they were authored with - by whoever
        /// decides how long the pool keeps the instance. A cue timed for the
        /// authored impact would otherwise pull a longer one back mid-fade,
        /// and the pool clears whatever is still on screen when it does.
        /// </remarks>
        public float SecondsNeeded() => AuthoredSeconds() * Mathf.Max(Duration, 0f) + 1f / 30f;

        /// <summary>
        /// The longest any particle lives as authored, in seconds: how long
        /// the impact lasts at a Duration of 1.
        /// </summary>
        public float AuthoredSeconds()
        {
            float longest = 0f;

            if (_layers != null)
            {
                for (int i = 0; i < _layers.Length; i++)
                {
                    if (_layers[i].System == null)
                        continue;

                    longest = Mathf.Max(longest,
                        _layers[i].System.main.startDelay.constantMax + peak(_layers[i].Lifetime));
                }

                return longest;
            }

            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                longest = Mathf.Max(longest, main.startDelay.constantMax + peak(main.startLifetime));
            }

            return longest;
        }

        /// <summary>The fade's own curve for the chosen shape, 1 at its start and 0 at its end.</summary>
        public float Fade(float u)
        {
            u = Mathf.Clamp01(u);

            switch (FadeShape)
            {
                case ImpactFadeShape.Soft:
                    return (1f - u) * (1f - u);
                case ImpactFadeShape.Smooth:
                    return 1f - u * u * (3f - 2f * u);
                case ImpactFadeShape.Hard:
                    return 1f - u * u * u;
                case ImpactFadeShape.Custom:
                    return FadeCurve != null && FadeCurve.length > 0
                        ? Mathf.Clamp01(FadeCurve.Evaluate(u))
                        : 1f - u;
                default:
                    return 1f - u;
            }
        }

        private void capture()
        {
            var layers = new List<Layer>();

            foreach (ParticleSystem system in GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.MainModule main = system.main;
                ParticleSystem.ColorOverLifetimeModule colour = system.colorOverLifetime;
                ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
                var renderer = system.GetComponent<ParticleSystemRenderer>();
                Material material = renderer != null ? renderer.sharedMaterial : null;
                bool hasIntensity = material != null && material.HasProperty(IntensityId);

                // Copied, not kept: the curves and gradients a module hands out
                // can be the module's own, and would then turn into whatever
                // was last applied - a pooled impact would fade on from its
                // previous tuning instead of from what it was authored with.
                layers.Add(new Layer
                {
                    System = system,
                    Renderer = renderer,
                    Lifetime = copy(main.startLifetime),
                    ColourEnabled = colour.enabled,
                    Colour = copy(colour.color),
                    SizeEnabled = size.enabled,
                    Size = copy(size.size),
                    HasIntensity = hasIntensity,
                    Intensity = hasIntensity ? material.GetFloat(IntensityId) : 1f
                });
            }

            _layers = layers.ToArray();
        }

        private void apply(in Layer layer)
        {
            if (layer.System == null)
                return;

            ParticleSystem.MainModule main = layer.System.main;
            main.startLifetime = scaled(layer.Lifetime, Mathf.Max(Duration, 0.01f));

            float fadeStart = 1f;

            ParticleSystem.ColorOverLifetimeModule colour = layer.System.colorOverLifetime;
            if (layer.ColourEnabled)
                colour.color = refaded(layer.Colour, out fadeStart);

            ParticleSystem.SizeOverLifetimeModule size = layer.System.sizeOverLifetime;
            if (FadeShrink > 0f && fadeStart < 1f)
            {
                size.enabled = true;
                size.size = shrunk(layer.SizeEnabled ? layer.Size : new ParticleSystem.MinMaxCurve(1f), fadeStart);
            }
            else
            {
                size.enabled = layer.SizeEnabled;
                size.size = layer.Size;
            }

            if (layer.Renderer == null || !layer.HasIntensity)
                return;

            // Left alone at 1, so an untuned impact keeps drawing from its
            // material and batching with it.
            if (Mathf.Approximately(Intensity, 1f))
            {
                layer.Renderer.SetPropertyBlock(null);
                return;
            }

            _block ??= new MaterialPropertyBlock();
            layer.Renderer.GetPropertyBlock(_block);
            _block.SetFloat(IntensityId, layer.Intensity * Mathf.Max(Intensity, 0f));
            layer.Renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// The authored colour over life with its fade-out rebuilt. Everything
        /// before the fade and every colour key is kept as authored.
        /// </summary>
        private ParticleSystem.MinMaxGradient refaded(ParticleSystem.MinMaxGradient authored, out float fadeStart)
        {
            fadeStart = 1f;

            switch (authored.mode)
            {
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(
                        refaded(authored.gradient, out fadeStart));

                case ParticleSystemGradientMode.TwoGradients:
                    Gradient min = refaded(authored.gradientMin, out float startMin);
                    Gradient max = refaded(authored.gradientMax, out fadeStart);
                    fadeStart = Mathf.Min(fadeStart, startMin);
                    return new ParticleSystem.MinMaxGradient(min, max);

                default:
                    // A flat colour has no ramp to reshape.
                    return authored;
            }
        }

        /// <remarks>
        /// The authored fade is the stretch from the second-to-last alpha key
        /// to the last, which lands on nothing at the particle's death. Fade
        /// Length moves where it starts, keeping where it ends; the shape
        /// then runs from the level the ramp had there down to nothing. At a
        /// length of 1 and the Linear shape, the keys come out where they were.
        /// </remarks>
        private Gradient refaded(Gradient authored, out float fadeStart)
        {
            fadeStart = 1f;

            if (authored == null)
                return null;

            GradientAlphaKey[] keys = authored.alphaKeys;

            if (keys.Length < 2)
                return authored;

            System.Array.Sort(keys, (a, b) => a.time.CompareTo(b.time));

            // Where the authored fade starts: the key before the last one when
            // the last sits on the particle's death, else the last key itself,
            // with the fade running on from it to death.
            GradientAlphaKey last = keys[keys.Length - 1];
            float authoredStart = last.time >= 0.999f ? keys[keys.Length - 2].time : last.time;
            float authoredLength = 1f - authoredStart;

            // An untouched fade keeps the authored gradient itself. The same
            // ramp rebuilt from more keys is not drawn quite the same - the
            // particle renderer comes out a few percent off on the faint edges
            // - and an untuned impact should be exactly the authored one.
            if (Mathf.Approximately(FadeLength, 1f) && FadeShape == ImpactFadeShape.Linear &&
                last.time >= 0.999f && last.alpha <= 1e-4f)
            {
                fadeStart = authoredStart;
                return authored;
            }

            // Never the whole life and never none of it: the layer keeps at
            // least a frame's worth of fade at either extreme.
            float start = Mathf.Clamp(1f - authoredLength * Mathf.Max(FadeLength, 0f), 0f, 0.98f);

            // A fade that starts earlier starts from wherever the authored ramp
            // had got to; one that starts later holds the level the authored
            // fade started from, rather than fading along the old ramp first.
            float level = start <= authoredStart
                ? authored.Evaluate(start).a
                : authored.Evaluate(authoredStart).a;

            var alpha = new List<GradientAlphaKey>(8);

            for (int i = 0; i < keys.Length && alpha.Count < 8 - FadeKeys; i++)
            {
                if (keys[i].time < start - 1e-4f && keys[i].time <= authoredStart + 1e-4f)
                    alpha.Add(keys[i]);
            }

            for (int i = 0; i < FadeKeys; i++)
            {
                float u = i / (float)(FadeKeys - 1);
                alpha.Add(new GradientAlphaKey(level * Fade(u), Mathf.Lerp(start, 1f, u)));
            }

            var gradient = new Gradient { mode = authored.mode, colorSpace = authored.colorSpace };
            gradient.SetKeys(authored.colorKeys, alpha.ToArray());

            fadeStart = start;
            return gradient;
        }

        /// <summary>
        /// The authored size over life, taken down by Fade Shrink over the
        /// fade on the same curve as the light.
        /// </summary>
        private ParticleSystem.MinMaxCurve shrunk(ParticleSystem.MinMaxCurve authored, float fadeStart)
        {
            switch (authored.mode)
            {
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(authored.curveMultiplier,
                        shrunk(authored.curve, 1f, fadeStart));

                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(authored.curveMultiplier,
                        shrunk(authored.curveMin, 1f, fadeStart),
                        shrunk(authored.curveMax, 1f, fadeStart));

                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(1f,
                        shrunk(null, authored.constantMin, fadeStart),
                        shrunk(null, authored.constantMax, fadeStart));

                default:
                    return new ParticleSystem.MinMaxCurve(1f, shrunk(null, authored.constant, fadeStart));
            }
        }

        private AnimationCurve shrunk(AnimationCurve authored, float constant, float fadeStart)
        {
            var keys = new Keyframe[SizeSamples + 1];

            for (int i = 0; i <= SizeSamples; i++)
            {
                float t = i / (float)SizeSamples;
                float value = authored != null ? authored.Evaluate(t) : constant;

                if (t > fadeStart)
                {
                    float u = (t - fadeStart) / Mathf.Max(1f - fadeStart, 1e-4f);
                    value *= Mathf.Lerp(1f, Fade(u), FadeShrink);
                }

                keys[i] = new Keyframe(t, value);
            }

            // Straight between samples: a spline through them would overshoot
            // below zero where the shrink lands on nothing.
            for (int i = 0; i <= SizeSamples; i++)
            {
                float inSlope = i > 0
                    ? (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time)
                    : 0f;
                float outSlope = i < SizeSamples
                    ? (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time)
                    : 0f;

                keys[i].inTangent = i > 0 ? inSlope : outSlope;
                keys[i].outTangent = i < SizeSamples ? outSlope : inSlope;
            }

            return new AnimationCurve(keys);
        }

        private static ParticleSystem.MinMaxCurve scaled(ParticleSystem.MinMaxCurve authored, float factor)
        {
            switch (authored.mode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(authored.constantMin * factor, authored.constantMax * factor);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(authored.curveMultiplier * factor, authored.curve);
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(authored.curveMultiplier * factor, authored.curveMin, authored.curveMax);
                default:
                    return new ParticleSystem.MinMaxCurve(authored.constant * factor);
            }
        }

        private static ParticleSystem.MinMaxCurve copy(ParticleSystem.MinMaxCurve curve)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(curve.curveMultiplier, copy(curve.curve));
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(curve.curveMultiplier, copy(curve.curveMin), copy(curve.curveMax));
                default:
                    // Constants are plain numbers.
                    return curve;
            }
        }

        private static AnimationCurve copy(AnimationCurve curve) =>
            curve == null
                ? null
                : new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };

        private static ParticleSystem.MinMaxGradient copy(ParticleSystem.MinMaxGradient gradient)
        {
            switch (gradient.mode)
            {
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(copy(gradient.gradient));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(copy(gradient.gradientMin), copy(gradient.gradientMax));
                case ParticleSystemGradientMode.RandomColor:
                    return new ParticleSystem.MinMaxGradient(copy(gradient.gradient)) { mode = ParticleSystemGradientMode.RandomColor };
                default:
                    // Colours are plain values.
                    return gradient;
            }
        }

        private static Gradient copy(Gradient gradient)
        {
            if (gradient == null)
                return null;

            var result = new Gradient { mode = gradient.mode, colorSpace = gradient.colorSpace };
            result.SetKeys(gradient.colorKeys, gradient.alphaKeys);
            return result;
        }

        /// <summary>The longest a lifetime can come out, in seconds.</summary>
        private static float peak(ParticleSystem.MinMaxCurve lifetime)
        {
            switch (lifetime.mode)
            {
                case ParticleSystemCurveMode.TwoConstants:
                    return Mathf.Max(lifetime.constantMin, lifetime.constantMax);

                case ParticleSystemCurveMode.Curve:
                case ParticleSystemCurveMode.TwoCurves:
                    AnimationCurve curve = lifetime.mode == ParticleSystemCurveMode.TwoCurves
                        ? lifetime.curveMax
                        : lifetime.curve;
                    float top = 0f;
                    if (curve != null)
                        for (int k = 0; k < curve.length; k++)
                            top = Mathf.Max(top, curve[k].value);
                    return top * lifetime.curveMultiplier;

                default:
                    return lifetime.constant;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Takes the values from the prefab this instance was made from.
        /// </summary>
        /// <remarks>
        /// The pool hands the same instance back hit after hit, so a value
        /// changed on the prefab in play mode would otherwise not show until
        /// the pool happened to make a new one - which, with one hit on screen
        /// at a time, is never. The pool marks every instance with the prefab
        /// it came from; in a build there is nothing to pull, since the prefab
        /// cannot change.
        /// </remarks>
        private void pullFromPrefab()
        {
            if (!TryGetComponent(out VFXMarker marker))
                return;

            var prefab = UnityEditor.EditorUtility.InstanceIDToObject(marker.PrefabId) as GameObject;

            if (prefab == null || prefab == gameObject || !prefab.TryGetComponent(out ImpactTuning source))
                return;

            Intensity = source.Intensity;
            Duration = source.Duration;
            FadeLength = source.FadeLength;
            FadeShape = source.FadeShape;
            FadeCurve = source.FadeCurve != null ? new AnimationCurve(source.FadeCurve.keys) : null;
            FadeShrink = source.FadeShrink;
        }
#endif
    }
}
