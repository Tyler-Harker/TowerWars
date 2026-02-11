using FluentAssertions;
using TowerWars.Shared.Constants;
using Xunit;

namespace TowerWars.Shared.Tests.Constants;

public class TowerStatsTests
{
    [Theory]
    [InlineData(TowerType.Basic, 100)]
    [InlineData(TowerType.Archer, 150)]
    [InlineData(TowerType.Cannon, 250)]
    [InlineData(TowerType.Magic, 200)]
    [InlineData(TowerType.Ultimate, 1000)]
    public void GetStats_ReturnsCorrectCost(TowerType type, int expectedCost)
    {
        var stats = TowerDefinitions.GetStats(type);
        stats.Cost.Should().Be(expectedCost);
    }

    [Fact]
    public void GetStats_UnknownType_ReturnsBasicStats()
    {
        var stats = TowerDefinitions.GetStats((TowerType)99);
        stats.Should().Be(TowerDefinitions.Stats[TowerType.Basic]);
    }

    [Fact]
    public void TowerStats_SellValue_IsApproximately70PercentOfCost()
    {
        foreach (var (type, stats) in TowerDefinitions.Stats)
        {
            var expectedSellValue = (int)(stats.Cost * GameConstants.TowerSellPercentage);
            stats.SellValue.Should().Be(expectedSellValue,
                because: $"{type} sell value should be 70% of cost");
        }
    }

    [Theory]
    [InlineData(TowerType.Lightning, 0)]
    [InlineData(TowerType.Basic, 10)]
    [InlineData(TowerType.Archer, 15)]
    public void GetStats_ProjectileSpeed_IsCorrect(TowerType type, float expectedSpeed)
    {
        var stats = TowerDefinitions.GetStats(type);
        stats.ProjectileSpeed.Should().Be(expectedSpeed);
    }

    [Theory]
    [InlineData(TowerType.Cannon)]
    [InlineData(TowerType.Fire)]
    [InlineData(TowerType.Ultimate)]
    public void GetStats_SplashTowers_HaveSplashRadius(TowerType type)
    {
        var stats = TowerDefinitions.GetStats(type);
        stats.SplashRadius.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetStats_IceTower_HasSlowEffect()
    {
        var stats = TowerDefinitions.GetStats(TowerType.Ice);
        stats.SlowAmount.Should().BeGreaterThan(0);
        stats.SlowDuration.Should().BeGreaterThan(0);
    }
}
