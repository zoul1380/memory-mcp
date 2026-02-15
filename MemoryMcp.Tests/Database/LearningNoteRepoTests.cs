using MemoryMcp.Database;
using MemoryMcp.Models;
using Xunit;

namespace MemoryMcp.Tests.Database;

public class LearningNoteRepoTests : IAsyncLifetime
{
    private readonly TestDbFixture _fixture = new();
    private McpDb _db = null!;
    private LearningNoteRepo _repo = null!;

    public async Task InitializeAsync()
    {
        _db = new McpDb(_fixture.DbPath);
        await _db.InitializeAsync();
        _repo = new LearningNoteRepo(_db.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        _fixture.Dispose();
    }

    [Fact]
    public async Task AddLearningAsync_WithMinimalFields_CreatesNote()
    {
        // Arrange
        var input = new LearningNoteInput(
            RepoKey: "test-repo",
            Title: "Test issue",
            Problem: "Something broke",
            Solution: "Fix it this way"
        );

        // Act
        var id = await _repo.AddLearningAsync(input);

        // Assert
        Assert.True(id > 0);
        var note = await _repo.GetByIdAsync(id);
        Assert.NotNull(note);
        Assert.Equal("test-repo", note!.RepoKey);
        Assert.Equal("Test issue", note.Title);
        Assert.Equal("Something broke", note.Problem);
        Assert.Equal("Fix it this way", note.Solution);
        Assert.Equal("likely", note.Confidence);
    }

    [Fact]
    public async Task AddLearningAsync_WithAllFields_CreatesCompleteNote()
    {
        // Arrange
        var input = new LearningNoteInput(
            RepoKey: "admin-app",
            Title: "Email reminder race condition",
            Problem: "Reminder sent after approval completed",
            Solution: "Check state before sending; add verification",
            RootCause: "Async job picks up old message",
            AppliesWhen: "When approval workflow is in flight",
            Confidence: "confirmed",
            Tags: new[] { "email", "approval", "race-condition" },
            Links: new[] { ("PR", "https://github.com/org/repo/pull/123"), ("Jira", "https://jira.example.com/TASK-456") }
        );

        // Act
        var id = await _repo.AddLearningAsync(input);

        // Assert
        Assert.True(id > 0);
        var note = await _repo.GetByIdAsync(id);
        Assert.NotNull(note);
        Assert.Equal("admin-app", note!.RepoKey);
        Assert.Equal("Email reminder race condition", note.Title);
        Assert.Equal("confirmed", note.Confidence);
        Assert.Equal("Async job picks up old message", note.RootCause);
        Assert.Equal("When approval workflow is in flight", note.AppliesWhen);

        // Check tags
        var (tags, links) = await _repo.GetMetaAsync(id);
        Assert.Equal(3, tags.Count);
        Assert.Contains("email", tags);
        Assert.Contains("approval", tags);
        Assert.Contains("race-condition", tags);

        // Check links
        Assert.Equal(2, links.Count);
        Assert.Contains(("PR", "https://github.com/org/repo/pull/123"), links);
        Assert.Contains(("Jira", "https://jira.example.com/TASK-456"), links);
    }

    [Fact]
    public async Task AddLearningAsync_NullRepoKey_Throws()
    {
        // Arrange
        var input = new LearningNoteInput(
            RepoKey: null!,
            Title: "Title",
            Problem: "Problem",
            Solution: "Solution"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repo.AddLearningAsync(input));
    }

    [Fact]
    public async Task AddLearningAsync_EmptyTitle_Throws()
    {
        // Arrange
        var input = new LearningNoteInput(
            RepoKey: "repo",
            Title: "  ",
            Problem: "Problem",
            Solution: "Solution"
        );

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _repo.AddLearningAsync(input));
    }

    [Fact]
    public async Task AddLearningAsync_WithTags_PersistsTags()
    {
        // Arrange
        var tags = new[] { "deployment", "docker", "ci-cd" };
        var input = new LearningNoteInput(
            RepoKey: "infra",
            Title: "Docker build timeout",
            Problem: "Build times out on CI",
            Solution: "Increase timeout and optimize layers",
            Tags: tags
        );

        // Act
        var id = await _repo.AddLearningAsync(input);

        // Assert
        var (retrievedTags, _) = await _repo.GetMetaAsync(id);
        Assert.Equal(3, retrievedTags.Count);
        Assert.Equal(tags.OrderBy(x => x), retrievedTags.OrderBy(x => x));
    }

    [Fact]
    public async Task AddLearningAsync_WithDuplicateTags_DeduplicatesTags()
    {
        // Arrange
        var input = new LearningNoteInput(
            RepoKey: "repo",
            Title: "Title",
            Problem: "Problem",
            Solution: "Solution",
            Tags: new[] { "bug", "bug", "important", "important" }
        );

        // Act
        var id = await _repo.AddLearningAsync(input);

        // Assert
        var (tags, _) = await _repo.GetMetaAsync(id);
        Assert.Equal(2, tags.Count);
        Assert.Contains("bug", tags);
        Assert.Contains("important", tags);
    }

    [Fact]
    public async Task AddLearningAsync_WithLinks_PersistsLinks()
    {
        // Arrange
        var links = new[] { ("PR", "https://github.com/org/repo/pull/123"), ("Issue", "https://github.com/org/repo/issues/456") };
        var input = new LearningNoteInput(
            RepoKey: "repo",
            Title: "Title",
            Problem: "Problem",
            Solution: "Solution",
            Links: links
        );

        // Act
        var id = await _repo.AddLearningAsync(input);

        // Assert
        var (_, retrievedLinks) = await _repo.GetMetaAsync(id);
        Assert.Equal(2, retrievedLinks.Count);
        Assert.Equal(links, retrievedLinks);
    }

    [Fact]
    public async Task SearchAsync_ByKeyword_ReturnsMatches()
    {
        // Arrange - add multiple learning notes
        await _repo.AddLearningAsync(new LearningNoteInput("repo1", "Email timeout", "Emails slow", "Increase timeout", Confidence: "confirmed"));
        await _repo.AddLearningAsync(new LearningNoteInput("repo2", "Cache bug", "Cache stale", "Clear cache"));

        // Act
        var results = await _repo.SearchAsync("email", limit: 10);

        // Assert
        Assert.Single(results);
        Assert.Equal("Email timeout", results[0].Title);
    }

    [Fact]
    public async Task SearchAsync_FtsOr_ReturnsMultiple()
    {
        // Arrange
        await _repo.AddLearningAsync(new LearningNoteInput("repo", "Email cache", "Email cache issue", "Fix"));
        await _repo.AddLearningAsync(new LearningNoteInput("repo", "Timeout bug", "Timeout happens", "Increase"));

        // Act
        var results = await _repo.SearchAsync("email OR timeout", limit: 10);

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchAsync_RepoScoped_FiltersResults()
    {
        // Arrange
        await _repo.AddLearningAsync(new LearningNoteInput("repo1", "Issue A", "Problem", "Solution"));
        await _repo.AddLearningAsync(new LearningNoteInput("repo2", "Issue A", "Problem", "Solution"));

        // Act
        var results = await _repo.SearchAsync("Issue", repoKey: "repo1", limit: 10);

        // Assert
        Assert.Single(results);
        Assert.Equal("repo1", results[0].RepoKey);
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_ReturnsEmpty()
    {
        // Arrange
        await _repo.AddLearningAsync(new LearningNoteInput("repo", "Title", "Problem", "Solution"));

        // Act
        var results = await _repo.SearchAsync("", limit: 10);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_NoMatches_ReturnsEmpty()
    {
        // Arrange
        await _repo.AddLearningAsync(new LearningNoteInput("repo", "Title", "Problem", "Solution"));

        // Act
        var results = await _repo.SearchAsync("nonexistent", limit: 10);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_RespectLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _repo.AddLearningAsync(new LearningNoteInput("repo", $"Issue {i}", "Problem", "Solution"));
        }

        // Act
        var results = await _repo.SearchAsync("Issue", limit: 5);

        // Assert
        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingId_ReturnsNote()
    {
        // Arrange
        var input = new LearningNoteInput("repo", "Title", "Problem", "Solution");
        var id = await _repo.AddLearningAsync(input);

        // Act
        var note = await _repo.GetByIdAsync(id);

        // Assert
        Assert.NotNull(note);
        Assert.Equal(id, note!.Id);
        Assert.Equal("Title", note.Title);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        // Act
        var note = await _repo.GetByIdAsync(99999);

        // Assert
        Assert.Null(note);
    }

    [Fact]
    public async Task DeleteAsync_ExistingId_DeletesNote()
    {
        // Arrange
        var id = await _repo.AddLearningAsync(new LearningNoteInput("repo", "Title", "Problem", "Solution"));

        // Act
        var deleted = await _repo.DeleteAsync(id);

        // Assert
        Assert.True(deleted);
        var note = await _repo.GetByIdAsync(id);
        Assert.Null(note);
    }

    [Fact]
    public async Task DeleteAsync_NonExistentId_ReturnsFalse()
    {
        // Act
        var deleted = await _repo.DeleteAsync(99999);

        // Assert
        Assert.False(deleted);
    }

    [Fact]
    public async Task CountAsync_ReturnsCorrectCount()
    {
        // Arrange
        await _repo.AddLearningAsync(new LearningNoteInput("repo1", "Title1", "Problem", "Solution"));
        await _repo.AddLearningAsync(new LearningNoteInput("repo1", "Title2", "Problem", "Solution"));
        await _repo.AddLearningAsync(new LearningNoteInput("repo2", "Title3", "Problem", "Solution"));

        // Act
        var totalCount = await _repo.CountAsync();
        var repo1Count = await _repo.CountAsync("repo1");
        var repo2Count = await _repo.CountAsync("repo2");

        // Assert
        Assert.Equal(3, totalCount);
        Assert.Equal(2, repo1Count);
        Assert.Equal(1, repo2Count);
    }

    [Fact]
    public async Task GetByRepoAsync_ReturnsRepoNotes()
    {
        // Arrange
        var id1 = await _repo.AddLearningAsync(new LearningNoteInput("repo1", "Title1", "Problem", "Solution"));
        var id2 = await _repo.AddLearningAsync(new LearningNoteInput("repo1", "Title2", "Problem", "Solution"));
        await _repo.AddLearningAsync(new LearningNoteInput("repo2", "Title3", "Problem", "Solution"));

        // Act
        var results = await _repo.GetByRepoAsync("repo1");

        // Assert
        Assert.Equal(2, results.Count);
        Assert.True(results.All(r => r.RepoKey == "repo1"));
    }

    [Fact]
    public async Task GetByRepoAsync_ReturnsOrderedByUpdateDesc()
    {
        // Arrange
        var id1 = await _repo.AddLearningAsync(new LearningNoteInput("repo", "Title1", "Problem", "Solution"));
        var id2 = await _repo.AddLearningAsync(new LearningNoteInput("repo", "Title2", "Problem", "Solution"));

        // Act
        var results = await _repo.GetByRepoAsync("repo");

        // Assert
        // Most recent should be first
        Assert.Equal(id2, results[0].Id);
        Assert.Equal(id1, results[1].Id);
    }

    [Fact]
    public async Task GetByRepoAsync_RespectLimit()
    {
        // Arrange
        for (int i = 0; i < 10; i++)
        {
            await _repo.AddLearningAsync(new LearningNoteInput("repo", $"Title{i}", "Problem", "Solution"));
        }

        // Act
        var results = await _repo.GetByRepoAsync("repo", limit: 3);

        // Assert
        Assert.Equal(3, results.Count);
    }
}
