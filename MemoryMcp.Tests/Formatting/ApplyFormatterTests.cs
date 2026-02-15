using MemoryMcp.Formatting;
using MemoryMcp.Models;
using Xunit;

namespace MemoryMcp.Tests.Formatting;

public class ApplyFormatterTests
{
    [Fact]
    public void BuildCopilotInstructions_EmptyNotes_ReturnsPlaceholder()
    {
        // Arrange
        var notes = new List<LearningNote>();

        // Act
        var result = ApplyFormatter.BuildCopilotInstructions(notes);

        // Assert
        Assert.Contains("No relevant learnings found", result);
    }

    [Fact]
    public void BuildCopilotInstructions_WithNotes_FormatsCorrectly()
    {
        // Arrange
        var notes = new List<LearningNote>
        {
            new(1, "repo1", "Email timeout", "Problem", "Increase timeout and add retry logic", null, null, "confirmed", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null),
            new(2, "repo2", "Cache bug", "Cache wrong", "Clear cache directory on startup", null, null, "likely", "2024-01-02T00:00:00Z", "2024-01-02T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildCopilotInstructions(notes);

        // Assert
        Assert.Contains("### Copilot Instructions", result);
        Assert.Contains("Email timeout", result);
        Assert.Contains("Cache bug", result);
        Assert.Contains("[repo1]", result);
        Assert.Contains("[repo2]", result);
    }

    [Fact]
    public void BuildCopilotInstructions_TruncatesLongSolutions()
    {
        // Arrange
        var longSolution = string.Concat(Enumerable.Repeat("x", 200));
        var notes = new List<LearningNote>
        {
            new(1, "repo", "Title", "Problem", longSolution, null, null, "confirmed", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildCopilotInstructions(notes);

        // Assert
        Assert.Contains("...", result);
        Assert.DoesNotContain(longSolution, result);
    }

    [Fact]
    public void BuildCopilotInstructions_IncludesRepoContext()
    {
        // Arrange
        var notes = new List<LearningNote>
        {
            new(1, "example-repo", "Issue A", "Problem", "Solution A", null, null, "confirmed", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null),
            new(2, "api-gateway", "Issue B", "Problem", "Solution B", null, null, "confirmed", "2024-01-02T00:00:00Z", "2024-01-02T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildCopilotInstructions(notes);

        // Assert
        Assert.Contains("[example-repo]", result);
        Assert.Contains("[api-gateway]", result);
    }

    [Fact]
    public void BuildChecklist_EmptyNotes_ReturnsPlaceholder()
    {
        // Arrange
        var notes = new List<LearningNote>();

        // Act
        var result = ApplyFormatter.BuildChecklist(notes);

        // Assert
        Assert.Contains("No relevant learnings found", result);
    }

    [Fact]
    public void BuildChecklist_WithNotes_CreatesCheckboxes()
    {
        // Arrange
        var notes = new List<LearningNote>
        {
            new(1, "repo", "Email timeout fix", "Problem", "Solution", null, "When timeout occurs", "confirmed", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null),
            new(2, "repo", "Cache optimization", "Problem", "Solution", null, "Before deployment", "likely", "2024-01-02T00:00:00Z", "2024-01-02T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildChecklist(notes);

        // Assert
        Assert.Contains("- [ ]", result);
        Assert.Contains("Email timeout fix", result);
        Assert.Contains("Cache optimization", result);
        Assert.Contains("### Checklist", result);
    }

    [Fact]
    public void BuildChecklist_WithAppliesWhen_IncludesConditions()
    {
        // Arrange
        var notes = new List<LearningNote>
        {
            new(1, "repo", "Issue", "Problem", "Solution", null, "When X happens", "confirmed", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildChecklist(notes);

        // Assert
        Assert.Contains("When X happens", result);
        Assert.Contains("When:", result);
    }

    [Fact]
    public void BuildGotchasSummary_EmptyNotes_ReturnsPlaceholder()
    {
        // Arrange
        var notes = new List<LearningNote>();

        // Act
        var result = ApplyFormatter.BuildGotchasSummary(notes);

        // Assert
        Assert.Contains("None found", result);
    }

    [Fact]
    public void BuildGotchasSummary_SeparatesConfirmedFromOthers()
    {
        // Arrange
        var notes = new List<LearningNote>
        {
            new(1, "repo", "Confirmed issue", "Problem A", "Solution", null, null, "confirmed", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null),
            new(2, "repo", "Likely issue", "Problem B", "Solution", null, null, "likely", "2024-01-02T00:00:00Z", "2024-01-02T00:00:00Z", null),
            new(3, "repo", "Hypothesis", "Problem C", "Solution", null, null, "hypothesis", "2024-01-03T00:00:00Z", "2024-01-03T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildGotchasSummary(notes);

        // Assert
        Assert.Contains("Confirmed issues:", result);
        Assert.Contains("Other observations:", result);
        Assert.Contains("Confirmed issue", result);
        Assert.Contains("Likely issue", result);
        Assert.Contains("Hypothesis", result);
    }

    [Fact]
    public void BuildGotchasSummary_OnlyConfirmed_DoesNotShowOtherSection()
    {
        // Arrange
        var notes = new List<LearningNote>
        {
            new(1, "repo", "Issue", "Problem", "Solution", null, null, "confirmed", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildGotchasSummary(notes);

        // Assert
        Assert.Contains("Confirmed issues:", result);
        Assert.DoesNotContain("Other observations:", result);
    }

    [Fact]
    public void BuildGotchasSummary_OnlyUnconfirmed_DoesNotShowConfirmedSection()
    {
        // Arrange
        var notes = new List<LearningNote>
        {
            new(1, "repo", "Maybe issue", "Problem", "Solution", null, null, "likely", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildGotchasSummary(notes);

        // Assert
        Assert.DoesNotContain("Confirmed issues:", result);
        Assert.Contains("Other observations:", result);
    }

    [Fact]
    public void BuildGotchasSummary_IncludesConfidenceLevel()
    {
        // Arrange
        var notes = new List<LearningNote>
        {
            new(1, "repo", "Issue", "Problem", "Solution", null, null, "likely", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildGotchasSummary(notes);

        // Assert
        Assert.Contains("(likely)", result);
    }

    [Fact]
    public void BuildGotchasSummary_TruncatesLongProblems()
    {
        // Arrange
        var longProblem = string.Concat(Enumerable.Repeat("x", 150));
        var notes = new List<LearningNote>
        {
            new(1, "repo", "Issue", longProblem, "Solution", null, null, "confirmed", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null)
        };

        // Act
        var result = ApplyFormatter.BuildGotchasSummary(notes);

        // Assert
        Assert.Contains("...", result);
        Assert.DoesNotContain(longProblem, result);
    }

    [Fact]
    public void BuildCopilotInstructions_MultipleNotes_AllIncluded()
    {
        // Arrange
        var notes = new List<LearningNote>();
        for (int i = 0; i < 5; i++)
        {
            notes.Add(new(i + 1, $"repo{i}", $"Title{i}", $"Problem{i}", $"Solution{i}", null, null, "confirmed", "2024-01-01T00:00:00Z", "2024-01-01T00:00:00Z", null));
        }

        // Act
        var result = ApplyFormatter.BuildCopilotInstructions(notes);

        // Assert
        for (int i = 0; i < 5; i++)
        {
            Assert.Contains($"Title{i}", result);
        }
    }
}
