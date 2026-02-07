using System.Diagnostics;

namespace LocalAIAgent.Domain.ValueObjects
{
    [DebuggerDisplay("Fido2Id = {_value}")]
    public sealed class Fido2Id
    {
        private readonly byte[] _value;

        public Fido2Id(byte[] value)
        {
            if (value is null || value.Length == 0)
                throw new ArgumentException("Fido2 ID cannot be null or empty", nameof(value));

            _value = value;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Fido2Id other && _value == other._value;
        }

        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(Fido2Id? left, Fido2Id? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(Fido2Id? left, Fido2Id? right) => !(left == right);

        public static implicit operator byte[](Fido2Id fido2Id) => fido2Id._value;
        public static implicit operator Fido2Id(byte[] value) => new(value);

        public override string ToString() => $"Fido2Id: {_value}";

        public byte[] Value => _value;
    }
}