using AgentMemoryEngine.Core;
using AgentMemoryEngine.Core.Budgeting;
using AgentMemoryEngine.Core.Scoring;
using AgentMemoryEngine.Core.Storage;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class ContextBudgeterTests
{
    [Fact]
    public void ContextBudgeter_PacksMemoriesWithinTokenBudget()
    {
        var candidates = new List<AmeSearchResult>
        {
            new() { MemoryId = 1, Tier = AmeMemoryTier.Semantic, CompositeScore = 0.95f, Payload = "Architecture Standard: Strict Folder-per-Feature segregation" },
            new() { MemoryId = 2, Tier = AmeMemoryTier.Episodic, CompositeScore = 0.88f, Payload = "Fix GridControl freeze by invoking Task in RunAfterShown" },
            new() { MemoryId = 3, Tier = AmeMemoryTier.Episodic, CompositeScore = 0.85f, Payload = "Fix GridControl freeze by using Task in RunAfterShown (Redundant)" },
            new() { MemoryId = 4, Tier = AmeMemoryTier.Procedural, CompositeScore = 0.70f, Payload = "Deploy pipeline: 1. Clean build 2. Run unit tests 3. Package single file bundle" }
        };

        // Budget of 60 tokens (approx 240 chars)
        var result = ContextBudgeter.BuildPromptContext(candidates, maxTokenBudget: 60, charsPerToken: 4);

        Assert.NotNull(result);
        Assert.True(result.EstimatedTokensUsed <= 60);
        Assert.Contains("<retrieved_memory_context>", result.FormattedPromptBlock);
        Assert.Contains("Architecture Standard", result.FormattedPromptBlock);
        Assert.Contains("Fix GridControl freeze", result.FormattedPromptBlock);
        
        // Assert redundant #3 was filtered out in favor of higher score #2
        Assert.DoesNotContain("(Redundant)", result.FormattedPromptBlock);
    }
}
