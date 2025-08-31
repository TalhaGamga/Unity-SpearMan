using System.Collections.Generic;

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

    public static CombatAnimationFrame ToCombatAnimationFrame(Dictionary<string, string> parsed)
    {
        string action = parsed.TryGetValue("Action", out var a) ? a : "";
        string eventKey = parsed.TryGetValue("EventKey", out var e) ? e : "";
        int stage = parsed.TryGetValue("Stage", out var s) && int.TryParse(s, out var st) ? st : 0;
        int comboStep = parsed.TryGetValue("ComboStep", out var cs) && int.TryParse(cs, out var csInt) ? csInt : 0;
        string comboType = parsed.TryGetValue("ComboType", out var ct) ? ct : "";
        bool isCancelable = parsed.TryGetValue("Cancelable", out var c) && bool.TryParse(c, out var b) && b;
        string stateName = "";

        // Pass comboStep and comboType to AnimationFrame constructor as needed

        return new CombatAnimationFrame(
            action,
            eventKey,
            stage,
            isCancelable,
            stateName,
            comboStep,
            comboType
        );
    }

    public static MovementAnimationFrame ToMovementAnimationFrame(Dictionary<string, string> parsed)
    {
        string action = parsed.TryGetValue("Action", out var a) ? a : "";
        string eventKey = parsed.TryGetValue("EventKey", out var e) ? e : "";

        return new MovementAnimationFrame(action, eventKey);
    }
}
