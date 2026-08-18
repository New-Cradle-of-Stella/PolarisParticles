using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Polaris.Particles.Effects
{
    internal sealed class EffectFileRegistry
    {
        internal const int MaxFileChars = 2 * 1024 * 1024;
        internal const int MaxTotalChars = 16 * 1024 * 1024;

        private const string RuntimeTextHeader = "/* PolarisParticles embedded effects */\n";

        internal static readonly EffectFileRegistry Instance = new EffectFileRegistry();

        private readonly object _gate = new object();
        private readonly List<EffectFileRecord> _pending = new List<EffectFileRecord>();
        private volatile EffectFileSnapshot _snapshot = EffectFileSnapshot.Empty;
        private bool _sealed;

        private EffectFileRegistry() { }

        internal IReadOnlyList<EffectFileRecord> Files => _snapshot.Files;
        internal string RuntimeText => _snapshot.RuntimeText;
        internal bool IsSealed => _sealed;

        internal void RegisterEmbedded(Assembly owner, string resourceName, string virtualName)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrWhiteSpace(resourceName))
                throw new ArgumentException("Embedded resource name is required.", nameof(resourceName));

            lock (_gate)
            {
                if (_sealed)
                    throw new InvalidOperationException("The .peffect registry is already sealed. Register embedded effects from the plugin Awake phase.");

                EffectFileRecord existingResource = _pending.FirstOrDefault(record =>
                    record.Owner == owner && string.Equals(record.ResourceName, resourceName, StringComparison.Ordinal));
                if (existingResource != null)
                {
                    string requested = virtualName == null ? existingResource.VirtualName : NormalizeVirtualName(virtualName);
                    if (!string.Equals(existingResource.VirtualName, requested, StringComparison.Ordinal))
                        throw new InvalidOperationException(
                            $"Embedded resource '{resourceName}' is already registered as '{existingResource.VirtualName}', not '{requested}'.");
                    return;
                }

                string name = NormalizeVirtualName(virtualName ?? DeriveVirtualName(resourceName));
                if (_pending.Any(record => string.Equals(record.VirtualName, name, StringComparison.Ordinal)))
                    throw new InvalidOperationException($"Duplicate .peffect virtual file name: '{name}'. Prefix effect file names with the mod ID.");

                _pending.Add(new EffectFileRecord(owner, resourceName, name, ReadEmbeddedText(owner, resourceName)));
            }
        }

        internal void ScanEmbedded(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null)
                return;

            foreach (Assembly assembly in assemblies.Where(value => value != null).Distinct())
            {
                string[] resources;
                try
                {
                    resources = assembly.GetManifestResourceNames();
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Could not inspect embedded resources in assembly '{assembly.GetName().Name}'.", ex);
                }

                foreach (string resource in resources
                             .Where(name => name.EndsWith(PEffectSyntax.Extension, StringComparison.OrdinalIgnoreCase))
                             .OrderBy(name => name, StringComparer.Ordinal))
                {
                    RegisterEmbedded(assembly, resource, null);
                }
            }
        }

        internal EffectFileSnapshot Seal()
        {
            lock (_gate)
            {
                if (_sealed)
                    return _snapshot;

                int totalChars = _pending.Sum(record => record.Text.Length);
                if (totalChars > MaxTotalChars)
                    throw new InvalidDataException($"Embedded .peffect resources total {totalChars} characters; the limit is {MaxTotalChars}.");

                List<EffectFileRecord> ordered = _pending
                    .OrderBy(record => record.Owner.FullName, StringComparer.Ordinal)
                    .ThenBy(record => record.VirtualName, StringComparer.Ordinal)
                    .ToList();
                Dictionary<string, EffectFileRecord> byName =
                    ordered.ToDictionary(record => record.VirtualName, StringComparer.Ordinal);
                Dictionary<string, PEffectOutline> outlines = ordered.ToDictionary(
                    record => record.VirtualName,
                    record => PEffectSyntax.Parse(record.Text, record.ResourceName),
                    StringComparer.Ordinal);

                PEffectSyntax.ValidateUniqueSectionKeys(ordered.SelectMany(record =>
                    outlines[record.VirtualName].Sections.Select(section => (section, $"'{record.ResourceName}'"))));

                // 只有登记在同一批里的 @include 才由我们合并，其余原样留给原版加载器。
                Dictionary<string, PEffectBundleFile> bundle = ordered.ToDictionary(
                    record => record.VirtualName,
                    record => new PEffectBundleFile(
                        record.Text,
                        outlines[record.VirtualName].Includes.Where(byName.ContainsKey).ToList()),
                    StringComparer.Ordinal);

                PEffectSyntax.ValidateNoIncludeCycles(bundle, "embedded .peffect");
                if (!PEffectSyntax.HasRootFile(bundle))
                    throw new InvalidDataException("The embedded .peffect include graph has no root file; check for an include cycle.");

                _snapshot = new EffectFileSnapshot(
                    new ReadOnlyCollection<EffectFileRecord>(ordered),
                    new ReadOnlyDictionary<string, EffectFileRecord>(byName),
                    PEffectSyntax.BuildRuntimeText(RuntimeTextHeader, bundle));
                _sealed = true;
                return _snapshot;
            }
        }

        internal bool TryGetText(string virtualName, out string text)
        {
            if (_snapshot.ByName.TryGetValue(PEffectSyntax.ToVirtualName(virtualName), out EffectFileRecord record))
            {
                text = record.Text;
                return true;
            }

            text = null;
            return false;
        }

        internal void Reset()
        {
            lock (_gate)
            {
                _pending.Clear();
                _snapshot = EffectFileSnapshot.Empty;
                _sealed = false;
            }
        }

        private static string ReadEmbeddedText(Assembly owner, string resourceName)
        {
            using (Stream stream = owner.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException(
                        $"Assembly '{owner.GetName().Name}' does not contain embedded resource '{resourceName}'.",
                        resourceName);

                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string text = PEffectSyntax.NormalizeText(reader.ReadToEnd());
                    if (text.Length > MaxFileChars)
                        throw new InvalidDataException($"{resourceName} exceeds the {MaxFileChars}-character .peffect limit.");
                    return text;
                }
            }
        }

        private static string DeriveVirtualName(string resourceName)
        {
            string withoutExtension = resourceName.Substring(0, resourceName.Length - PEffectSyntax.Extension.Length);
            int separator = withoutExtension.LastIndexOf('.');
            return separator < 0 ? withoutExtension : withoutExtension.Substring(separator + 1);
        }

        private static string NormalizeVirtualName(string value)
        {
            string name = PEffectSyntax.ToVirtualName(value);
            if (!PEffectSyntax.IsValidName(name))
                throw new InvalidDataException($"Invalid .peffect virtual name '{value}'. Use only letters, digits, and underscores.");
            if (PEffectSyntax.IsReservedName(name))
                throw new InvalidDataException("The .peffect virtual name '__main' is reserved by the game.");
            return name;
        }
    }

    internal sealed class EffectFileSnapshot
    {
        internal static readonly EffectFileSnapshot Empty = new EffectFileSnapshot(
            Array.AsReadOnly(Array.Empty<EffectFileRecord>()),
            new ReadOnlyDictionary<string, EffectFileRecord>(new Dictionary<string, EffectFileRecord>(StringComparer.Ordinal)),
            string.Empty);

        internal EffectFileSnapshot(
            IReadOnlyList<EffectFileRecord> files,
            IReadOnlyDictionary<string, EffectFileRecord> byName,
            string runtimeText)
        {
            Files = files;
            ByName = byName;
            RuntimeText = runtimeText ?? string.Empty;
        }

        internal IReadOnlyList<EffectFileRecord> Files { get; }
        internal IReadOnlyDictionary<string, EffectFileRecord> ByName { get; }
        internal string RuntimeText { get; }
    }
}
