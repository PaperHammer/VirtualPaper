using System.Numerics;
using MessagePack;

namespace BuiltIn.Models {
    /// <summary>
    /// 泛型数值类型的二维点
    /// </summary>
    /// <typeparam name="T">必须是数值类型（int/long/float/double等）</typeparam>
    [MessagePackObject]
    public readonly struct ArcPoint<T>(T x, T y) :
        IEquatable<ArcPoint<T>>,
        IFormattable
        where T : struct, IComparable, IComparable<T>, IConvertible, IEquatable<T>, INumber<T>, IRootFunctions<T>, IFormattable {
        [Key(0)]
        public T X { get; } = x;
        [Key(1)]
        public T Y { get; } = y;

        public override string ToString() => $"({X}, {Y})";
        public string ToString(string? format, IFormatProvider? formatProvider) =>
            $"({X.ToString(format, formatProvider)}, {Y.ToString(format, formatProvider)})";

        public bool Equals(ArcPoint<T> other) =>
            X.Equals(other.X) && Y.Equals(other.Y);

        public override bool Equals(object? obj) =>
            obj is ArcPoint<T> other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public static bool operator ==(ArcPoint<T> left, ArcPoint<T> right) {
            return left.Equals(right);
        }

        public static bool operator !=(ArcPoint<T> left, ArcPoint<T> right) {
            return !(left == right);
        }

        public static ArcPoint<T> operator +(ArcPoint<T> left, ArcPoint<T> right) {
            return new ArcPoint<T>(left.X + right.X, left.Y + right.Y);
        }

        public static ArcPoint<T> operator -(ArcPoint<T> left, ArcPoint<T> right) {
            return new ArcPoint<T>(left.X - right.X, left.Y - right.Y);
        }

        public static ArcPoint<T> operator *(ArcPoint<T> point, T scale) {
            return new ArcPoint<T>(point.X * scale, point.Y + scale);
        }

        public static T Dictance(ArcPoint<T> left, ArcPoint<T> right) {
            var dx = left.X - right.X;
            var dy = left.Y - right.Y;
            return T.Sqrt(dx * dx + dy * dy);
        }

        public static implicit operator Vector2(ArcPoint<T> point) {
            return new Vector2(
                Convert.ToSingle(point.X),
                Convert.ToSingle(point.Y)
            );
        }
    }
}
