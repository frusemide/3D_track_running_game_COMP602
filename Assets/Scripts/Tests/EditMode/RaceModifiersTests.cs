using NUnit.Framework;
// User story: "As a player, I want to be able to customise my shoes so that I can adapt to different weather conditions." (Sprint 2 scope: rain.)
public class RaceModifiersTests
{
    private const float Tolerance = 0.0001f;
    private const float BaseTooFast = 0.18f;   // Player._tooFastStepInterval

    // ---------- Rain vs shoes (Acceptance test 2) ----------

    [TestCase(WeatherType.Clear)]
    [TestCase(WeatherType.Rain)]
    public void T01_ClearWeather_AnyShoes_NoTopSpeedPenalty(WeatherType shoes)
    {
        Assert.AreEqual(1f, RaceModifiers.WeatherTopSpeedMultiplier(WeatherType.Clear, shoes), Tolerance);
    }

    [Test]
    public void T02_Rain_NormalShoes_ReducesTopSpeed()
    {
        Assert.AreEqual(0.85f, RaceModifiers.WeatherTopSpeedMultiplier(WeatherType.Rain, WeatherType.Clear), Tolerance);
    }

    [Test]
    public void T03_Rain_RainShoes_NoPenalty()
    {
        Assert.AreEqual(1f, RaceModifiers.WeatherTopSpeedMultiplier(WeatherType.Rain, WeatherType.Rain), Tolerance);
    }

    // ---------- Slippery hazard patches + Grip ----------

    [TestCase(WeatherType.Clear)]
    [TestCase(WeatherType.Rain)]
    public void T04_OffSlipPatch_TooFastIntervalUnchanged(WeatherType weather)
    {
        Assert.AreEqual(BaseTooFast, RaceModifiers.TooFastStepInterval(BaseTooFast, false, weather, 0), Tolerance);
    }

    [Test]
    public void T05_OnSlipPatch_ClearWeather_AddsExtraInterval()
    {
        Assert.AreEqual(0.28f, RaceModifiers.TooFastStepInterval(BaseTooFast, true, WeatherType.Clear, 0), Tolerance);
    }

    [Test]
    public void T06_OnSlipPatch_Rain_IsMoreSlippery()
    {
        Assert.AreEqual(0.33f, RaceModifiers.TooFastStepInterval(BaseTooFast, true, WeatherType.Rain, 0), Tolerance);
    }

    [Test]
    public void T07_OnSlipPatch_GripReducesExtraInterval()
    {
        Assert.AreEqual(0.24f, RaceModifiers.TooFastStepInterval(BaseTooFast, true, WeatherType.Clear, 2), Tolerance);  // 2 Grip points remove 40% of the 0.10s extra
    }

    [Test]
    public void T08_OnSlipPatch_HighGrip_NeverBelowBase()
    {
        Assert.AreEqual(BaseTooFast, RaceModifiers.TooFastStepInterval(BaseTooFast, true, WeatherType.Rain, 10), Tolerance);
    }

    [Test]
    public void T09_OnSlipPatch_NegativeGrip_IsWorse()
    {
        Assert.AreEqual(0.32f, RaceModifiers.TooFastStepInterval(BaseTooFast, true, WeatherType.Clear, -2), Tolerance); // e.g. sprint spikes: +Top Speed but -2 Grip
    }
}
