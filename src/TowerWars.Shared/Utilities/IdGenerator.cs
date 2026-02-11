namespace TowerWars.Shared.Utilities;

public sealed class IdGenerator
{
    private uint _counter;

    public uint Next() => Interlocked.Increment(ref _counter);

    public void Reset() => Interlocked.Exchange(ref _counter, 0);
}
