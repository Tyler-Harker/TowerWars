using FluentAssertions;
using TowerWars.Shared.Constants;
using Xunit;

namespace TowerWars.Shared.Tests.Constants;

public class UnitStatsTests
{
    [Theory]
    [InlineData(UnitType.Basic, 100)]
    [InlineData(UnitType.Fast, 60)]
    [InlineData(UnitType.Tank, 500)]
    [InlineData(UnitType.Boss, 2000)]
    public void GetStats_ReturnsCorrectHealth(UnitType type, int expectedHealth)
    {
        var stats = UnitDefinitions.GetStats(type);
        stats.Health.Should().Be(expectedHealth);
    }

    [Theory]
    [InlineData(UnitType.Flying, true)]
    [InlineData(UnitType.Basic, false)]
    [InlineData(UnitType.Tank, false)]
    public void GetStats_FlyingProperty_IsCorrect(UnitType type, bool canFly)
    {
        var stats = UnitDefinitions.GetStats(type);
        stats.CanFly.Should().Be(canFly);
    }

    [Fact]
    public void GetStats_InvisibleUnit_HasInvisibleProperty()
    {
        var stats = UnitDefinitions.GetStats(UnitType.Invisible);
        stats.IsInvisible.Should().BeTrue();
    }

    [Fact]
    public void GetStats_SplittingUnit_HasSplitProperties()
    {
        var stats = UnitDefinitions.GetStats(UnitType.Splitting);
        stats.SplitCount.Should().Be(2);
        stats.SplitInto.Should().Be(UnitType.Fast);
    }

    [Theory]
    [InlineData(1, 1.1f)]
    [InlineData(5, 1.5f)]
    [InlineData(10, 2.0f)]
    public void ScaleForWave_IncreasesHealthCorrectly(int waveNumber, float expectedMultiplier)
    {
        var baseStats = UnitDefinitions.GetStats(UnitType.Basic);
        var scaled = UnitDefinitions.ScaleForWave(UnitType.Basic, waveNumber);

        var expectedHealth = (int)(baseStats.Health * expectedMultiplier);
        scaled.Health.Should().Be(expectedHealth);
    }

    [Fact]
    public void ScaleForWave_SpeedCappedAtDoubleBase()
    {
        var baseStats = UnitDefinitions.GetStats(UnitType.Basic);
        var scaled = UnitDefinitions.ScaleForWave(UnitType.Basic, 100);

        scaled.Speed.Should().BeLessOrEqualTo(baseStats.Speed * 2);
    }

    [Fact]
    public void GetStats_UnknownType_ReturnsBasicStats()
    {
        var stats = UnitDefinitions.GetStats((UnitType)99);
        stats.Should().Be(UnitDefinitions.Stats[UnitType.Basic]);
    }

    [Fact]
    public void AllUnits_HavePositiveGoldReward()
    {
        foreach (var (type, stats) in UnitDefinitions.Stats)
        {
            stats.GoldReward.Should().BeGreaterThan(0,
                because: $"{type} should give gold when killed");
        }
    }

    [Fact]
    public void BossUnit_HasHigherLivesCost()
    {
        var bossStats = UnitDefinitions.GetStats(UnitType.Boss);
        var basicStats = UnitDefinitions.GetStats(UnitType.Basic);

        bossStats.LivesCost.Should().BeGreaterThan(basicStats.LivesCost);
    }
}
