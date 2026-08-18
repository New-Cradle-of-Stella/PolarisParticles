using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace Polaris.Particles.Debugging
{
    internal static class PEffectDebugServer
    {
        private static Thread _thread;
        private static volatile bool _running;

        internal static void Start()
        {
            if (_thread != null)
                return;

            _running = true;
            _thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "Polaris.Particles.DebugServer",
            };
            _thread.Start();
        }

        internal static void Stop()
        {
            if (_thread == null)
                return;

            _running = false;
            try
            {
                // 唤醒阻塞在 WaitForConnection 的循环，让它看到 _running 已经翻转。
                using (var wakeUp = new NamedPipeClientStream(".", PEffectDebugProtocol.PipeName, PipeDirection.InOut))
                    wakeUp.Connect(200);
            }
            catch
            {
                // The server may already be between connections.
            }

            _thread.Join(TimeSpan.FromSeconds(6));
            _thread = null;
        }

        private static void Loop()
        {
            while (_running)
            {
                try
                {
                    using (var pipe = new NamedPipeServerStream(
                        PEffectDebugProtocol.PipeName,
                        PipeDirection.InOut,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.None))
                    {
                        pipe.WaitForConnection();
                        if (!_running)
                            break;
                        Handle(pipe);
                    }
                }
                catch (Exception ex)
                {
                    if (!_running)
                        break;
                    PolarisAPI.Errors.Report(ex, "PolarisParticles debug pipe", typeof(PEffectDebugServer).Assembly);
                }
            }
        }

        private static void Handle(Stream pipe)
        {
            IReadOnlyList<PEffectDebugWireFile> files = ReadFiles(pipe);
            (bool ok, string message) = PEffectDebugPump.EnqueueAndWait(files, TimeSpan.FromSeconds(10));
            using (var writer = new BinaryWriter(pipe, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(ok);
                writer.Write(message ?? string.Empty);
                writer.Flush();
            }
        }

        private static IReadOnlyList<PEffectDebugWireFile> ReadFiles(Stream pipe)
        {
            using (var reader = new BinaryReader(pipe, Encoding.UTF8, leaveOpen: true))
            {
                int version = reader.ReadInt32();
                if (version != PEffectDebugProtocol.Version)
                    throw new InvalidDataException($"Unsupported particle debug protocol {version}; expected {PEffectDebugProtocol.Version}.");

                int count = reader.ReadInt32();
                if (count <= 0 || count > PEffectDebugProtocol.MaxFiles)
                    throw new InvalidDataException($"Invalid .peffect file count: {count}.");

                var files = new List<PEffectDebugWireFile>(count);
                int totalChars = 0;
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadString();
                    string path = reader.ReadString();
                    string text = reader.ReadString();
                    totalChars += text.Length;
                    if (text.Length > PEffectDebugProtocol.MaxFileChars || totalChars > PEffectDebugProtocol.MaxTotalChars)
                        throw new InvalidDataException("The incoming .peffect snapshot exceeds the debug channel size limit.");
                    files.Add(new PEffectDebugWireFile(name, path, text));
                }

                return files;
            }
        }
    }
}
