namespace TowerWars.Shared.Utilities;

public interface ITimeProvider
{
    DateTime UtcNow { get; }
    long UnixTimeMilliseconds { get; }
}

public sealed class SystemTimeProvider : ITimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
    public long UnixTimeMilliseconds => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
