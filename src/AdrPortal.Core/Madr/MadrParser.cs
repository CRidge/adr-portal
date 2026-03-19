using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AdrPortal.Core.Entities;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace AdrPortal.Core.Madr;

/// <summary>
/// Parses MADR 4.0 markdown documents into ADR domain models.
/// </summary>
public sealed class MadrParser : IMadrParser
{
    private static readonly Regex FileNameRegex = new(
        @"^adr-(?<number>\d{4})-(?<slug>[a-z0-9][a-z0-9-]*)\.md$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex FirstLevelTitleRegex = new(
        @"(?m)^\s*#\s+(?<title>.+?)\s*$",
        RegexOptions.Compiled);

    private readonly IDeserializer yamlDeserializer = new DeserializerBuilder().Build();

    /// <inheritdoc />
    public Adr Parse(string repoRelativePath, string markdown)
    {
        if (string.IsNullOrWhiteSpace(repoRelativePath))
        {
            throw new ArgumentException("ADR path is required.", nameof(repoRelativePath));
        }

        ArgumentNullException.ThrowIfNull(markdown);

        var normalizedRelativePath = NormalizeRelativePath(repoRelativePath);
        var (number, slug) = ParseFileName(normalizedRelativePath);
        var (frontMatter, body) = MadrDocument.SplitFrontMatter(markdown);
        var metadata = DeserializeFrontMatter(frontMatter);

        var adr = new Adr
        {
            Number = number,
            Slug = slug,
            RepoRelativePath = normalizedRelativePath,
            Title = ParseTitle(body) ?? BuildTitleFromSlug(slug),
            Status = ParseStatus(ReadString(metadata, "status")),
            Date = ParseDate(ReadString(metadata, "date")),
            GlobalId = ParseGuid(ReadString(metadata, "global-id")),
            GlobalVersion = ParseInt32(ReadString(metadata, "global-version")),
            DecisionMakers = ParseStringList(metadata, "decision-makers"),
            Consulted = ParseStringList(metadata, "consulted"),
            Informed = ParseStringList(metadata, "informed"),
            SupersededByNumber = ParseInt32(ReadString(metadata, "superseded-by")),
            RawMarkdown = markdown
        };

        return adr;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        var pathSegments = relativePath
            .Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return string.Join('/', pathSegments);
    }

    private static (int Number, string Slug) ParseFileName(string repoRelativePath)
    {
        var fileName = Path.GetFileName(repoRelativePath);
        var match = FileNameRegex.Match(fileName);
        if (!match.Success)
        {
            throw new FormatException($"ADR filename '{fileName}' does not follow 'adr-0001-slug.md'.");
        }

        var number = int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
        var slug = match.Groups["slug"].Value.ToLowerInvariant();

        return (number, slug);
    }

    private IReadOnlyDictionary<string, object?> DeserializeFrontMatter(string? frontMatter)
    {
        if (string.IsNullOrWhiteSpace(frontMatter))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var deserialized = yamlDeserializer.Deserialize<Dictionary<string, object?>>(frontMatter)
                ?? [];

            return new Dictionary<string, object?>(deserialized, StringComparer.OrdinalIgnoreCase);
        }
        catch (YamlException exception)
        {
            throw new FormatException("Failed to parse MADR YAML front matter.", exception);
        }
    }

    private static string? ReadString(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var value) || value is null)
        {
            return null;
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
    }

    private static AdrStatus ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return AdrStatus.Proposed;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "proposed" => AdrStatus.Proposed,
            "accepted" => AdrStatus.Accepted,
            "rejected" => AdrStatus.Rejected,
            "superseded" => AdrStatus.Superseded,
            "deprecated" => AdrStatus.Deprecated,
            _ => throw new FormatException($"Unsupported ADR status '{status}'.")
        };
    }

    private static DateOnly ParseDate(string? dateValue)
    {
        if (string.IsNullOrWhiteSpace(dateValue))
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }

        if (DateOnly.TryParseExact(
            dateValue,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsedDateOnly))
        {
            return parsedDateOnly;
        }

        if (DateTime.TryParse(
            dateValue,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var parsedDateTime))
        {
            return DateOnly.FromDateTime(parsedDateTime);
        }

        throw new FormatException($"Invalid ADR date '{dateValue}'.");
    }

    private static Guid? ParseGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Guid.TryParse(value, out var parsedGuid))
        {
            return parsedGuid;
        }

        throw new FormatException($"Invalid GUID value '{value}'.");
    }

    private static int? ParseInt32(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInteger))
        {
            return parsedInteger;
        }

        throw new FormatException($"Invalid integer value '{value}'.");
    }

    private static IReadOnlyList<string> ParseStringList(IReadOnlyDictionary<string, object?> values, string key)
    {
        if (!values.TryGetValue(key, out var rawValue) || rawValue is null)
        {
            return [];
        }

        if (rawValue is string singleValue)
        {
            return string.IsNullOrWhiteSpace(singleValue)
                ? []
                : [singleValue.Trim()];
        }

        if (rawValue is IEnumerable<object?> manyValues)
        {
            var parsedValues = manyValues
                .Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .ToArray();

            return parsedValues;
        }

        var convertedValue = Convert.ToString(rawValue, CultureInfo.InvariantCulture)?.Trim();
        return string.IsNullOrWhiteSpace(convertedValue)
            ? []
            : [convertedValue];
    }

    private static string? ParseTitle(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        var headingMatch = FirstLevelTitleRegex.Match(body);
        if (headingMatch.Success)
        {
            return headingMatch.Groups["title"].Value.Trim();
        }

        var document = Markdown.Parse(body);
        var titleHeading = document
            .Descendants<HeadingBlock>()
            .FirstOrDefault(headingBlock => headingBlock.Level == 1);

        if (titleHeading?.Inline is not ContainerInline inline)
        {
            return null;
        }

        var title = ReadInlineText(inline).Trim();
        if (!string.IsNullOrWhiteSpace(title))
        {
            return title;
        }

        return null;
    }

    private static string ReadInlineText(ContainerInline inline)
    {
        var content = new StringBuilder();
        var current = inline.FirstChild;
        while (current is not null)
        {
            switch (current)
            {
                case LiteralInline literalInline:
                    content.Append(literalInline.Content.Text.Substring(literalInline.Content.Start, literalInline.Content.Length));
                    break;
                case ContainerInline nestedInline:
                    content.Append(ReadInlineText(nestedInline));
                    break;
            }

            current = current.NextSibling;
        }

        return content.ToString();
    }

    private static string BuildTitleFromSlug(string slug)
    {
        var spaced = slug.Replace('-', ' ');
        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
    }
}
