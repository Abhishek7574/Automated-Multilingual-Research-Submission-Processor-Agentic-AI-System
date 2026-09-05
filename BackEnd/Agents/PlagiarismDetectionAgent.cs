using Backend.Models;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Backend.Agents;

/// <summary>
/// Plagiarism Detection Agent – stub implementation.
/// TODO: Replace with Turnitin API, Copyleaks, or SK-powered embedding cosine similarity.
/// Decoupled from business-rule Validation Agent.
/// </summary>
public class PlagiarismDetectionAgent : IPlagiarismDetectionAgent
{
    // Example reference sources (simulate academic database)
    private readonly Dictionary<string, string> _sources = new()
        {
            { "Paper A", "Machine learning techniques are widely used in artificial intelligence systems." },
            { "Paper B", "Cloud computing enables scalable and distributed computing resources." },
            { "Paper C", "Natural language processing helps computers understand human language." }
        };

    public Task<StepResult<PlagiarismResult>> DetectAsync(string text, CancellationToken ct = default)
    {
        var matches = new List<PlagiarismMatch>();
        double highestSimilarity = 0;

        foreach (var source in _sources)
        {
            var similarity = CheckSimilarity(text, source.Value);

            if (similarity > 0.2) // threshold to record match
            {
                matches.Add(new PlagiarismMatch(
                    Source: source.Key,
                    Similarity: similarity * 100,
                    MatchedText: source.Value
                ));
            }

            if (similarity > highestSimilarity)
                highestSimilarity = similarity;
        }

        var result = new PlagiarismResult(
            SimilarityPercent: highestSimilarity * 100,
            PlagiarismDetected: highestSimilarity > 0.7,
            Matches: matches
        );

        // FIX: Use the constructor instead of the property as a method
        return Task.FromResult(new StepResult<PlagiarismResult>(
            true,   // IsSuccess
            result, // Value
            null,   // Error message
            0       // Elapsed milliseconds
        ));
    }

    private double CheckSimilarity(string text1, string text2)
    {
        var words1 = Tokenize(text1);
        var words2 = Tokenize(text2);

        var allWords = words1.Union(words2).Distinct().ToList();

        var vector1 = CreateVector(words1, allWords);
        var vector2 = CreateVector(words2, allWords);

        return CosineSimilarity(vector1, vector2);
    }

    private List<string> Tokenize(string text)
    {
        return Regex.Split(text.ToLower(), @"\W+")
            .Where(w => w.Length > 2)
            .ToList();
    }

    private List<double> CreateVector(List<string> words, List<string> allWords)
    {
        var vector = new List<double>();

        foreach (var word in allWords)
        {
            vector.Add(words.Count(w => w == word));
        }

        return vector;
    }

    private double CosineSimilarity(List<double> v1, List<double> v2)
    {
        double dot = 0;
        double mag1 = 0;
        double mag2 = 0;

        for (int i = 0; i < v1.Count; i++)
        {
            dot += v1[i] * v2[i];
            mag1 += Math.Pow(v1[i], 2);
            mag2 += Math.Pow(v2[i], 2);
        }

        if (mag1 == 0 || mag2 == 0)
            return 0;

        return dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
}
