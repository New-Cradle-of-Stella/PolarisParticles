using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;
using XX;

namespace Polaris.Particles.Debugging
{
    /// <summary>把命名管道线程收到的快照搬到 Unity 主线程应用，并把结果回送给等待中的调用方。</summary>
    internal sealed class PEffectDebugPump : MonoBehaviour
    {
        private sealed class Request
        {
            internal Request(IReadOnlyList<PEffectDebugWireFile> files)
            {
                Files = files;
            }

            internal readonly ManualResetEventSlim Done = new ManualResetEventSlim(false);
            internal IReadOnlyList<PEffectDebugWireFile> Files { get; }
            internal bool Ok;
            internal string Message;
        }

        private static readonly ConcurrentQueue<Request> Queue = new ConcurrentQueue<Request>();
        private static PEffectDebugPump _instance;

        internal static void EnsureInstance(GameObject root)
        {
            if (_instance == null)
                _instance = root.AddComponent<PEffectDebugPump>();
        }

        internal static (bool ok, string message) EnqueueAndWait(
            IReadOnlyList<PEffectDebugWireFile> files,
            TimeSpan timeout)
        {
            if (_instance == null)
                return (false, "Particle debug is not ready yet.");

            var request = new Request(files);
            Queue.Enqueue(request);
            if (!request.Done.Wait(timeout))
                return (false, "The game main thread did not apply the particle snapshot in time.");
            return (request.Ok, request.Message);
        }

        private void Update()
        {
            while (Queue.TryDequeue(out Request request))
            {
                try
                {
                    PEffectDebugPage.StopPreview();
                    PEffectDebugSnapshot snapshot = PEffectDebugStore.Replace(request.Files);
                    EfParticleManager.reloadParticleCsv(true);
                    ValidateLoadedSections(snapshot);
                    PEffectDebugPage.OnSnapshotApplied(snapshot);
                    request.Ok = true;
                    request.Message = $"Loaded {snapshot.Documents.Count} .peffect file(s), generation {snapshot.Generation}. Press F9 in game to inspect and play.";
                    Debug.Log("[PolarisParticles] " + request.Message);
                }
                catch (Exception ex)
                {
                    request.Ok = false;
                    request.Message = ex.Message;
                }
                finally
                {
                    request.Done.Set();
                }
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;

            while (Queue.TryDequeue(out Request request))
            {
                request.Ok = false;
                request.Message = "Particle debug is shutting down.";
                request.Done.Set();
            }
        }

        /// <summary>原版加载器只在重载后才知道键是否有效，所以逐个回查一遍。</summary>
        private static void ValidateLoadedSections(PEffectDebugSnapshot snapshot)
        {
            string[] missing = snapshot.Documents
                .SelectMany(document => document.Sections)
                .Where(section => !IsLoaded(section))
                .Select(section => section.Label + " " + section.Key)
                .ToArray();
            if (missing.Length != 0)
                throw new InvalidDataException(
                    "The original particle loader rejected these definitions: " + string.Join(", ", missing) + ".");
        }

        private static bool IsLoaded(PEffectSection section)
        {
            switch (section.Kind)
            {
                case PEffectSectionKind.Particle:
                    return EfParticleManager.Get(section.Key, true, true) != null;
                case PEffectSectionKind.Timeline:
                    return EfParticleManager.GetSetterScript(section.Key) != null;
                case PEffectSectionKind.AttackGhost:
                    return EfParticleManager.GetAGD(section.Key) != null;
                default:
                    return false;
            }
        }
    }
}
