using System.Diagnostics;

namespace LocalAIAgent.Domain.ValueObjects
{
    [DebuggerDisplay("UserId = {_value}")]
    public sealed class UserId
    {
        private readonly int _value;

        public UserId(int value)
        {
            if (value is <= 0)
                throw new ArgumentException("User ID cannot be 0 or negative", nameof(value));

            _value = value;
        }

        public override bool Equals(object? obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is UserId other && _value == other._value;
        }

        public override int GetHashCode() => _value.GetHashCode();

        public static bool operator ==(UserId? left, UserId? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        public static bool operator !=(UserId? left, UserId? right) => !(left == right);

        public static implicit operator int(UserId userId) => userId._value;
        public static implicit operator UserId(int value) => new(value);

        public override string ToString() => $"UserId: {_value}";

        public int Value => _value;
    }
}
