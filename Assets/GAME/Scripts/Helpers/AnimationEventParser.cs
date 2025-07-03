using System.Collections.Generic;
using UnityEngine;

public static class AnimationEventParser
{
    public static Dictionary<string, string> Parse(string eventString)
    {
        var dict = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(eventString))
            return dict;

        var parts = eventString.Split(';');
        foreach (var part in parts)
        {
            var kv = part.Split('=');
            if (kv.Length == 2)
                dict[kv[0].Trim()] = kv[1].Trim();
        }
        return dict;
    }

    public static AnimationFrame ToAnimationFrame(string eventString, Animator anim, int layer)
    {
        var dict = Parse(eventString);

        string action = dict.TryGetValue("Action", out var a) ? a : "";
        string eventKey = dict.TryGetValue("EventKey", out var e) ? e : "";
        int stage = dict.TryGetValue("Stage", out var s) && int.TryParse(s, out var st) ? st : 0;
        int comboStep = dict.TryGetValue("ComboStep", out var cs) && int.TryParse(cs, out var csInt) ? csInt : 0;
        string comboType = dict.TryGetValue("ComboType", out var ct) ? ct : "";
        bool isCancelable = dict.TryGetValue("Cancelable", out var c) && bool.TryParse(c, out var b) && b;

        string stateName = "";
        float normalizedTime = 0f;
        if (anim != null)
        {
            var stateInfo = anim.GetCurrentAnimatorStateInfo(layer);
            stateName = stateInfo.IsName("") ? "" : stateInfo.shortNameHash.ToString();
            normalizedTime = stateInfo.normalizedTime;
        }

        // Pass comboStep and comboType to AnimationFrame constructor as needed

        return new AnimationFrame(
            action,
            eventKey,
            stage,
            isCancelable,
            stateName,
            normalizedTime,
            layer,
            anim,
            comboStep,
            comboType
        );
    }
}
