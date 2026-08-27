using System.Text;
using AgentMemoryEngine.Core.Scoring;
using AgentMemoryEngine.Core.Storage;

namespace AgentMemoryEngine.Core.Budgeting;

/// <summary>
/// Result of an LLM Context Budgeting optimization.
/// </summary>
public record AmePromptBudgetResult
{
    public string FormattedPromptBlock { get; init; } = string.Empty;
    public int EstimatedTokensUsed { get; init; }
    public int MaxTokenBudget { get; init; }
    public int SelectedCount { get; init; }
    public IReadOnlyList<AmeSearchResult> SelectedMemories { get; init; } = [];
}

/// <summary>
/// Smart Context Window Token Budgeter for AI Agents.
/// Greedily packs the highest-scoring, non-redundant memories into a fixed token budget for LLM Prompt injection.
/// </summary>
public static class ContextBudgeter
{
    /// <summary>
    /// Packs the highest-scoring candidate memories into an LLM-ready prompt markdown block within the given token budget.
    /// </summary>
    public static AmePromptBudgetResult BuildPromptContext(
        IReadOnlyList<AmeSearchResult> candidates,
        int maxTokenBudget = 1500,
        int charsPerToken = 4)
    {
        if (candidates == null || candidates.Count == 0)
        {
            return new AmePromptBudgetResult
            {
                FormattedPromptBlock = string.Empty,
                EstimatedTokensUsed = 0,
                MaxTokenBudget = maxTokenBudget,
                SelectedCount = 0,
                SelectedMemories = []
            };
        }

        var sorted = candidates.OrderByDescending(c => c.CompositeScore).ToList();
        var selected = new List<AmeSearchResult>();
        int currentChars = 0;
        int maxChars = maxTokenBudget * charsPerToken;

        var sb = new StringBuilder();
        sb.AppendLine("<retrieved_memory_context>");

        foreach (var c in sorted)
        {
            string itemHeader = $"  [{c.Tier.ToString().ToUpperInvariant()} #{c.MemoryId} | Score: {c.CompositeScore:F2}]";
            string itemText = $"{itemHeader} {c.Payload.Trim()}";
            int itemCharLength = itemText.Length + 4; // Including newlines and spaces

            if (currentChars + itemCharLength <= maxChars)
            {
                // Simple semantic deduplication: check token overlap with already selected items
                if (!IsRedundant(c.Payload, selected))
                {
                    selected.Add(c);
                    sb.AppendLine($"  - {itemText}");
                    currentChars += itemCharLength;
                }
            }
        }

        sb.AppendLine("</retrieved_memory_context>");

        int estimatedTokens = (int)Math.Ceiling((double)currentChars / charsPerToken);

        return new AmePromptBudgetResult
        {
            FormattedPromptBlock = sb.ToString(),
            EstimatedTokensUsed = estimatedTokens,
            MaxTokenBudget = maxTokenBudget,
            SelectedCount = selected.Count,
            SelectedMemories = selected
        };
    }

    private static bool IsRedundant(string payload, List<AmeSearchResult> alreadySelected)
    {
        var words = new HashSet<string>(payload.Split([' ', '|', ',', '.', ':', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
        if (words.Count == 0) return false;

        foreach (var sel in alreadySelected)
        {
            var selWords = new HashSet<string>(sel.Payload.Split([' ', '|', ',', '.', ':', ';', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
            int intersection = words.Count(w => selWords.Contains(w));
            double overlap = (double)intersection / Math.Min(words.Count, selWords.Count);

            if (overlap > 0.85)
            {
                return true; // Too redundant with an already selected higher-score memory
            }
        }

        return false;
    }
}
