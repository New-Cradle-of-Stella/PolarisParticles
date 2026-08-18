using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Polaris.Particles
{
    internal enum PEffectSectionKind
    {
        Particle,
        Timeline,
        AttackGhost,
    }

    /// <summary>一处分节头 <c>/* ___ KEY ___ */</c> 的解析结果。</summary>
    internal sealed class PEffectSection
    {
        internal PEffectSection(PEffectSectionKind kind, string key, int line)
        {
            Kind = kind;
            Key = key;
            Line = line;
        }

        internal PEffectSectionKind Kind { get; }
        internal string Key { get; }
        internal int Line { get; }

        /// <summary>原版脚本里的分节名，同时用于错误消息与调试页显示。</summary>
        internal string Label => Kind switch
        {
            PEffectSectionKind.Timeline => "SETTER",
            PEffectSectionKind.AttackGhost => "AGD",
            _ => "Particle",
        };
    }

    /// <summary>一份 .peffect 的语法轮廓：它包含哪些文件、又定义了哪些分节。</summary>
    internal sealed class PEffectOutline
    {
        internal PEffectOutline(IList<string> includes, IList<PEffectSection> sections)
        {
            Includes = new ReadOnlyCollection<string>(includes);
            Sections = new ReadOnlyCollection<PEffectSection>(sections);
        }

        internal IReadOnlyList<string> Includes { get; }
        internal IReadOnlyList<PEffectSection> Sections { get; }
    }

    /// <summary>参与运行时合并的一份文件：文本，以及其中能在同一批文件里解析到的 @include。</summary>
    internal readonly struct PEffectBundleFile
    {
        internal PEffectBundleFile(string text, IReadOnlyList<string> resolvedIncludes)
        {
            Text = text;
            ResolvedIncludes = resolvedIncludes;
        }

        internal string Text { get; }
        internal IReadOnlyList<string> ResolvedIncludes { get; }
    }

    /// <summary>
    /// .peffect 的文本语法与批次校验：分节头、@include 图、合并出的运行时文本。
    /// 内嵌登记（<see cref="Effects.EffectFileRegistry"/>）与调试快照
    /// （<see cref="Debugging.PEffectDebugStore"/>）共用这一份实现。
    /// </summary>
    internal static class PEffectSyntax
    {
        internal const string Extension = ".peffect";
        internal const string ReservedName = "__main";

        private const string ParticleExtension = ".particle";
        private const string TimelinePrefix = "SETTER.";
        private const string AttackGhostPrefix = "AGD.";

        private static readonly Regex SectionPattern = new Regex(
            @"^\s*/\*\s*___\s*(?<key>.*?)\s*___\s*\*/\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex IncludePattern = new Regex(
            @"^\s*@(?<name>[A-Za-z0-9_.-]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex NamePattern = new Regex(
            @"^\w+$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>虚拟文件名与分节键的通用规则：非空白，且只含字母、数字和下划线。</summary>
        internal static bool IsValidName(string name) =>
            !string.IsNullOrWhiteSpace(name) && NamePattern.IsMatch(name);

        internal static bool IsReservedName(string name) =>
            string.Equals(name, ReservedName, StringComparison.Ordinal);

        /// <summary>去掉首尾空白与 .peffect / .particle 扩展名，得到虚拟文件名。</summary>
        internal static string ToVirtualName(string value)
        {
            string name = (value ?? string.Empty).Trim();
            if (name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - Extension.Length);
            if (name.EndsWith(ParticleExtension, StringComparison.OrdinalIgnoreCase))
                return name.Substring(0, name.Length - ParticleExtension.Length);
            return name;
        }

        /// <summary>统一换行为 \n，并保证文本以换行结尾。</summary>
        internal static string NormalizeText(string value)
        {
            string text = (value ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            return text.EndsWith("\n", StringComparison.Ordinal) ? text : text + "\n";
        }

        /// <summary>解析一份 .peffect 的分节与包含；origin 只出现在错误消息里。</summary>
        internal static PEffectOutline Parse(string text, string origin)
        {
            var includes = new List<string>();
            var sections = new List<PEffectSection>();
            int lineNumber = 0;
            foreach (string line in ReadLines(text))
            {
                lineNumber++;
                Match section = SectionPattern.Match(line);
                if (section.Success)
                {
                    sections.Add(ParseSection(section.Groups["key"].Value.Trim(), lineNumber, origin));
                    continue;
                }

                if (TryMatchInclude(line, out string include))
                    includes.Add(include);
            }

            return new PEffectOutline(includes, sections);
        }

        /// <summary>同种分节的键在一批文件里必须唯一；origin 用于指出冲突双方。</summary>
        internal static void ValidateUniqueSectionKeys(IEnumerable<(PEffectSection Section, string Origin)> sections)
        {
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach ((PEffectSection section, string origin) in sections)
            {
                string compound = section.Label + ":" + section.Key;
                if (owners.TryGetValue(compound, out string previous))
                    throw new InvalidDataException(
                        $"Duplicate {section.Label} key '{section.Key}' in {previous} and {origin}.");
                owners.Add(compound, origin);
            }
        }

        /// <summary>深度优先检测 @include 环；label 用于区分内嵌登记与调试快照的消息。</summary>
        internal static void ValidateNoIncludeCycles(
            IReadOnlyDictionary<string, PEffectBundleFile> bundle,
            string label)
        {
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in bundle.Keys)
                VisitIncludes(name, bundle, label, visiting, visited);
        }

        /// <summary>是否存在没有被其他文件包含的根文件；空批次视为成立。</summary>
        internal static bool HasRootFile(IReadOnlyDictionary<string, PEffectBundleFile> bundle)
        {
            if (bundle.Count == 0)
                return true;

            var included = new HashSet<string>(StringComparer.Ordinal);
            foreach (PEffectBundleFile file in bundle.Values)
                foreach (string include in file.ResolvedIncludes)
                    included.Add(include);

            return bundle.Keys.Any(name => !included.Contains(name));
        }

        /// <summary>
        /// 把整批文件合并成一份交给原版加载器的文本：被包含的文件先出现，
        /// 已解析的 @include 行随之删除，未解析的按原样保留给原版处理。
        /// </summary>
        internal static string BuildRuntimeText(string header, IReadOnlyDictionary<string, PEffectBundleFile> bundle)
        {
            var builder = new StringBuilder(header);
            var visited = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in bundle.Keys.OrderBy(name => name, StringComparer.Ordinal))
                AppendBundleFile(name, bundle, visited, builder);
            return builder.ToString();
        }

        private static PEffectSection ParseSection(string raw, int line, string origin)
        {
            PEffectSectionKind kind = PEffectSectionKind.Particle;
            string key = raw;
            if (raw.StartsWith(TimelinePrefix, StringComparison.Ordinal))
            {
                kind = PEffectSectionKind.Timeline;
                key = raw.Substring(TimelinePrefix.Length);
            }
            else if (raw.StartsWith(AttackGhostPrefix, StringComparison.Ordinal))
            {
                kind = PEffectSectionKind.AttackGhost;
                key = raw.Substring(AttackGhostPrefix.Length);
            }

            if (!IsValidName(key))
                throw new InvalidDataException($"{origin}:{line}: invalid section key '{raw}'.");
            return new PEffectSection(kind, key, line);
        }

        private static void VisitIncludes(
            string name,
            IReadOnlyDictionary<string, PEffectBundleFile> bundle,
            string label,
            ISet<string> visiting,
            ISet<string> visited)
        {
            if (visited.Contains(name))
                return;
            if (!visiting.Add(name))
                throw new InvalidDataException($"Cyclic {label} include detected at '{name}'.");

            foreach (string include in bundle[name].ResolvedIncludes)
                VisitIncludes(include, bundle, label, visiting, visited);

            visiting.Remove(name);
            visited.Add(name);
        }

        private static void AppendBundleFile(
            string name,
            IReadOnlyDictionary<string, PEffectBundleFile> bundle,
            ISet<string> visited,
            StringBuilder builder)
        {
            if (!visited.Add(name))
                return;

            PEffectBundleFile file = bundle[name];
            foreach (string include in file.ResolvedIncludes)
                AppendBundleFile(include, bundle, visited, builder);

            builder.AppendLine().Append("/* source: ").Append(name).AppendLine(Extension + " */");
            foreach (string line in ReadLines(file.Text))
            {
                if (TryMatchInclude(line, out string include) && file.ResolvedIncludes.Contains(include))
                    continue;
                builder.AppendLine(line);
            }
        }

        private static bool TryMatchInclude(string line, out string name)
        {
            Match match = IncludePattern.Match(line);
            name = match.Success ? ToVirtualName(match.Groups["name"].Value) : null;
            return match.Success;
        }

        private static IEnumerable<string> ReadLines(string text)
        {
            using (var reader = new StringReader(text))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                    yield return line;
            }
        }
    }
}
