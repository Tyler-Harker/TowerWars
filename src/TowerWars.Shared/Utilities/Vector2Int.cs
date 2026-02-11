using MessagePack;

namespace TowerWars.Shared.Utilities;

[MessagePackObject]
public readonly struct Vector2Int : IEquatable<Vector2Int>
{
    [Key(0)]
    public int X { get; }

    [Key(1)]
    public int Y { get; }

    [SerializationConstructor]
    public Vector2Int(int x, int y)
    {
        X = x;
        Y = y;
    }

    public static Vector2Int Zero => new(0, 0);
    public static Vector2Int One => new(1, 1);
    public static Vector2Int Up => new(0, -1);
    public static Vector2Int Down => new(0, 1);
    public static Vector2Int Left => new(-1, 0);
    public static Vector2Int Right => new(1, 0);

    public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.X + b.X, a.Y + b.Y);
    public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.X - b.X, a.Y - b.Y);
    public static Vector2Int operator *(Vector2Int a, int scalar) => new(a.X * scalar, a.Y * scalar);
    public static bool operator ==(Vector2Int a, Vector2Int b) => a.X == b.X && a.Y == b.Y;
    public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);

    public float Magnitude => MathF.Sqrt(X * X + Y * Y);
    public int ManhattanDistance(Vector2Int other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public bool Equals(Vector2Int other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is Vector2Int other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";
}
