using System.Text.Json;
using System.Text.RegularExpressions;
using AdrPortal.Core.Ai;
using AdrPortal.Core.Entities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace AdrPortal.Infrastructure.Ai;

/// <summary>
/// Uses configured external IChatClient infrastructure with deterministic fallback support.
/// </summary>
public sealed class ExternalAiService(
    string? token,
    string? externalUnavailableReason,
    IOptions<AiProviderOptions> optionsAccessor,
    DeterministicAiService deterministicAiService,
    ILogger<ExternalAiService> logger)
    : IAiService
{
    private readonly AiProviderOptions options = optionsAccessor.Value;
    private readonly IChatClient? chatClient = string.IsNullOrWhiteSpace(token)
        ? null
        : CreateChatClient(token, optionsAccessor.Value);
    private static readonly Regex JsonBlockRegex = new(
        @"\{[\s\S]*\}",
        RegexOptions.Compiled);

    /// <summary>
    /// Evaluates a draft ADR with external chat completion and optional deterministic fallback.
    /// </summary>
    /// <param name="draftAdr">Draft ADR under analysis.</param>
    /// <param name="existingAdrs">Existing ADR corpus.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Structured recommendation output.</returns>
    public async Task<AdrEvaluationRecommendation> EvaluateAndRecommendAsync(
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<Adr> existingAdrs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draftAdr);
        ArgumentNullException.ThrowIfNull(existingAdrs);
        ct.ThrowIfCancellationRequested();

        if (chatClient is null)
        {
            var fallback = await deterministicAiService.EvaluateAndRecommendAsync(draftAdr, existingAdrs, ct);
            return fallback with
            {
                IsFallback = true,
                FallbackReason = externalUnavailableReason
            };
        }

        var messages = BuildEvaluateMessages(draftAdr, existingAdrs);
        try
        {
            var response = await chatClient.GetResponseAsync(messages, null, ct);
            var payload = response.Text;
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new InvalidOperationException("AI response payload was empty.");
            }

            var parsed = ParseEvaluateResult(payload);
            return parsed with
            {
                IsFallback = false,
                FallbackReason = null
            };
        }
        catch (Exception exception) when (CanFallback(exception))
        {
            logger.LogWarning(
                exception,
                "External AI EvaluateAndRecommendAsync failed; using deterministic fallback.");
            var fallback = await deterministicAiService.EvaluateAndRecommendAsync(draftAdr, existingAdrs, ct);
            return fallback with
            {
                IsFallback = true,
                FallbackReason = "External AI call failed; deterministic fallback used."
            };
        }
    }

    /// <summary>
    /// Finds affected ADRs with external chat completion and optional deterministic fallback.
    /// </summary>
    /// <param name="draftAdr">Draft ADR under analysis.</param>
    /// <param name="existingAdrs">Existing ADR corpus.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Structured affected ADR list.</returns>
    public async Task<AffectedAdrAnalysisResult> FindAffectedAdrsAsync(
        AdrDraftForAnalysis draftAdr,
        IReadOnlyList<Adr> existingAdrs,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(draftAdr);
        ArgumentNullException.ThrowIfNull(existingAdrs);
        ct.ThrowIfCancellationRequested();

        if (chatClient is null)
        {
            var fallback = await deterministicAiService.FindAffectedAdrsAsync(draftAdr, existingAdrs, ct);
            return fallback with
            {
                IsFallback = true,
                FallbackReason = externalUnavailableReason
            };
        }

        var messages = BuildAffectedMessages(draftAdr, existingAdrs);
        try
        {
            var response = await chatClient.GetResponseAsync(messages, null, ct);
            var payload = response.Text;
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new InvalidOperationException("AI response payload was empty.");
            }

            var parsed = ParseAffectedResult(payload);
            return parsed with
            {
                IsFallback = false,
                FallbackReason = null
            };
        }
        catch (Exception exception) when (CanFallback(exception))
        {
            logger.LogWarning(
                exception,
                "External AI FindAffectedAdrsAsync failed; using deterministic fallback.");
            var fallback = await deterministicAiService.FindAffectedAdrsAsync(draftAdr, existingAdrs, ct);
            return fallback with
            {
                IsFallback = true,
                FallbackReason = "External AI call failed; deterministic fallback used."
            };
        }
    }

    /// <summary>
    /// Delegates bootstrap workflows to deterministic implementation.
    /// </summary>
    /// <param name="repoRootPath">Repository root path.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Deterministic bootstrap proposal set.</returns>
    public Task<AdrBootstrapProposalSet> BootstrapAdrsFromCodebaseAsync(string repoRootPath, CancellationToken ct)
    {
        return deterministicAiService.BootstrapAdrsFromCodebaseAsync(repoRootPath, ct);
    }

    private bool CanFallback(Exception exception)
    {
        if (!options.AllowDeterministicFallback)
        {
            return false;
        }

        return exception is InvalidOperationException
            or JsonException
            or NotSupportedException
            or System.ClientModel.ClientResultException
            or HttpRequestException
            or TaskCanceledException;
    }

    private static List<ChatMessage> BuildEvaluateMessages(AdrDraftForAnalysis draftAdr, IReadOnlyList<Adr> existingAdrs)
    {
        return
        [
            new ChatMessage(
                ChatRole.System,
                """
                You are an ADR analysis assistant. Return strict JSON only.
                Respond with object fields:
                preferredOption (string),
                recommendationSummary (string),
                decisionFit (string),
                options (array of { optionName, summary, score, rationale, tradeOffs[] }),
                risks (string[]),
                suggestedAlternatives (string[]),
                groundingAdrNumbers (int[]).
                No markdown or prose outside JSON.
                """),
            new ChatMessage(
                ChatRole.User,
                BuildEvaluatePrompt(draftAdr, existingAdrs))
        ];
    }

    private static List<ChatMessage> BuildAffectedMessages(AdrDraftForAnalysis draftAdr, IReadOnlyList<Adr> existingAdrs)
    {
        return
        [
            new ChatMessage(
                ChatRole.System,
                """
                You are an ADR impact analysis assistant. Return strict JSON only.
                Respond with object fields:
                summary (string),
                items (array of { adrNumber, title, impactLevel, rationale, signals[] }).
                impactLevel must be one of: Low, Medium, High.
                No markdown or prose outside JSON.
                """),
            new ChatMessage(
                ChatRole.User,
                BuildAffectedPrompt(draftAdr, existingAdrs))
        ];
    }

    private static string BuildEvaluatePrompt(AdrDraftForAnalysis draftAdr, IReadOnlyList<Adr> existingAdrs)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Draft ADR:");
        builder.AppendLine($"- Title: {draftAdr.Title}");
        builder.AppendLine($"- Slug: {draftAdr.Slug}");
        builder.AppendLine($"- ProblemStatement: {draftAdr.ProblemStatement}");
        builder.AppendLine($"- DecisionDrivers: {string.Join(" | ", draftAdr.DecisionDrivers)}");
        builder.AppendLine($"- ConsideredOptions: {string.Join(" | ", draftAdr.ConsideredOptions)}");
        builder.AppendLine($"- BodyMarkdown: {draftAdr.BodyMarkdown}");
        builder.AppendLine();
        builder.AppendLine("Existing ADRs:");
        foreach (var adr in existingAdrs.OrderBy(adr => adr.Number))
        {
            builder.AppendLine(
                $"- ADR-{adr.Number:0000} | {adr.Status} | {adr.Title} | {adr.Slug}");
        }

        return builder.ToString();
    }

    private static string BuildAffectedPrompt(AdrDraftForAnalysis draftAdr, IReadOnlyList<Adr> existingAdrs)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Draft ADR:");
        builder.AppendLine($"- Title: {draftAdr.Title}");
        builder.AppendLine($"- Slug: {draftAdr.Slug}");
        builder.AppendLine($"- ProblemStatement: {draftAdr.ProblemStatement}");
        builder.AppendLine($"- DecisionDrivers: {string.Join(" | ", draftAdr.DecisionDrivers)}");
        builder.AppendLine($"- ConsideredOptions: {string.Join(" | ", draftAdr.ConsideredOptions)}");
        builder.AppendLine($"- BodyMarkdown: {draftAdr.BodyMarkdown}");
        builder.AppendLine();
        builder.AppendLine("Existing ADRs:");
        foreach (var adr in existingAdrs.OrderBy(adr => adr.Number))
        {
            builder.AppendLine(
                $"- ADR-{adr.Number:0000} | {adr.Status} | {adr.Title} | {adr.Slug}");
        }

        return builder.ToString();
    }

    private static AdrEvaluationRecommendation ParseEvaluateResult(string payload)
    {
        var json = ExtractJson(payload);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var preferredOption = RequiredString(root, "preferredOption");
        var recommendationSummary = RequiredString(root, "recommendationSummary");
        var decisionFit = RequiredString(root, "decisionFit");
        var options = ParseOptions(root);
        var risks = ParseStringArray(root, "risks");
        var suggestedAlternatives = ParseStringArray(root, "suggestedAlternatives");
        var grounding = ParseIntArray(root, "groundingAdrNumbers");

        if (options.Count is 0)
        {
            throw new InvalidOperationException("AI response options array must contain at least one option.");
        }

        return new AdrEvaluationRecommendation
        {
            PreferredOption = preferredOption,
            RecommendationSummary = recommendationSummary,
            DecisionFit = decisionFit,
            Options = options,
            Risks = risks,
            SuggestedAlternatives = suggestedAlternatives,
            GroundingAdrNumbers = grounding,
            IsFallback = false
        };
    }

    private static AffectedAdrAnalysisResult ParseAffectedResult(string payload)
    {
        var json = ExtractJson(payload);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var summary = RequiredString(root, "summary");
        var items = ParseAffectedItems(root);
        return new AffectedAdrAnalysisResult
        {
            Summary = summary,
            Items = items,
            IsFallback = false
        };
    }

    private static string ExtractJson(string payload)
    {
        var trimmed = payload.Trim();
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
        {
            return trimmed;
        }

        var match = JsonBlockRegex.Match(trimmed);
        if (!match.Success)
        {
            throw new InvalidOperationException("AI response did not contain a valid JSON object.");
        }

        return match.Value;
    }

    private static string RequiredString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind is not JsonValueKind.String)
        {
            throw new InvalidOperationException($"AI response property '{propertyName}' is required and must be a string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"AI response property '{propertyName}' cannot be empty.");
        }

        return value.Trim();
    }

    private static IReadOnlyList<string> ParseStringArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return [];
        }

        if (property.ValueKind is not JsonValueKind.Array)
        {
            throw new InvalidOperationException($"AI response property '{propertyName}' must be an array.");
        }

        return property.EnumerateArray()
            .Where(item => item.ValueKind is JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToArray();
    }

    private static IReadOnlyList<int> ParseIntArray(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return [];
        }

        if (property.ValueKind is not JsonValueKind.Array)
        {
            throw new InvalidOperationException($"AI response property '{propertyName}' must be an array.");
        }

        var values = new List<int>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind is JsonValueKind.Number && item.TryGetInt32(out var parsed))
            {
                values.Add(parsed);
            }
        }

        return values;
    }

    private static IReadOnlyList<AdrOptionRecommendation> ParseOptions(JsonElement root)
    {
        if (!root.TryGetProperty("options", out var property) || property.ValueKind is not JsonValueKind.Array)
        {
            throw new InvalidOperationException("AI response property 'options' must be an array.");
        }

        var options = new List<AdrOptionRecommendation>();
        foreach (var option in property.EnumerateArray())
        {
            var optionName = RequiredString(option, "optionName");
            var summary = RequiredString(option, "summary");
            var score = ParseScore(option, "score");
            var rationale = RequiredString(option, "rationale");
            var tradeOffs = ParseStringArray(option, "tradeOffs");

            options.Add(
                new AdrOptionRecommendation
                {
                    OptionName = optionName,
                    Summary = summary,
                    Score = score,
                    Rationale = rationale,
                    TradeOffs = tradeOffs
                });
        }

        return options;
    }

    private static double ParseScore(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind is not JsonValueKind.Number)
        {
            throw new InvalidOperationException($"AI response property '{propertyName}' must be numeric.");
        }

        var value = property.GetDouble();
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new InvalidOperationException($"AI response property '{propertyName}' must be finite.");
        }

        return Math.Clamp(value, 0.0, 1.0);
    }

    private static IReadOnlyList<AffectedAdrResultItem> ParseAffectedItems(JsonElement root)
    {
        if (!root.TryGetProperty("items", out var property) || property.ValueKind is not JsonValueKind.Array)
        {
            return [];
        }

        var items = new List<AffectedAdrResultItem>();
        foreach (var item in property.EnumerateArray())
        {
            var number = item.TryGetProperty("adrNumber", out var numberProperty) && numberProperty.TryGetInt32(out var parsedNumber)
                ? parsedNumber
                : throw new InvalidOperationException("AI response affected item requires integer 'adrNumber'.");
            var title = RequiredString(item, "title");
            var rationale = RequiredString(item, "rationale");
            var impact = ParseImpact(item);
            var signals = ParseStringArray(item, "signals");
            items.Add(
                new AffectedAdrResultItem
                {
                    AdrNumber = number,
                    Title = title,
                    ImpactLevel = impact,
                    Rationale = rationale,
                    Signals = signals
                });
        }

        return items;
    }

    private static AdrImpactLevel ParseImpact(JsonElement item)
    {
        var raw = RequiredString(item, "impactLevel");
        return raw.ToLowerInvariant() switch
        {
            "high" => AdrImpactLevel.High,
            "medium" => AdrImpactLevel.Medium,
            "low" => AdrImpactLevel.Low,
            _ => throw new InvalidOperationException($"Unsupported impact level '{raw}'.")
        };
    }

    private static IChatClient CreateChatClient(string token, AiProviderOptions options)
    {
        var model = string.IsNullOrWhiteSpace(options.Model)
            ? "gpt-4o-mini"
            : options.Model.Trim();
        ChatClient openAiClient;
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            openAiClient = new ChatClient(model, token);
        }
        else
        {
            var clientOptions = new OpenAIClientOptions
            {
                Endpoint = new Uri(options.Endpoint, UriKind.Absolute)
            };
            openAiClient = new ChatClient(model, new System.ClientModel.ApiKeyCredential(token), clientOptions);
        }

        return openAiClient.AsIChatClient();
    }
}
