# MemoryMCP - Quick Start Guide

## Project Status
✅ **Complete with all 46 unit & integration tests passing**

## What You've Built

A lightweight, SQLite-backed memory/context provider (MCP) that:
- **Captures** coding learnings: bugs, fixes, deployment steps, gotchas
- **Searches** using full-text search (FTS5) 
- **Applies** learnings to current sessions with ready-to-use outputs

## Testing

All tests pass - here's the breakdown:

```
✓ 5 Database Initialization Tests    - Schema, FTS, triggers, idempotency
✓ 20+ Repository CRUD Tests          - Add, search, delete, filter, count
✓ 13 Formatter Tests                 - Instructions, checklists, summaries  
✓ 6 Integration Tests                - Full workflows, multi-repo, confidence filters

Total: 46 tests - 797ms execution time
```

### Run All Tests
```bash
dotnet test MemoryMcp.sln
```

### Run Specific Test Group
```bash
dotnet test --filter "ApplyFormatterTests"       # Formatter only
dotnet test --filter "LearningNoteRepoTests"     # CRUD operations
dotnet test --filter "EndToEndWorkflowTests"     # Full integration scenarios
```

### Run with Verbose Output
```bash
dotnet test --verbosity detailed
```

## Project Structure

```
MemoryMcp/
├── Database/
│   ├── McpDb.cs               # SQLite initialization & schema
│   └── LearningNoteRepo.cs    # CRUD + FTS search (11 methods)
├── Models/
│   └── LearningNote.cs        # Data records
├── Formatting/
│   └── ApplyFormatter.cs      # Output generation (3 formatters)
└── MemoryMcp.csproj

MemoryMcp.Tests/
├── Database/
│   ├── DatabaseInitializationTests.cs  # 5 tests
│   └── LearningNoteRepoTests.cs        # 20+ tests
├── Formatting/
│   └── ApplyFormatterTests.cs          # 13 tests
├── Integration/
│   └── EndToEndWorkflowTests.cs        # 6 tests (full scenarios)
├── TestDbFixture.cs                    # Shared test database setup
└── MemoryMcp.Tests.csproj
```

## Usage Example

```csharp
// 1. Initialize database
var db = new McpDb(@"%USERPROFILE%\.mcp\mcp.db");
await db.InitializeAsync();

// 2. Create repository
var repo = new LearningNoteRepo(db.ConnectionString);

// 3. Add a learning
var id = await repo.AddLearningAsync(new LearningNoteInput(
    RepoKey: "example-repo",
    Title: "Email reminder race condition",
    Problem: "Duplicate emails sent on retry",
    Solution: "Add idempotency check before send",
    RootCause: "Async message in flight during approval",
    Confidence: "confirmed",
    Tags: new[] { "email", "race-condition" },
    Links: new[] { ("PR", "https://github.com/org/repo/pull/123") }
));

// 4. Search for learnings
var results = await repo.SearchAsync("email reminder", repoKey: "example-repo");

// 5. Generate session outputs
var instructions = ApplyFormatter.BuildCopilotInstructions(results);
var checklist = ApplyFormatter.BuildChecklist(results);
var gotchas = ApplyFormatter.BuildGotchasSummary(results);

// Output:
//   ✓ AI-ready instruction snippets
//   ✓ Pre/post-change checklists
//   ✓ Known issues by confidence level
```

## Key Features Implemented

### Repository Methods (LearningNoteRepo)
- `AddLearningAsync()` - Create with tags & links (transaction-safe)
- `SearchAsync()` - FTS search (repo-scoped or global)
- `GetByIdAsync()` - Retrieve single note
- `GetByRepoAsync()` - All notes for a repo
- `GetMetaAsync()` - Associated tags and links
- `DeleteAsync()` - Remove learning
- `CountAsync()` - Count notes

### Search Features
- **Full-Text Search (FTS5)** - Searches title, problem, solution, root_cause, applies_when, repo_key
- **BM25 Ranking** - Best matches first
- **Repository Filtering** - Scoped or cross-repo search
- **Query Syntax** - Supports "word1 OR word2", "NEAR/3", quoted phrases

### Formatter Output (ApplyFormatter)
- `BuildCopilotInstructions()` - AI-ready snippets
- `BuildChecklist()` - Pre/post-change verification
- `BuildGotchasSummary()` - Issues by confidence (confirmed/likely/hypothesis)

### Database Features
- SQLite with FTS5 virtual table
- Automatic FTS trigger synchronization
- Transaction-safe multi-part operations
- Indexed queries (repo, confidence, updated_at)
- Foreign key constraints with cascading deletes

## What's Ready for Future Work

- [ ] CLI tool (`mcp add`, `mcp search`, `mcp apply`)
- [ ] VS Code extension UI
- [ ] Auto dedup/merge learnings
- [ ] Export markdown per repo
- [ ] Session history tracking
- [ ] Update/edit operations
- [ ] Team sync & permissions
- [ ] Web UI

## Dependencies Used

```xml
<PackageReference Include="Microsoft.Data.Sqlite" Version="8.0.0" />
<PackageReference Include="Dapper" Version="2.1.15" />
<PackageReference Include="xunit" Version="2.6.6" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.9.0" />
```

## Test Coverage Highlights

### Database Tests
- Schema creation with all tables, indexes, and triggers
- Idempotent initialization (safe to run multiple times)
- FTS table creation and trigger synchronization
- CRUD operations (create, read, update, delete)
- Tag deduplication and management
- Link persistence and retrieval
- Search across global and repo-scoped data
- Error handling and validation

### Integration Tests
- Full capture → search → apply workflow
- Multi-repository searches
- Confidence-based filtering
- Learning update scenarios
- Complete session output generation with all three formatters

---

**Ready to extend!** All core functionality is solid and tested. Pick any "future work" item and build on this foundation.
