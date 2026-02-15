# MemoryMCP - Memory/Context Provider for Coding

A lightweight, SQLite-backed learning management system that captures, searches, and applies development insights across projects.

## Features

- **Capture** important learnings: bugs, fixes, gotchas, deployment steps
- **Search** using full-text search (FTS5) with keyword and semantic queries
- **Browse** by repository or across projects
- **Apply** learnings to current sessions with:
  - Copilot instructions snippets
  - Pre-flight checklists
  - Gotchas summaries

## Project Structure

```
MemoryMcp/
├── MemoryMcp/                      # Main library
│   ├── Database/
│   │   ├── McpDb.cs               # DB initialization & schema
│   │   └── LearningNoteRepo.cs    # Repository pattern (CRUD + search)
│   ├── Models/
│   │   └── LearningNote.cs        # Data models
│   ├── Formatting/
│   │   └── ApplyFormatter.cs      # Output generation (instructions, checklist, gotchas)
│   └── MemoryMcp.csproj
├── MemoryMcp.Tests/               # Comprehensive test suite
│   ├── Database/
│   │   ├── DatabaseInitializationTests.cs
│   │   └── LearningNoteRepoTests.cs
│   ├── Formatting/
│   │   └── ApplyFormatterTests.cs
│   ├── Integration/
│   │   └── EndToEndWorkflowTests.cs
│   ├── TestDbFixture.cs
│   ├── Usings.cs
│   └── MemoryMcp.Tests.csproj
├── MemoryMcp.sln
└── README.md
```

## Prerequisites

- .NET 8.0 or later
- Windows, macOS, or Linux

## Building

```bash
dotnet build MemoryMcp.sln
```

## Running Tests

Run all tests:
```bash
dotnet test MemoryMcp.sln
```

Run specific test class:
```bash
dotnet test MemoryMcp.sln --filter "DatabaseInitializationTests"
```

Run with verbose output:
```bash
dotnet test MemoryMcp.sln --verbosity detailed
```

## Test Coverage

### Unit Tests
- **DatabaseInitializationTests** (5 tests)
  - Schema creation
  - Table/FTS/trigger creation
  - Idempotency

- **LearningNoteRepoTests** (20+ tests)
  - CRUD operations
  - Full-text search (keyword, OR queries)
  - Repository-scoped vs global search
  - Tag management and deduplication
  - Link persistence
  - Counting and filtering
  - Error handling and validation

- **ApplyFormatterTests** (13 tests)
  - Copilot instruction generation
  - Checklist creation
  - Gotchas summary formatting
  - Text truncation and formatting
  - Confidence level handling

### Integration Tests
- **EndToEndWorkflowTests** (6 tests)
  - Full capture → search → apply workflow
  - Multi-repo searches
  - Tag-based filtering
  - Confidence filtering
  - Learning updates and verification
  - Complete session output generation

## Key Classes

### McpDb
Handles SQLite database initialization with:
- Learning notes table with full-text search
- Tags and tag mappings
- Links table for PR/Jira/doc references
- Automatic FTS synchronization via triggers
- Updated-at tracking

### LearningNoteRepo
Repository pattern implementation:
- `AddLearningAsync()` - Create new learning with tags/links
- `SearchAsync()` - FTS search (repo-scoped or global)
- `GetByIdAsync()` - Retrieve single note
- `GetByRepoAsync()` - All notes for a repo
- `GetMetaAsync()` - Associated tags and links
- `DeleteAsync()` - Remove a learning
- `CountAsync()` - Count notes

### ApplyFormatter
Formats search results for current session:
- `BuildCopilotInstructions()` - AI instruction snippet
- `BuildChecklist()` - Pre/post change checklist
- `BuildGotchasSummary()` - Issues by confidence level

## Example Usage

```csharp
// Initialize database
var db = new McpDb(@"%USERPROFILE%\.mcp\mcp.db");
await db.InitializeAsync();

// Create repository
var repo = new LearningNoteRepo(db.ConnectionString);

// Add a learning
var id = await repo.AddLearningAsync(new LearningNoteInput(
    RepoKey: "example-repo",
    Title: "Email reminder race condition",
    Problem: "Duplicate emails sent on retry",
    Solution: "Add idempotency check; verify state before send",
    RootCause: "Async message in flight when approval completes",
    Confidence: "confirmed",
    Tags: new[] { "email", "approval", "race-condition" },
    Links: new[] { ("PR", "https://github.com/org/repo/pull/123") }
));

// Search for it
var results = await repo.SearchAsync("email reminder", repoKey: "example-repo");

// Generate session aids
var instructions = ApplyFormatter.BuildCopilotInstructions(results);
var checklist = ApplyFormatter.BuildChecklist(results);
var gotchas = ApplyFormatter.BuildGotchasSummary(results);
```

## Data Model

### LearningNote
```csharp
record LearningNote(
    long Id,
    string RepoKey,              // e.g., "example-repo"
    string Title,                // Main heading
    string Problem,              // What went wrong
    string Solution,             // How to fix/prevent
    string? RootCause,           // Optional root cause
    string? AppliesWhen,         // When this learning applies
    string Confidence,           // "confirmed" | "likely" | "hypothesis"
    string CreatedAt,            // ISO8601 timestamp
    string UpdatedAt,            // ISO8601 timestamp
    string? LastVerifiedAt       // When last confirmed
)
```

### LearningNoteInput
Input model for adding learnings (all fields optional except core 4):
```csharp
record LearningNoteInput(
    string RepoKey,
    string Title,
    string Problem,
    string Solution,
    string? RootCause = null,
    string? AppliesWhen = null,
    string Confidence = "likely",
    IEnumerable<string>? Tags = null,
    IEnumerable<(string label, string url)>? Links = null
)
```

## Search Syntax (FTS5)

```sql
-- Simple keywords
"email timeout"

-- OR queries
"timeout OR deadline"

-- NEAR operator (within N words)
"reminder NEAR/3 approved"

-- Quoted phrases
"race condition"

-- Field-specific (if needed)
problem:timeout solution:retry
```

## Future Enhancements

- [ ] CLI tool (`mcp add`, `mcp search`, `mcp apply`)
- [ ] VS Code extension UI
- [ ] Automatic deduplication/merging
- [ ] Export markdown per repo
- [ ] Session grouping and history
- [ ] Update/edit learning operations
- [ ] Team sync and sharing (with permissions)
- [ ] Web UI
- [ ] Integration with Git hooks / PR templates

## License

MIT
