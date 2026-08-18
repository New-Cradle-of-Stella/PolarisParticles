using System;
using System.Collections.Generic;

namespace Polaris.Particles.Debugging
{
    /// <summary>调试快照里的一份 .peffect：原文加上它的语法轮廓。</summary>
    internal sealed class PEffectDebugDocument
    {
        private readonly PEffectOutline _outline;

        internal PEffectDebugDocument(string virtualName, string displayPath, string text, PEffectOutline outline)
        {
            VirtualName = virtualName;
            DisplayPath = displayPath;
            Text = text;
            _outline = outline;
        }

        internal string VirtualName { get; }
        internal string DisplayPath { get; }
        internal string Text { get; }
        internal IReadOnlyList<string> Includes => _outline.Includes;
        internal IReadOnlyList<PEffectSection> Sections => _outline.Sections;
    }

    internal sealed class PEffectDebugSnapshot
    {
        internal static readonly PEffectDebugSnapshot Empty = new PEffectDebugSnapshot(
            Array.AsReadOnly(Array.Empty<PEffectDebugDocument>()),
            new Dictionary<string, PEffectDebugDocument>(StringComparer.Ordinal),
            string.Empty, 0, DateTime.MinValue);

        internal PEffectDebugSnapshot(
            IReadOnlyList<PEffectDebugDocument> documents,
            IReadOnlyDictionary<string, PEffectDebugDocument> byName,
            string runtimeText,
            int generation,
            DateTime updatedAt)
        {
            Documents = documents;
            ByName = byName;
            RuntimeText = runtimeText ?? string.Empty;
            Generation = generation;
            UpdatedAt = updatedAt;
        }

        internal IReadOnlyList<PEffectDebugDocument> Documents { get; }
        internal IReadOnlyDictionary<string, PEffectDebugDocument> ByName { get; }
        internal string RuntimeText { get; }
        internal int Generation { get; }
        internal DateTime UpdatedAt { get; }
    }
}
