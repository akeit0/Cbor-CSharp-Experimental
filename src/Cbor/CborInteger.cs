using System.Globalization;
using System.Numerics;

namespace Cbor;

/// <summary>A major-type 0 or 1 integer, including the complete range from -2^64 through 2^64-1.</summary>
public readonly struct CborInteger : IEquatable<CborInteger>
{
    /// <summary>Creates a nonnegative integer.</summary>
    public CborInteger(ulong value) : this(value, false) { }

    private CborInteger(ulong argument, bool negative)
    {
        Argument = argument;
        IsNegative = negative;
    }

    /// <summary>The wire argument. For a negative integer its value is -1 minus this argument.</summary>
    public ulong Argument { get; }

    /// <summary>Whether the integer has major type 1.</summary>
    public bool IsNegative { get; }

    /// <summary>Creates a negative integer with value -1 minus argument, without signed narrowing.</summary>
    public static CborInteger FromNegativeArgument(ulong argument) => new(argument, true);

    /// <summary>Creates an integer from a signed CLR value.</summary>
    public static CborInteger FromInt64(long value) => value < 0 ? new((ulong)~value, true) : new((ulong)value);

    /// <summary>Converts to Int64, throwing when the value is out of range.</summary>
    public long ToInt64() => IsNegative ? ~checked((long)Argument) : checked((long)Argument);

    /// <summary>Converts to UInt64, throwing for any negative value.</summary>
    public ulong ToUInt64() => IsNegative ? throw new OverflowException("A negative CBOR integer cannot be converted to UInt64.") : Argument;

    /// <summary>Converts without narrowing.</summary>
    public BigInteger ToBigInteger() => IsNegative ? ~new BigInteger(Argument) : new BigInteger(Argument);

    /// <inheritdoc />
    public bool Equals(CborInteger other) => Argument == other.Argument && IsNegative == other.IsNegative;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CborInteger other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => Argument.GetHashCode() ^ IsNegative.GetHashCode();
    /// <inheritdoc />
    public override string ToString() => ToBigInteger().ToString(CultureInfo.InvariantCulture);
    /// <summary>Compares integer values.</summary>
    public static bool operator ==(CborInteger left, CborInteger right) => left.Equals(right);
    /// <summary>Compares integer values.</summary>
    public static bool operator !=(CborInteger left, CborInteger right) => !left.Equals(right);
}
