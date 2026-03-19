using System.Globalization;
using AdrPortal.Core.Entities;
using YamlDotNet.Serialization;

namespace AdrPortal.Core.Madr;

/// <summary>
/// Serializes ADR domain records to MADR 4.0 markdown documents.
/// </summary>
public sealed class MadrWriter : IMadrWriter
{
    private const string NewLine = "\n";
    private readonly ISerializer yamlSerializer = new SerializerBuilder().Build();

    /// <inheritdoc />
    public string Write(Adr adr)
    {
        ArgumentNullException.ThrowIfNull(adr);

        var frontMatter = BuildFrontMatter(adr);
        var body = BuildBody(adr).TrimStart('\r', '\n');

        return $"---{NewLine}{frontMatter}---{NewLine}{NewLine}{body}";
    }

    private string BuildFrontMatter(Adr adr)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["status"] = adr.Status.ToString().ToLowerInvariant(),
            ["date"] = adr.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["decision-makers"] = adr.DecisionMakers,
            ["consulted"] = adr.Consulted,
            ["informed"] = adr.Informed
        };

        if (adr.GlobalId is not null)
        {
            metadata["global-id"] = adr.GlobalId.Value.ToString();
        }

        if (adr.GlobalVersion is not null)
        {
            metadata["global-version"] = adr.GlobalVersion.Value;
        }

        if (adr.SupersededByNumber is not null)
        {
            metadata["superseded-by"] = adr.SupersededByNumber.Value;
        }

        var serialized = yamlSerializer.Serialize(metadata).TrimEnd();
        return $"{serialized}{NewLine}";
    }

    private static string BuildBody(Adr adr)
    {
        var bodyFromRawMarkdown = ExtractBody(adr.RawMarkdown);
        if (!string.IsNullOrWhiteSpace(bodyFromRawMarkdown))
        {
            return bodyFromRawMarkdown;
        }

        return $"# {adr.Title}{NewLine}{NewLine}## Context and Problem Statement{NewLine}{NewLine}";
    }

    private static string ExtractBody(string rawMarkdown)
    {
        if (string.IsNullOrWhiteSpace(rawMarkdown))
        {
            return string.Empty;
        }

        var (_, body) = MadrDocument.SplitFrontMatter(rawMarkdown);
        return body;
    }
}
