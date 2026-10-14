using UnityEngine;

public static class RaceModifiers
{

    // --- Weather (top speed) ---
    public const float RainTopSpeedMultiplier = 0.85f;

    // --- Slippery patches (stumble timing) ---
    public const float SlipPatchExtraInterval = 0.10f;   // seconds added to the "too fast" step limit
    public const float RainSlipMultiplier = 1.5f;
    public const float GripReductionPerPoint = 0.20f;    // each Grip point removes 20% of the extra slipping

    public static float WeatherTopSpeedMultiplier(WeatherType weather, WeatherType shoeWeather)
    {
        if (weather == WeatherType.Rain && shoeWeather != WeatherType.Rain)
            return RainTopSpeedMultiplier;

        return 1f;
    }

    // On a slippery patch the "too fast" step limit grows, so rushed pedalling stumbles.
    // Rain makes patches worse; Grip cancels the extra slipping (negative Grip makes it worse).
    public static float TooFastStepInterval(float baseInterval, bool onSlipPatch, WeatherType weather, int grip)
    {
        if (!onSlipPatch)
            return baseInterval;

        float extra = SlipPatchExtraInterval;
        if (weather == WeatherType.Rain)
            extra *= RainSlipMultiplier;

        float gripReduction = Mathf.Clamp(grip * GripReductionPerPoint, -1f, 1f);
        return baseInterval + extra * (1f - gripReduction);
    }
}
