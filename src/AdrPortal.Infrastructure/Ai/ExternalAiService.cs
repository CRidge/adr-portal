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
    private static readonly Regex SlugSanitizerRegex = new(
        @"[^a-z0-9]+",
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
    /// Generates ADR draft guidance from a repository question using external AI and deterministic fallback.
    /// </summary>
    /// <param name="question">User-authored ADR question.</param>
    /// <param name="existingAdrs">Existing ADR corpus used for grounding and constraints.</param>
    /// <param name="ct">Cancellation token for the operation.</param>
    /// <returns>Generated ADR draft guidance with recommendation options.</returns>
    public async Task<AdrQuestionGenerationResult> GenerateDraftFromQuestionAsync(
        string question,
        IReadOnlyList<Adr> existingAdrs,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(existingAdrs);
        ct.ThrowIfCancellationRequested();

        var trimmedQuestion = question.Trim();
        var activeConstraints = existingAdrs
            .Where(adr => adr.Status is AdrStatus.Proposed or AdrStatus.Accepted)
            .OrderBy(adr => adr.Number)
            .ToArray();
        var constraintSet = activeConstraints.Length > 0 ? activeConstraints : existingAdrs;

        if (chatClient is null)
        {
            var fallback = await deterministicAiService.GenerateDraftFromQuestionAsync(trimmedQuestion, constraintSet, ct);
            return fallback with
            {
                Recommendation = fallback.Recommendation with
                {
                    IsFallback = true,
                    FallbackReason = externalUnavailableReason
                }
            };
        }

        var messages = BuildQuestionMessages(trimmedQuestion, constraintSet);
        try
        {
            var response = await chatClient.GetResponseAsync(messages, null, ct);
            var payload = response.Text;
            if (string.IsNullOrWhiteSpace(payload))
            {
                throw new InvalidOperationException("AI response payload was empty.");
            }

            var parsed = ParseQuestionResult(payload, trimmedQuestion);
            return parsed with
            {
                Recommendation = parsed.Recommendation with
                {
                    IsFallback = false,
                    FallbackReason = null
                }
            };
        }
        catch (Exception exception) when (CanFallback(exception))
        {
            logger.LogWarning(
                exception,
                "External AI GenerateDraftFromQuestionAsync failed; using deterministic fallback.");
            var fallback = await deterministicAiService.GenerateDraftFromQuestionAsync(trimmedQuestion, constraintSet, ct);
            return fallback with
            {
                Recommendation = fallback.Recommendation with
                {
                    IsFallback = true,
                    FallbackReason = "External AI call failed; deterministic fallback used."
                }
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
                options (array of { optionName, summary, score, rationale, pros[], cons[], tradeOffs[] }),
                risks (string[]),
                suggestedAlternatives (string[]),
                groundingAdrNumbers (int[]).
                Provide at least three distinct options whenever possible.
                Each option must include at least one pro and one con.
                recommendationSummary must clearly explain why preferredOption is chosen.
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

    private static List<ChatMessage> BuildQuestionMessages(string question, IReadOnlyList<Adr> existingAdrs)
    {
        return
        [
            new ChatMessage(
                ChatRole.System,
                """
                You are an ADR authoring assistant for repository workflows. Return strict JSON only.
                Respond with object fields:
                question (string),
                suggestedTitle (string),
                suggestedSlug (string, lowercase letters/numbers/hyphens),
                problemStatement (string),
                decisionDrivers (string[]),
                recommendation (object with:
                  preferredOption (string),
                  recommendationSummary (string),
                  decisionFit (string),
                  options (array of { optionName, summary, score, rationale, pros[], cons[], tradeOffs[] }),
                  risks (string[]),
                  suggestedAlternatives (string[]),
                  groundingAdrNumbers (int[])
                ).
                Provide at least three distinct recommendation options whenever possible.
                Each option must include at least one pro and one con.
                decisionFit must explicitly reference repository ADR constraints.
                No markdown or prose outside JSON.
                """),
            new ChatMessage(
                ChatRole.User,
                BuildQuestionPrompt(question, existingAdrs))
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

    private static string BuildQuestionPrompt(string question, IReadOnlyList<Adr> existingAdrs)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("Repository ADR question:");
        builder.AppendLine($"- Question: {question}");
        builder.AppendLine();
        builder.AppendLine("Existing ADR constraints (prefer active ADRs when grounding):");
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

        if (options.Count < 3)
        {
            throw new InvalidOperationException("AI response options array must contain at least three options.");
        }

        if (!options.Any(
                option => string.Equals(option.OptionName, preferredOption, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("AI response preferred option must match one of the option names.");
        }

        if (string.IsNullOrWhiteSpace(recommendationSummary) || recommendationSummary.Length < 20)
        {
            throw new InvalidOperationException("AI response recommendation summary must provide clear rationale.");
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

    private static AdrQuestionGenerationResult ParseQuestionResult(string payload, string fallbackQuestion)
    {
        var json = ExtractJson(payload);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var question = root.TryGetProperty("question", out var questionProperty) && questionProperty.ValueKind is JsonValueKind.String
            ? (questionProperty.GetString() ?? string.Empty).Trim()
            : fallbackQuestion;
        if (string.IsNullOrWhiteSpace(question))
        {
            question = fallbackQuestion;
        }

        var suggestedTitle = RequiredString(root, "suggestedTitle");
        var suggestedSlug = NormalizeSlug(RequiredString(root, "suggestedSlug"));
        var problemStatement = RequiredString(root, "problemStatement");
        var decisionDrivers = ParseStringArray(root, "decisionDrivers");
        var recommendation = ParseQuestionRecommendation(root);
        return new AdrQuestionGenerationResult
        {
            Question = question,
            SuggestedTitle = suggestedTitle,
            SuggestedSlug = suggestedSlug,
            ProblemStatement = problemStatement,
            DecisionDrivers = decisionDrivers,
            Recommendation = recommendation
        };
    }

    private static AdrEvaluationRecommendation ParseQuestionRecommendation(JsonElement root)
    {
        if (!root.TryGetProperty("recommendation", out var recommendation)
            || recommendation.ValueKind is not JsonValueKind.Object)
        {
            throw new InvalidOperationException("AI response property 'recommendation' is required and must be an object.");
        }

        var preferredOption = RequiredString(recommendation, "preferredOption");
        var recommendationSummary = RequiredString(recommendation, "recommendationSummary");
        var decisionFit = RequiredString(recommendation, "decisionFit");
        var options = ParseOptions(recommendation);
        var risks = ParseStringArray(recommendation, "risks");
        var suggestedAlternatives = ParseStringArray(recommendation, "suggestedAlternatives");
        var grounding = ParseIntArray(recommendation, "groundingAdrNumbers");
        if (options.Count < 3)
        {
            throw new InvalidOperationException("AI response recommendation options array must contain at least three options.");
        }

        if (!options.Any(
                option => string.Equals(option.OptionName, preferredOption, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("AI response preferred option must match one of the option names.");
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
            IsFallback = false,
            FallbackReason = null
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
            var pros = ParseNonEmptyStringArray(option, "pros");
            var cons = ParseNonEmptyStringArray(option, "cons");
            var tradeOffs = ParseStringArray(option, "tradeOffs");

            options.Add(
                new AdrOptionRecommendation
                {
                    OptionName = optionName,
                    Summary = summary,
                    Score = score,
                    Rationale = rationale,
                    Pros = pros,
                    Cons = cons,
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

    private static IReadOnlyList<string> ParseNonEmptyStringArray(JsonElement root, string propertyName)
    {
        var values = ParseStringArray(root, propertyName);
        if (values.Count is 0)
        {
            throw new InvalidOperationException($"AI response property '{propertyName}' must contain at least one value.");
        }

        return values;
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

    private static string NormalizeSlug(string raw)
    {
        var lowered = raw.Trim().ToLowerInvariant();
        var collapsed = SlugSanitizerRegex.Replace(lowered, "-").Trim('-');
        return string.IsNullOrWhiteSpace(collapsed) ? "adr-question-draft" : collapsed;
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
