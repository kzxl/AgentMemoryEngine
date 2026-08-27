using AgentMemoryEngine.Core.Lexical;
using Xunit;

namespace AgentMemoryEngine.Tests;

public class LexicalBm25Tests
{
    [Fact]
    public void BM25_InvertedIndex_RanksExactKeywordsHighest()
    {
        var lexical = new LexicalIndex();

        lexical.IndexDocument(1, "Fixing GridControl freeze using async Task in RunAfterShown");
        lexical.IndexDocument(2, "Resolving React state desynchronization on WebSocket listener");
        lexical.IndexDocument(3, "Optimizing SQL query deadlock with nonclustered index on TicketId");

        Assert.Equal(3, lexical.DocumentCount);
        Assert.True(lexical.VocabularySize > 10);

        var scores = lexical.SearchBm25("GridControl RunAfterShown");

        Assert.NotEmpty(scores);
        Assert.True(scores.ContainsKey(1));
        Assert.Equal(1.0f, scores[1]); // Top ranked normalized to 1.0

        if (scores.TryGetValue(2, out float score2))
        {
            Assert.True(scores[1] > score2);
        }
    }
}
