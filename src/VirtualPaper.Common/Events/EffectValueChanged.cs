using System.Text.Json.Serialization;

namespace VirtualPaper.Common.Events.EffectValue.Base {
    [JsonSerializable(typeof(EffectValueChangedBase))]
    public partial class EffectValueChangedBaseContext : JsonSerializerContext { }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(EffectValueChanged<int>), "int")]
    [JsonDerivedType(typeof(EffectValueChanged<double>), "double")]
    [JsonDerivedType(typeof(EffectValueChanged<bool>), "bool")]
    [JsonDerivedType(typeof(EffectValueChanged<string>), "string")]
    public abstract class EffectValueChangedBase : EventArgs {
        public string ControlName { get; set; } = string.Empty;
        public string PropertyName { get; set; } = string.Empty;
    }

    public class EffectValueChanged<T> : EffectValueChangedBase {
        public virtual T Value { get; set; }
    }
}
