using System.Reflection;

namespace Polaris.Particles.Effects
{
    /// <summary>一份已经登记的内嵌 .peffect 资源的只读信息。</summary>
    public sealed class EffectFileRecord
    {
        internal EffectFileRecord(Assembly owner, string resourceName, string virtualName, string text)
        {
            Owner = owner;
            ResourceName = resourceName;
            VirtualName = virtualName;
            Text = text;
        }

        public Assembly Owner { get; }
        public string ResourceName { get; }
        public string VirtualName { get; }
        public int CharacterCount => Text.Length;
        internal string Text { get; }
    }
}
