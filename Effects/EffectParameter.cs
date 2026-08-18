using System;

namespace Polaris.Particles.Effects
{
    /// <summary>一份待写入 SETTER 播放请求的每次调用变量（数值或字符串）。</summary>
    internal readonly struct EffectParameter
    {
        private EffectParameter(string key, double numberValue, string stringValue, bool isNumeric)
        {
            Key = key;
            NumberValue = numberValue;
            StringValue = stringValue;
            IsNumeric = isNumeric;
        }

        internal string Key { get; }
        internal double NumberValue { get; }
        internal string StringValue { get; }
        internal bool IsNumeric { get; }

        internal static EffectParameter Number(string key, double value) =>
            new EffectParameter(Validate(key), value, null, true);

        internal static EffectParameter Text(string key, string value) =>
            new EffectParameter(Validate(key), 0d, value ?? string.Empty, false);

        private static string Validate(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Parameter name is required.", nameof(key));
            return key;
        }
    }
}
