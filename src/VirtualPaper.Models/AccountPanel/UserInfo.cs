using System.Text.Json.Serialization;
using VirtualPaper.Models.Mvvm;

namespace VirtualPaper.Models.AccountPanel {
    [JsonSerializable(typeof(UserInfo))]
    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    public partial class UserInfoContext : JsonSerializerContext { }

    public class UserInfo : ObservableObject, IEquatable<UserInfo> {
        [JsonInclude]
        public string Uid { get; }

        private byte[]? _avatar;
        public byte[]? Avatar {
            get => _avatar;
            set { _avatar = value; OnPropertyChanged(); }
        }

        private string _name = string.Empty;
        public string Name {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Email { get; set; } = string.Empty;

        private string? _sign;
        public string? Sign {
            get => _sign;
            set { _sign = value; OnPropertyChanged(); }
        }
        [JsonInclude]
        public UserStatus Status { get; }

        [JsonConstructor]
        public UserInfo(string uid, UserStatus status) {
            Uid = uid;
            Status = status;
        }

        public bool Equals(UserInfo? other) {
            return other != null &&
                Uid == other.Uid &&
                Avatar == other.Avatar &&               
                Name == other.Name &&
                Email == other.Email &&
                Sign == other.Sign &&
                Status == other.Status;
        }

        public static bool operator ==(UserInfo? left, UserInfo? right) {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(UserInfo? left, UserInfo? right) {
            return !(left == right);
        }

        public override bool Equals(object? obj) {
            return Equals(obj as UserInfo);
        }

        public override int GetHashCode() {
            return HashCode.Combine(Uid, Name, Email, Sign, Status);
        }

        public UserInfo Clone() {
            return new(Uid, Status) {
                Avatar = Avatar,
                Name = Name,
                Email = Email,
                Sign = Sign
            };
        }
    }

    [Flags]
    public enum UserStatus {
        Normal = 1 << 0, // 001
        Locked = 1 << 1, // 010
        Deleted = 1 << 2 // 100
    }
}
