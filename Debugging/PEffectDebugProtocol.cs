namespace Polaris.Particles.Debugging
{
    /// <summary>PolarisTools 与游戏内粒子调试通道共用的线协议常量。</summary>
    public static class PEffectDebugProtocol
    {
        public const int Version = 1;
        public const string PipeName = "Polaris.Particles.Debug";
        public const int MaxFiles = 256;
        public const int MaxFileChars = 2 * 1024 * 1024;
        public const int MaxTotalChars = 16 * 1024 * 1024;
    }

    public sealed class PEffectDebugWireFile
    {
        public PEffectDebugWireFile(string virtualName, string displayPath, string text)
        {
            VirtualName = virtualName ?? string.Empty;
            DisplayPath = displayPath ?? string.Empty;
            Text = text ?? string.Empty;
        }

        public string VirtualName { get; }
        public string DisplayPath { get; }
        public string Text { get; }
    }
}
