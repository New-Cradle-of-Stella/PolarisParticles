namespace Polaris.Particles.Effects
{
    public readonly struct ParticleKey
    {
        public ParticleKey(string value) => Value = value;

        public string Value { get; }

        public override string ToString() => Value;
    }

    public readonly struct TimelineKey
    {
        public TimelineKey(string value) => Value = value;

        public string Value { get; }

        public override string ToString() => Value;
    }

    public readonly struct AttackGhostKey
    {
        public AttackGhostKey(string value) => Value = value;

        public string Value { get; }

        public override string ToString() => Value;
    }
}
