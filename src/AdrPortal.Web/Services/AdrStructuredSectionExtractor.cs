using System.Text.RegularExpressions;

namespace AdrPortal.Web.Services;

/// <summary>
/// Extracts key MADR sections into structured data for detail-page presentation.
/// </summary>
public sealed class AdrStructuredSectionExtractor
{
    private static readonly Regex FrontMatterRegex = new(
        @"\A---\r?\n(?<frontMatter>.*?)\r?\n---\r?\n?",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex ChosenOptionRegex = new(
        @"Chosen option:\s*""?(?<option>[^""\r\n]+)""?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RationaleRegex = new(
        @"\b(?:because|Rationale:)\s*(?<rationale>.+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Extracts structured option and consequence data from ADR markdown.
    /// </summary>
    /// <param name="markdown">Raw ADR markdown including optional front matter.</param>
    /// <returns>Structured section projection.</returns>
    public AdrStructuredSectionsViewModel Extract(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);

        var markdownBody = RemoveFrontMatter(markdown);
        var lines = NormalizeLines(markdownBody);
        var sectionRanges = BuildSectionRanges(lines);

        var consideredOptionLines = GetSectionLines(lines, sectionRanges, "## Considered Options");
        var prosAndConsLines = GetSectionLines(lines, sectionRanges, "## Pros and Cons of the Options");
        var decisionOutcomeLines = GetSectionLines(lines, sectionRanges, "## Decision Outcome");
        var consequencesLines = GetSectionLines(lines, sectionRanges, "### Consequences");

        var selectedOption = ExtractSelectedOption(decisionOutcomeLines);
        var rationale = ExtractRationale(decisionOutcomeLines);

        var options = ParseProsAndConsOptions(prosAndConsLines);
        if (options.Count is 0)
        {
            options = ParseSimpleConsideredOptions(consideredOptionLines);
        }

        var positives = new List<string>();
        var negatives = new List<string>();
        ExtractConsequences(consequencesLines, positives, negatives);
        if (positives.Count is 0 && negatives.Count is 0)
        {
            ExtractConsequences(decisionOutcomeLines, positives, negatives);
        }

        return new AdrStructuredSectionsViewModel
        {
            SelectedOption = selectedOption,
            Rationale = rationale,
            ConsideredOptions = options,
            PositiveConsequences = positives,
            NegativeConsequences = negatives
        };
    }

    private static string RemoveFrontMatter(string markdown)
    {
        var normalized = markdown.TrimStart('\uFEFF');
        var match = FrontMatterRegex.Match(normalized);
        return match.Success ? normalized[match.Length..] : normalized;
    }

    private static string[] NormalizeLines(string markdownBody)
    {
        return markdownBody
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static Dictionary<string, (int Start, int End)> BuildSectionRanges(string[] lines)
    {
        var headings = new List<(string Heading, int Level, int LineIndex)>();
        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].Trim();
            if (!trimmed.StartsWith('#'))
            {
                continue;
            }

            var level = 0;
            while (level < trimmed.Length && trimmed[level] is '#')
            {
                level++;
            }

            headings.Add((trimmed, level, index));
        }

        var ranges = new Dictionary<string, (int Start, int End)>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headings.Count; index++)
        {
            var current = headings[index];
            var end = lines.Length;
            for (var next = index + 1; next < headings.Count; next++)
            {
                if (headings[next].Level <= current.Level)
                {
                    end = headings[next].LineIndex;
                    break;
                }
            }

            ranges[current.Heading] = (current.LineIndex + 1, end);
        }

        return ranges;
    }

    private static IReadOnlyList<string> GetSectionLines(
        string[] lines,
        IReadOnlyDictionary<string, (int Start, int End)> ranges,
        string heading)
    {
        if (!ranges.TryGetValue(heading, out var range))
        {
            return [];
        }

        return lines[range.Start..range.End];
    }

    private static string? ExtractSelectedOption(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = ChosenOptionRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            var option = match.Groups["option"].Value.Trim();
            return string.IsNullOrWhiteSpace(option) ? null : option;
        }

        return null;
    }

    private static string? ExtractRationale(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            if (!line.Contains("because", StringComparison.OrdinalIgnoreCase)
                && !line.Contains("Rationale:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = RationaleRegex.Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }

            var rationale = match.Groups["rationale"].Value.Trim().Trim('"');
            return string.IsNullOrWhiteSpace(rationale) ? null : rationale;
        }

        return null;
    }

    private static IReadOnlyList<AdrDecisionOptionViewModel> ParseProsAndConsOptions(IReadOnlyList<string> lines)
    {
        var options = new List<AdrDecisionOptionViewModel>();
        var currentName = string.Empty;
        var currentSummary = string.Empty;
        var currentPros = new List<string>();
        var currentCons = new List<string>();
        var mode = string.Empty;

        void FinalizeCurrent()
        {
            if (string.IsNullOrWhiteSpace(currentName))
            {
                return;
            }

            options.Add(new AdrDecisionOptionViewModel
            {
                Name = currentName,
                Summary = currentSummary,
                Pros = currentPros.ToArray(),
                Cons = currentCons.ToArray()
            });
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                FinalizeCurrent();
                currentName = trimmed[4..].Trim();
                currentSummary = string.Empty;
                currentPros = [];
                currentCons = [];
                mode = string.Empty;
                continue;
            }

            if (string.IsNullOrWhiteSpace(currentName))
            {
                continue;
            }

            if (trimmed.StartsWith("* Good", StringComparison.OrdinalIgnoreCase))
            {
                mode = "pro";
                currentPros.Add(RemoveBullet(trimmed));
                continue;
            }

            if (trimmed.StartsWith("* Bad", StringComparison.OrdinalIgnoreCase))
            {
                mode = "con";
                currentCons.Add(RemoveBullet(trimmed));
                continue;
            }

            if (trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                var normalized = RemoveBullet(trimmed);
                if (mode is "pro")
                {
                    currentPros.Add(normalized);
                }
                else if (mode is "con")
                {
                    currentCons.Add(normalized);
                }
                else
                {
                    currentSummary = AppendSentence(currentSummary, normalized);
                }
            }
            else if (!string.IsNullOrWhiteSpace(trimmed))
            {
                currentSummary = AppendSentence(currentSummary, trimmed);
            }
        }

        FinalizeCurrent();
        return options;
    }

    private static IReadOnlyList<AdrDecisionOptionViewModel> ParseSimpleConsideredOptions(IReadOnlyList<string> lines)
    {
        var options = lines
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("* ", StringComparison.Ordinal))
            .Select(line => RemoveBullet(line))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(name => new AdrDecisionOptionViewModel
            {
                Name = name,
                Summary = string.Empty,
                Pros = [],
                Cons = []
            })
            .ToArray();

        return options;
    }

    private static void ExtractConsequences(
        IReadOnlyList<string> lines,
        ICollection<string> positives,
        ICollection<string> negatives)
    {
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("* ", StringComparison.Ordinal))
            {
                continue;
            }

            var text = RemoveBullet(trimmed);
            if (text.StartsWith("Good", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Positive", StringComparison.OrdinalIgnoreCase))
            {
                positives.Add(text);
                continue;
            }

            if (text.StartsWith("Bad", StringComparison.OrdinalIgnoreCase)
                || text.StartsWith("Negative", StringComparison.OrdinalIgnoreCase))
            {
                negatives.Add(text);
            }
        }
    }

    private static string RemoveBullet(string value)
    {
        return value.StartsWith("* ", StringComparison.Ordinal) ? value[2..].Trim() : value.Trim();
    }

    private static string AppendSentence(string existing, string addition)
    {
        if (string.IsNullOrWhiteSpace(addition))
        {
            return existing;
        }

        return string.IsNullOrWhiteSpace(existing)
            ? addition
            : $"{existing} {addition}";
    }
}
