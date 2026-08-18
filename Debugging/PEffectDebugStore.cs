using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace Polaris.Particles.Debugging
{
    internal static class PEffectDebugStore
    {
        private const string RuntimeTextHeader = "/* PolarisParticles live debug snapshot */\n";

        private static volatile PEffectDebugSnapshot _current = PEffectDebugSnapshot.Empty;

        internal static PEffectDebugSnapshot Current => _current;

        internal static string RuntimeText => _current.RuntimeText;

        internal static bool TryGetText(string virtualName, out string text)
        {
            if (_current.ByName.TryGetValue(PEffectSyntax.ToVirtualName(virtualName), out PEffectDebugDocument document))
            {
                text = document.Text;
                return true;
            }

            text = null;
            return false;
        }

        internal static PEffectDebugSnapshot Replace(IReadOnlyList<PEffectDebugWireFile> files)
        {
            if (files == null || files.Count == 0)
                throw new InvalidDataException("The debug snapshot contains no .peffect files.");
            if (files.Count > PEffectDebugProtocol.MaxFiles)
                throw new InvalidDataException($"The debug snapshot contains {files.Count} files; the limit is {PEffectDebugProtocol.MaxFiles}.");

            int totalChars = 0;
            var documents = new List<PEffectDebugDocument>(files.Count);
            var byName = new Dictionary<string, PEffectDebugDocument>(StringComparer.Ordinal);

            foreach (PEffectDebugWireFile file in files)
            {
                string name = NormalizeAndValidateName(file.VirtualName);
                string text = PEffectSyntax.NormalizeText(file.Text);
                if (text.Length > PEffectDebugProtocol.MaxFileChars)
                    throw new InvalidDataException($"{name}.peffect is larger than {PEffectDebugProtocol.MaxFileChars} characters.");

                totalChars += text.Length;
                if (totalChars > PEffectDebugProtocol.MaxTotalChars)
                    throw new InvalidDataException($"The .peffect snapshot is larger than {PEffectDebugProtocol.MaxTotalChars} characters.");

                if (byName.ContainsKey(name))
                    throw new InvalidDataException($"Duplicate .peffect virtual name: {name}");

                string displayPath = string.IsNullOrWhiteSpace(file.DisplayPath)
                    ? name + PEffectSyntax.Extension
                    : file.DisplayPath;
                var document = new PEffectDebugDocument(
                    name, displayPath, text, PEffectSyntax.Parse(text, displayPath));
                documents.Add(document);
                byName.Add(name, document);
            }

            ValidateIncludesExist(documents, byName);

            // 调试快照要求 @include 全部命中，因此这里的包含表就是已解析的包含表。
            Dictionary<string, PEffectBundleFile> bundle = documents.ToDictionary(
                document => document.VirtualName,
                document => new PEffectBundleFile(document.Text, document.Includes),
                StringComparer.Ordinal);

            PEffectSyntax.ValidateNoIncludeCycles(bundle, ".peffect");
            PEffectSyntax.ValidateUniqueSectionKeys(documents.SelectMany(document =>
                document.Sections.Select(section => (section, document.DisplayPath))));

            if (!PEffectSyntax.HasRootFile(bundle))
                throw new InvalidDataException("The .peffect include graph has no root file (it is probably cyclic).");

            var snapshot = new PEffectDebugSnapshot(
                new ReadOnlyCollection<PEffectDebugDocument>(
                    documents.OrderBy(document => document.VirtualName, StringComparer.Ordinal).ToList()),
                new ReadOnlyDictionary<string, PEffectDebugDocument>(byName),
                PEffectSyntax.BuildRuntimeText(RuntimeTextHeader, bundle),
                _current.Generation + 1,
                DateTime.Now);
            _current = snapshot;
            return snapshot;
        }

        internal static void Clear()
        {
            _current = PEffectDebugSnapshot.Empty;
        }

        private static void ValidateIncludesExist(
            IEnumerable<PEffectDebugDocument> documents,
            IReadOnlyDictionary<string, PEffectDebugDocument> byName)
        {
            foreach (PEffectDebugDocument document in documents)
            foreach (string include in document.Includes)
                if (!byName.ContainsKey(include))
                    throw new InvalidDataException(
                        $"{document.DisplayPath} includes missing file '{include}.peffect'.");
        }

        private static string NormalizeAndValidateName(string value)
        {
            string name = PEffectSyntax.ToVirtualName(value);
            if (!PEffectSyntax.IsValidName(name))
                throw new InvalidDataException($"Invalid .peffect virtual name: '{value}'. File names may only contain letters, digits, and underscores.");
            if (PEffectSyntax.IsReservedName(name))
                throw new InvalidDataException("The virtual name '__main' is reserved by the game particle loader.");
            return name;
        }
    }
}
