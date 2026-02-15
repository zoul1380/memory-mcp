using MemoryMcp.Database;
using MemoryMcp.Formatting;
using MemoryMcp.Models;
using Xunit;

namespace MemoryMcp.Tests.Integration;

public class EndToEndWorkflowTests : IAsyncLifetime
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

    public Task DisposeAsync()
    {
        _fixture.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task FullWorkflow_CaptureSearchAndApply()
    {
        // Scenario: Developer discovers email timeout issue, captures it, then searches for it later

        // Step 1: Capture learning
        var captureInput = new LearningNoteInput(
            RepoKey: "example-repo",
            Title: "Email reminder sends twice on retry",
            Problem: "Hiring managers receive duplicate reminder emails after approval completed",
            Solution: "Check approval state before sending; verify idempotency; add deduplication check",
            RootCause: "Race condition between approval update and email job",
            AppliesWhen: "When resend button clicked while approval processing",
            Confidence: "confirmed",
            Tags: new[] { "email", "approval", "race-condition", "deployment" },
            Links: new[] { ("PR", "https://github.com/org/example-repo/pull/5823"), ("Jira", "https://jira.example.com/TEAM-1234") }
        );

        var noteId = await _repo.AddLearningAsync(captureInput);
        Assert.True(noteId > 0);

        // Step 2: Verify it was stored
        var stored = await _repo.GetByIdAsync(noteId);
        Assert.NotNull(stored);
        Assert.Equal("example-repo", stored!.RepoKey);
        Assert.Equal("confirmed", stored.Confidence);

        // Step 3: Search for it by keyword
        var results = await _repo.SearchAsync("email reminder", repoKey: "example-repo");
        Assert.NotEmpty(results);
        Assert.Equal(noteId, results[0].Id);

        // Step 4: Apply to current session - generate Copilot instructions
        var instructions = ApplyFormatter.BuildCopilotInstructions(results);
        Assert.Contains("Email reminder sends twice on retry", instructions);
        Assert.Contains("example-repo", instructions);

        // Step 5: Generate checklist
        var checklist = ApplyFormatter.BuildChecklist(results);
        Assert.Contains("Email reminder sends twice on retry", checklist);
        Assert.Contains("When resend button clicked", checklist);

        // Step 6: Generate gotchas summary
        var gotchas = ApplyFormatter.BuildGotchasSummary(results);
        Assert.Contains("Confirmed issues", gotchas);
        Assert.Contains("duplicate reminder emails", gotchas);
    }

    [Fact]
    public async Task MultiRepoSearch_FindsRelevantLearnings()
    {
        // Scenario: Developer works on multiple repos and needs cross-repo insights

        // Add learnings to different repos
        var id1 = await _repo.AddLearningAsync(new LearningNoteInput(
            RepoKey: "api-gateway",
            Title: "Timeout handling",
            Problem: "Requests timeout after 30s",
            Solution: "Increase timeout and add exponential backoff"
        ));

        var id2 = await _repo.AddLearningAsync(new LearningNoteInput(
            RepoKey: "payment-service",
            Title: "Timeout on payment retry",
            Problem: "Payment retries timeout",
            Solution: "Add circuit breaker pattern"
        ));

        var id3 = await _repo.AddLearningAsync(new LearningNoteInput(
            RepoKey: "notification-service",
            Title: "Email processing",
            Problem: "Emails not sending",
            Solution: "Check queue"
        ));

        // Cross-repo search for "timeout"
        var results = await _repo.SearchAsync("timeout");
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Id == id1);
        Assert.Contains(results, r => r.Id == id2);
        Assert.DoesNotContain(results, r => r.Id == id3);
    }

    [Fact]
    public async Task TagFilteredSearch()
    {
        // Scenario: Filter learnings by tags instead of full text

        // Add learning with tags
        var id1 = await _repo.AddLearningAsync(new LearningNoteInput(
            RepoKey: "backend",
            Title: "Deployment issue",
            Problem: "Deployment fails",
            Solution: "Check permissions",
            Tags: new[] { "deployment", "critical" }
        ));

        var id2 = await _repo.AddLearningAsync(new LearningNoteInput(
            RepoKey: "backend",
            Title: "Docker configuration",
            Problem: "Container crash with deployment process",
            Solution: "Update base image",
            Tags: new[] { "docker", "deployment" }
        ));

        var id3 = await _repo.AddLearningAsync(new LearningNoteInput(
            RepoKey: "backend",
            Title: "Database migration",
            Problem: "Migration fails",
            Solution: "Check schema",
            Tags: new[] { "database", "migration" }
        ));

        // Get all repo entries and filter by tag manually (or via dedicated tag search if implemented)
        var allRepo = await _repo.GetByRepoAsync("backend");
        Assert.Equal(3, allRepo.Count);

        // Search for "deployment" across the repo (searches FTS fields, not tags)
        var deploymentResults = await _repo.SearchAsync("deployment", repoKey: "backend");
        Assert.True(deploymentResults.Count >= 2); // Should find id1 (title) and id2 (problem)
    }

    [Fact]
    public async Task ConfidenceFilterWorkflow()
    {
        // Scenario: Find only confirmed issues for critical path code changes

        await _repo.AddLearningAsync(new LearningNoteInput(
            RepoKey: "payment",
            Title: "Confirmed bug in retry logic",
            Problem: "Duplicate charges",
            Solution: "Add idempotency check",
            Confidence: "confirmed"
        ));

        await _repo.AddLearningAsync(new LearningNoteInput(
            RepoKey: "payment",
            Title: "Possible issue",
            Problem: "Maybe slow query",
            Solution: "Add index",
            Confidence: "hypothesis"
        ));

        // Get all and manually filter by confidence
        var all = await _repo.GetByRepoAsync("payment");
        var confirmed = all.Where(n => n.Confidence == "confirmed").ToList();
        var hypotheses = all.Where(n => n.Confidence == "hypothesis").ToList();

        Assert.Single(confirmed);
        Assert.Single(hypotheses);
    }

    [Fact]
    public async Task UpdateLearning_ScenarioVerifiedAgain()
    {
        // Scenario: Developer verifies an old learning after an upgrade

        // Initial capture
        var input = new LearningNoteInput(
            RepoKey: "legacy-app",
            Title: "Old dependency issue",
            Problem: "Library X caused crashes",
            Solution: "Downgrade to version 2.x",
            Confidence: "confirmed",
            RootCause: "Incompatibility with system library"
        );

        var id = await _repo.AddLearningAsync(input);

        // Verify it exists
        var note1 = await _repo.GetByIdAsync(id);
        Assert.NotNull(note1);
        Assert.Null(note1!.LastVerifiedAt);

        // Later: simulate update with verification (in real implementation, would update via UpdateAsync method)
        // For now, delete and re-add to demonstrate the workflow
        await _repo.DeleteAsync(id);

        var updatedInput = new LearningNoteInput(
            RepoKey: "legacy-app",
            Title: "Old dependency issue",
            Problem: "Library X caused crashes",
            Solution: "Upgrade to version 3.x (fixed in latest)",
            Confidence: "confirmed",
            RootCause: "Was incompatibility with system library (now resolved)"
        );

        var newId = await _repo.AddLearningAsync(updatedInput);
        var note2 = await _repo.GetByIdAsync(newId);
        Assert.NotNull(note2);
        Assert.Equal("Upgrade to version 3.x (fixed in latest)", note2!.Solution);
    }

    [Fact]
    public async Task BuildCompleteOutputForSession()
    {
        // Scenario: Full output generation for a coding session

        // Simulate the developer is working on the API Gateway and wants guidance
        var gatewayLearnings = new List<LearningNoteInput>
        {
            new("api-gateway", "Connection pooling", "Runs out of connections", "Increase pool size to 50", Confidence: "confirmed"),
            new("api-gateway", "Request tracing", "Hard to debug", "Add correlation IDs", Confidence: "confirmed"),
            new("api-gateway", "Rate limiting", "Need per-client limits", "Use RedisStore", Tags: new[] { "performance" })
        };

        var ids = new List<long>();
        foreach (var learning in gatewayLearnings)
        {
            ids.Add(await _repo.AddLearningAsync(learning));
        }

        // Search for relevant learnings
        var relevant = await _repo.GetByRepoAsync("api-gateway");
        Assert.Equal(3, relevant.Count);

        // Generate outputs
        var copilotInstructions = ApplyFormatter.BuildCopilotInstructions(relevant);
        var checklist = ApplyFormatter.BuildChecklist(relevant);
        var gotchas = ApplyFormatter.BuildGotchasSummary(relevant);

        // Verify all three outputs are generated
        Assert.NotEmpty(copilotInstructions);
        Assert.NotEmpty(checklist);
        Assert.NotEmpty(gotchas);

        // Verify content
        Assert.Contains("Connection pooling", copilotInstructions);
        Assert.Contains("Request tracing", checklist);
        Assert.Contains("Confirmed issues", gotchas);
    }
}
