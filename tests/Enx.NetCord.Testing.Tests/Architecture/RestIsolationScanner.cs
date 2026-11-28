using System.Text;
using System.Text.RegularExpressions;

namespace Enx.NetCord.Testing.Tests.Architecture;

/// <summary>One breach of the <c>Rest/</c> isolation rules found in a source file.</summary>
/// <param name="File">Path relative to the scanned root, with <c>/</c> separators.</param>
/// <param name="Line">1-based line of the offending token.</param>
/// <param name="Type">Nearest enclosing (or preceding) type declaration, or <c>?</c>.</param>
/// <param name="Rule">Which rule was broken.</param>
/// <param name="Text">The offending token(s).</param>
internal sealed record RestIsolationViolation(string File, int Line, string Type, string Rule, string Text)
{
    public override string ToString() => $"{File}:{Line} [{Type}] {Rule}: {Text}";
}

/// <summary>
/// Source-text detector for ADR 0004 (amendment 04B): code under <c>Rest/</c> must reach guilds and channels through
/// <c>GuildAccess</c> as <c>StateCaller.Bot</c>. It works on the text with comments, string literals and character
/// literals blanked out (interpolation holes stay code), so line numbers are preserved and a mention in a comment or
/// a message is never a violation. Matching is by name, not by receiver type: IL inspection cannot see
/// <c>StateCaller.Fixture</c> because the compiler inlines enum constants.
/// </summary>
/// <remarks>
/// Known limits of the cast rule (a textual heuristic, not a semantic analysis): a cast is recognised only as a
/// parenthesised <c>StateCaller</c> name (plain, namespace-qualified, <c>global::</c>-qualified or nullable), and
/// <c>default(StateCaller)</c> is reported too because the zero value is undefined. <c>typeof</c> and <c>nameof</c>
/// are skipped. Not detected: a <c>Fixture</c> value obtained without spelling <c>StateCaller.Fixture</c> or a cast,
/// such as <c>Enum.Parse</c>, <c>Enum.ToObject</c> or enum arithmetic.
/// </remarks>
internal static partial class RestIsolationScanner
{
    private const string FixtureRule = "uses a StateCaller value other than Bot (Fixture is reserved to the seed and test commands)";
    private const string AliasRule = "imports StateCaller under a name that hides its values (using static / alias)";
    private const string CastRule = "builds a StateCaller from a number or default";
    private const string BypassRule = "calls a FakeDiscordState member that bypasses GuildAccess";

    [GeneratedRegex(@"\bStateCaller\s*\.\s*(?!Bot\b)(?<value>\w+)")]
    private static partial Regex OtherCallerValue();

    [GeneratedRegex(@"\busing\s+(?:static\s+[\w.:]*|\w+\s*=\s*[\w.:]*)\bStateCaller\s*;")]
    private static partial Regex CallerImport();

    // A cast `(StateCaller)n`, `(X.StateCaller)n`, `(global::X.StateCaller)n` or `(StateCaller?)n`, and
    // `default(StateCaller)` (the undefined zero value), but not `typeof(StateCaller)` or `nameof(StateCaller)`.
    [GeneratedRegex(@"(?<!\b(?:typeof|nameof)\s*)\(\s*(?:global\s*::\s*)?(?:[\w.]*\.)?StateCaller\s*\??\s*\)")]
    private static partial Regex CallerCast();

    [GeneratedRegex(@"\b(?:record\s+(?:class|struct)|class|struct|record|interface|enum)\s+(?<name>\w+)")]
    private static partial Regex TypeDeclaration();

    /// <summary>Scans one file.</summary>
    /// <param name="file">Name used in the report.</param>
    /// <param name="source">The C# text.</param>
    /// <param name="forbiddenMembers">Names of the <c>FakeDiscordState</c> members that <c>Rest/</c> must not call.</param>
    internal static IReadOnlyList<RestIsolationViolation> Scan(string file, string source, IReadOnlySet<string> forbiddenMembers)
    {
        var code = Sanitize(source);
        var violations = new List<RestIsolationViolation>();

        void Report(Match match, string rule, string text) =>
            violations.Add(new RestIsolationViolation(file, LineOf(code, match.Index), TypeAt(code, match.Index), rule, text));

        foreach (Match match in OtherCallerValue().Matches(code))
        {
            Report(match, FixtureRule, $"StateCaller.{match.Groups["value"].Value}");
        }

        foreach (Match match in CallerImport().Matches(code))
        {
            Report(match, AliasRule, Collapse(match.Value));
        }

        foreach (Match match in CallerCast().Matches(code))
        {
            Report(match, CastRule, Collapse(match.Value));
        }

        if (forbiddenMembers.Count > 0)
        {
            var members = new Regex(
                @"\.\s*(?<member>" + string.Join('|', forbiddenMembers.Select(Regex.Escape)) + @")\b",
                RegexOptions.CultureInvariant);
            foreach (Match match in members.Matches(code))
            {
                Report(match, BypassRule, $".{match.Groups["member"].Value}");
            }
        }

        return [.. violations.OrderBy(v => v.Line)];
    }

    private static string Collapse(string text) => Regex.Replace(text, @"\s+", " ");

    private static int LineOf(string code, int index)
    {
        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (code[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string TypeAt(string code, int index)
    {
        var name = "?";
        foreach (Match match in TypeDeclaration().Matches(code))
        {
            if (match.Index > index)
            {
                break;
            }

            name = match.Groups["name"].Value;
        }

        return name;
    }

    /// <summary>
    /// Returns <paramref name="source"/> with comments, string contents and character literals replaced by spaces
    /// (line breaks kept). Interpolation holes of interpolated strings are kept as code.
    /// </summary>
    internal static string Sanitize(string source)
    {
        var output = source.ToCharArray();
        new Lexer(source, output).ScanCode(0, untilClosingBrace: false);
        return new string(output);
    }

    private sealed class Lexer(string text, char[] output)
    {
        public int ScanCode(int i, bool untilClosingBrace)
        {
            var depth = 0;
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '/' && Peek(i + 1) == '/')
                {
                    while (i < text.Length && text[i] != '\n')
                    {
                        Blank(i++);
                    }
                }
                else if (c == '/' && Peek(i + 1) == '*')
                {
                    Blank(i++);
                    Blank(i++);
                    while (i < text.Length && !(text[i] == '*' && Peek(i + 1) == '/'))
                    {
                        Blank(i++);
                    }

                    if (i < text.Length)
                    {
                        Blank(i++);
                        Blank(i++);
                    }
                }
                else if (c == '\'')
                {
                    i = SkipChar(i);
                }
                else if (c == '"')
                {
                    i = ScanString(i);
                }
                else if (c == '{')
                {
                    depth++;
                    i++;
                }
                else if (c == '}')
                {
                    if (untilClosingBrace && depth == 0)
                    {
                        return i;
                    }

                    depth--;
                    i++;
                }
                else
                {
                    i++;
                }
            }

            return i;
        }

        private int SkipChar(int i)
        {
            Blank(i++);
            while (i < text.Length && text[i] != '\'' && text[i] != '\n')
            {
                if (text[i] == '\\' && i + 1 < text.Length)
                {
                    Blank(i++);
                }

                Blank(i++);
            }

            if (i < text.Length && text[i] == '\'')
            {
                Blank(i++);
            }

            return i;
        }

        private int ScanString(int start)
        {
            var verbatim = false;
            var dollars = 0;
            for (var back = start - 1; back >= 0 && text[back] is '$' or '@'; back--)
            {
                if (text[back] == '@')
                {
                    verbatim = true;
                }
                else
                {
                    dollars++;
                }
            }

            var quotes = 0;
            while (start + quotes < text.Length && text[start + quotes] == '"')
            {
                quotes++;
            }

            // `""` is an empty string, not the opening of a raw string.
            if (quotes == 2)
            {
                Blank(start);
                Blank(start + 1);
                return start + 2;
            }

            var interpolated = dollars > 0;
            return quotes >= 3 && !verbatim
                ? ScanRawString(start, quotes, interpolated)
                : ScanQuoted(start, verbatim, interpolated);
        }

        private int ScanQuoted(int i, bool verbatim, bool interpolated)
        {
            Blank(i++);
            while (i < text.Length)
            {
                var c = text[i];
                if (c == '"')
                {
                    if (verbatim && Peek(i + 1) == '"')
                    {
                        Blank(i++);
                        Blank(i++);
                        continue;
                    }

                    Blank(i++);
                    return i;
                }

                if (!verbatim && c == '\\' && i + 1 < text.Length)
                {
                    Blank(i++);
                    Blank(i++);
                    continue;
                }

                if (!verbatim && c == '\n')
                {
                    return i; // Unterminated: stop at the end of the line.
                }

                if (interpolated && c == '{')
                {
                    if (Peek(i + 1) == '{')
                    {
                        Blank(i++);
                        Blank(i++);
                        continue;
                    }

                    i = ScanCode(i + 1, untilClosingBrace: true) + 1;
                    continue;
                }

                Blank(i++);
            }

            return i;
        }

        private int ScanRawString(int i, int quotes, bool interpolated)
        {
            for (var q = 0; q < quotes; q++)
            {
                Blank(i++);
            }

            while (i < text.Length)
            {
                if (text[i] == '"' && CountQuotes(i) >= quotes)
                {
                    for (var q = 0; q < quotes; q++)
                    {
                        Blank(i++);
                    }

                    return i;
                }

                if (interpolated && text[i] == '{')
                {
                    i = ScanCode(i + 1, untilClosingBrace: true) + 1;
                    continue;
                }

                Blank(i++);
            }

            return i;
        }

        private int CountQuotes(int i)
        {
            var count = 0;
            while (i + count < text.Length && text[i + count] == '"')
            {
                count++;
            }

            return count;
        }

        private char Peek(int i) => i < text.Length ? text[i] : '\0';

        private void Blank(int i)
        {
            if (i < output.Length && output[i] is not ('\n' or '\r'))
            {
                output[i] = ' ';
            }
        }
    }

    /// <summary>The repository root, found from the test assembly's directory by its solution file.</summary>
    internal static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Enx.NetCord.Testing.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException(
            $"Enx.NetCord.Testing.slnx was not found above '{AppContext.BaseDirectory}': the architecture test cannot locate src/.");
    }

    /// <summary>Scans every <c>.cs</c> file under a directory (ignoring <c>bin</c> and <c>obj</c>).</summary>
    internal static (int FileCount, IReadOnlyList<RestIsolationViolation> Violations) ScanDirectory(
        string root,
        string directory,
        IReadOnlySet<string> forbiddenMembers)
    {
        var files = Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(directory, path).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"))
            .Order(StringComparer.Ordinal)
            .ToList();

        var violations = new List<RestIsolationViolation>();
        foreach (var path in files)
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            violations.AddRange(Scan(relative, File.ReadAllText(path, Encoding.UTF8), forbiddenMembers));
        }

        return (files.Count, violations);
    }
}
