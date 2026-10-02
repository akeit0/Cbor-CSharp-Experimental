using Cbor.Internal;

namespace Cbor;

/// <summary>A CBOR simple value, including false, true, null, undefined, and unassigned values.</summary>
/// <remarks>Values 24 through 31 are reserved and cannot be constructed. Floats and break are not simple values.</remarks>
public readonly struct CborSimpleValue : IEquatable<CborSimpleValue>
{
    /// <summary>Creates a simple value in the range 0–23 or 32–255.</summary>
    public CborSimpleValue(byte value)
    {
        if (value is >= CborEncoding.DirectArgumentLimit and < CborEncoding.MinimumExtendedSimpleValue)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Simple values 24 through 31 are reserved.");
        }
        Value = value;
    }

    /// <summary>The simple-value number.</summary>
    public byte Value { get; }
    /// <summary>CBOR false.</summary>
    public static CborSimpleValue False => new(CborEncoding.FalseValue);
    /// <summary>CBOR true.</summary>
    public static CborSimpleValue True => new(CborEncoding.TrueValue);
    /// <summary>CBOR null.</summary>
    public static CborSimpleValue Null => new(CborEncoding.NullValue);
    /// <summary>CBOR undefined, distinct from null.</summary>
    public static CborSimpleValue Undefined => new(CborEncoding.UndefinedValue);
    /// <inheritdoc />
    public bool Equals(CborSimpleValue other) => Value == other.Value;
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is CborSimpleValue other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => Value;
    /// <summary>Compares simple-value numbers.</summary>
    public static bool operator ==(CborSimpleValue left, CborSimpleValue right) => left.Equals(right);
    /// <summary>Compares simple-value numbers.</summary>
    public static bool operator !=(CborSimpleValue left, CborSimpleValue right) => !left.Equals(right);
}
