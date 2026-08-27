using System.Text.RegularExpressions;

namespace AgentMemoryEngine.Core.Lexical;

/// <summary>
/// Inverted Lexical Index with Okapi BM25 ranking for exact keyword & symbol retrieval.
/// Enables hybrid search combining dense semantic vectors with precise lexical keyword matching.
/// </summary>
public sealed class LexicalIndex
{
    private readonly Dictionary<string, List<(uint MemoryId, uint TermFreq)>> _postings = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, uint> _docLengths = [];
    private long _totalDocLength = 0;
    private double _avgDocLength = 0.0;
    private static readonly Regex TokenRegex = new(@"\b[A-Za-z0-9_]{2,}\b", RegexOptions.Compiled);

    public int VocabularySize => _postings.Count;
    public int DocumentCount => _docLengths.Count;

    /// <summary>
    /// Tokenizes text into normalized word/symbol tokens.
    /// </summary>
    public static List<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        foreach (Match m in TokenRegex.Matches(text))
        {
            tokens.Add(m.Value.ToLowerInvariant());
        }
        return tokens;
    }

    /// <summary>
    /// Indexes a memory payload into the inverted posting lists.
    /// </summary>
    public void IndexDocument(uint memoryId, string payload)
    {
        var tokens = Tokenize(payload);
        uint docLen = (uint)tokens.Count;
        _docLengths[memoryId] = docLen;
        _totalDocLength += docLen;
        _avgDocLength = _docLengths.Count > 0 ? (double)_totalDocLength / _docLengths.Count : 0.0;

        // Calculate term frequencies in document
        var termCounts = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in tokens)
        {
            termCounts[t] = termCounts.GetValueOrDefault(t, 0u) + 1u;
        }

        foreach (var (term, count) in termCounts)
        {
            if (!_postings.TryGetValue(term, out var list))
            {
                list = [];
                _postings[term] = list;
            }
            list.Add((memoryId, count));
        }
    }

    /// <summary>
    /// Executes an Okapi BM25 lexical search for the given query text.
    /// Returns a map of Memory ID to normalized BM25 score in [0.0, 1.0].
    /// </summary>
    public Dictionary<uint, float> SearchBm25(string queryText, float k1 = 1.2f, float b = 0.75f)
    {
        var scores = new Dictionary<uint, float>();
        var queryTokens = Tokenize(queryText).Distinct().ToList();

        if (queryTokens.Count == 0 || DocumentCount == 0)
            return scores;

        int N = DocumentCount;

        foreach (var term in queryTokens)
        {
            if (!_postings.TryGetValue(term, out var postingList))
                continue;

            int n_q = postingList.Count;
            // Standard Robertson-Spärck Jones IDF
            double idf = Math.Log((N - n_q + 0.5) / (n_q + 0.5) + 1.0);
            if (idf <= 0.0) idf = 0.01;

            foreach (var (memId, tf) in postingList)
            {
                uint docLen = _docLengths.GetValueOrDefault(memId, (uint)_avgDocLength);
                double lengthNorm = 1.0 - b + b * (docLen / Math.Max(1.0, _avgDocLength));
                double tfWeight = (tf * (k1 + 1.0)) / (tf + k1 * lengthNorm);

                double termScore = idf * tfWeight;
                scores[memId] = scores.GetValueOrDefault(memId, 0.0f) + (float)termScore;
            }
        }

        // Normalize scores to [0.0, 1.0]
        if (scores.Count > 0)
        {
            float maxScore = scores.Values.Max();
            if (maxScore > 0.0f)
            {
                foreach (var id in scores.Keys.ToList())
                {
                    scores[id] = Math.Clamp(scores[id] / maxScore, 0.0f, 1.0f);
                }
            }
        }

        return scores;
    }
}
