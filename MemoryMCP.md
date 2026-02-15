# MemoryMCP.md
A lightweight **Memory / Context Provider (MCP)** for coding work that:
- **captures important learnings** per repo/session
- **stores them in SQLite**
- **searches** fast (including full-text)
- **applies** relevant learnings to the current session (e.g., generate Copilot instructions / checklists)

---

## 1) Goals

### What MCP does
- Keeps important learnings on each session or repo (bugs, fixes, gotchas, steps, commands, PR notes).
- Lets you search those learnings and apply them to your current work.

### Why we’re building this
- Reduce “re-learning” across repos.
- Turn tribal knowledge into reusable, searchable, repo-aware context.
- Generate ready-to-paste “Copilot instructions” and quick checklists.

### Non-goals (for MVP)
- Team-wide sync and permissions (can be added later).
- Automatic ingestion from all sources (PRs/Slack/etc.).
- Cloud hosting.

---

## 2) User Workflows

### A) Capture a learning
Example scenarios:
- After debugging a tricky issue
- After a PR review (“why this was changed”)
- After a deployment/incident
- After setup (“how to run locally”)

Expected data captured:
- Problem (symptoms)
- Root cause (optional)
- Solution / steps
- Gotchas
- Commands used
- Links (Jira, PR, docs)
- Tags
- Confidence level

### B) Search learnings
Search types:
- keyword search: `binocular multisearch`
- semantic-like search via FTS: `reminder email sent even after approval`
- filtered search: repo-specific, tags, confidence, date range

### C) Apply to current session
Generate:
- **Copilot instruction snippet** (5–10 lines)
- **Checklist** (things to verify before/after changes)
- **Known gotchas** summary

---

## 3) Storage: SQLite

### File location (suggested)
- Windows: `%USERPROFILE%\.mcp\mcp.db`
- macOS/Linux: `~/.mcp/mcp.db`

### Why SQLite
- Zero setup, single file
- Fast queries + Full Text Search (FTS5)
- Easy backup/export

---

## 4) Data Model (MVP)

### Core Tables
- `learning_notes` — the learning entries
- `tags` — tag dictionary
- `learning_note_tags` — many-to-many mapping
- `links` — optional links per learning (PR/Jira/docs)

### Full Text Search (FTS5)
- `learning_notes_fts` — indexes `title`, `problem`, `solution`, etc.

We’ll keep FTS in sync via triggers.

---

## 5) SQLite Schema (CREATE TABLE + FTS + Triggers)

> Copy/paste this into a migration script.

```sql
PRAGMA foreign_keys = ON;

-- =========================================================
-- learning_notes: main stored learnings
-- =========================================================
CREATE TABLE IF NOT EXISTS learning_notes (
  id               INTEGER PRIMARY KEY AUTOINCREMENT,
  repo_key          TEXT NOT NULL,                   -- e.g. "example-repo"
  title             TEXT NOT NULL,
  problem           TEXT NOT NULL,
  solution          TEXT NOT NULL,
  root_cause        TEXT,
  applies_when      TEXT,
  confidence        TEXT NOT NULL DEFAULT 'likely',   -- confirmed|likely|hypothesis
  created_at        TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  updated_at        TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  last_verified_at  TEXT
);

CREATE INDEX IF NOT EXISTS idx_learning_notes_repo ON learning_notes(repo_key);
CREATE INDEX IF NOT EXISTS idx_learning_notes_confidence ON learning_notes(confidence);
CREATE INDEX IF NOT EXISTS idx_learning_notes_updated ON learning_notes(updated_at);

-- =========================================================
-- tags + mapping
-- =========================================================
CREATE TABLE IF NOT EXISTS tags (
  id     INTEGER PRIMARY KEY AUTOINCREMENT,
  name   TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS learning_note_tags (
  learning_note_id  INTEGER NOT NULL,
  tag_id            INTEGER NOT NULL,
  PRIMARY KEY (learning_note_id, tag_id),
  FOREIGN KEY (learning_note_id) REFERENCES learning_notes(id) ON DELETE CASCADE,
  FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_learning_note_tags_tag ON learning_note_tags(tag_id);

-- =========================================================
-- links
-- =========================================================
CREATE TABLE IF NOT EXISTS links (
  id              INTEGER PRIMARY KEY AUTOINCREMENT,
  learning_note_id INTEGER NOT NULL,
  label           TEXT NOT NULL,  -- "PR", "Jira", "Doc", etc.
  url             TEXT NOT NULL,
  FOREIGN KEY (learning_note_id) REFERENCES learning_notes(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_links_note ON links(learning_note_id);

-- =========================================================
-- Full Text Search (FTS5)
-- Keep in sync with triggers below.
-- =========================================================
CREATE VIRTUAL TABLE IF NOT EXISTS learning_notes_fts
USING fts5(
  title,
  problem,
  solution,
  root_cause,
  applies_when,
  repo_key,
  content='learning_notes',
  content_rowid='id'
);

-- =========================================================
-- Triggers to sync updated_at
-- =========================================================
CREATE TRIGGER IF NOT EXISTS trg_learning_notes_updated_at
AFTER UPDATE ON learning_notes
FOR EACH ROW
BEGIN
  UPDATE learning_notes
  SET updated_at = (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
  WHERE id = NEW.id;
END;

-- =========================================================
-- Triggers to sync FTS table
-- =========================================================
CREATE TRIGGER IF NOT EXISTS trg_learning_notes_ai
AFTER INSERT ON learning_notes
FOR EACH ROW
BEGIN
  INSERT INTO learning_notes_fts(rowid, title, problem, solution, root_cause, applies_when, repo_key)
  VALUES (NEW.id, NEW.title, NEW.problem, NEW.solution, NEW.root_cause, NEW.applies_when, NEW.repo_key);
END;

CREATE TRIGGER IF NOT EXISTS trg_learning_notes_ad
AFTER DELETE ON learning_notes
FOR EACH ROW
BEGIN
  INSERT INTO learning_notes_fts(learning_notes_fts, rowid, title, problem, solution, root_cause, applies_when, repo_key)
  VALUES ('delete', OLD.id, OLD.title, OLD.problem, OLD.solution, OLD.root_cause, OLD.applies_when, OLD.repo_key);
END;

CREATE TRIGGER IF NOT EXISTS trg_learning_notes_au
AFTER UPDATE ON learning_notes
FOR EACH ROW
BEGIN
  INSERT INTO learning_notes_fts(learning_notes_fts, rowid, title, problem, solution, root_cause, applies_when, repo_key)
  VALUES ('delete', OLD.id, OLD.title, OLD.problem, OLD.solution, OLD.root_cause, OLD.applies_when, OLD.repo_key);

  INSERT INTO learning_notes_fts(rowid, title, problem, solution, root_cause, applies_when, repo_key)
  VALUES (NEW.id, NEW.title, NEW.problem, NEW.solution, NEW.root_cause, NEW.applies_when, NEW.repo_key);
END;
```

---

## 6) Query Examples

### A) Search by repo + FTS query
```sql
SELECT
  n.id, n.repo_key, n.title, n.confidence, n.updated_at
FROM learning_notes_fts f
JOIN learning_notes n ON n.id = f.rowid
WHERE n.repo_key = @repoKey
  AND learning_notes_fts MATCH @query
ORDER BY bm25(learning_notes_fts) ASC
LIMIT @limit;
```

Example `@query`:
- `reminder NEAR/3 approved`
- `"binocular multisearch"`
- `NU1903 OR audit`

### B) Search across all repos (global)
```sql
SELECT n.id, n.repo_key, n.title, n.confidence, n.updated_at
FROM learning_notes_fts f
JOIN learning_notes n ON n.id = f.rowid
WHERE learning_notes_fts MATCH @query
ORDER BY bm25(learning_notes_fts) ASC
LIMIT @limit;
```

### C) Filter by tags (optional)
```sql
SELECT DISTINCT n.id, n.repo_key, n.title, n.updated_at
FROM learning_notes n
JOIN learning_note_tags nt ON nt.learning_note_id = n.id
JOIN tags t ON t.id = nt.tag_id
WHERE n.repo_key = @repoKey
  AND t.name IN ('deployment', 'email')
ORDER BY n.updated_at DESC
LIMIT 50;
```

---

## 7) C# Implementation (MVP)

### Dependencies
Option 1 (simple):
- `Microsoft.Data.Sqlite`

Option 2 (ergonomic):
- `Dapper` + `Microsoft.Data.Sqlite`

Below uses **Dapper** for cleaner code.

```bash
dotnet add package Microsoft.Data.Sqlite
dotnet add package Dapper
```

---

## 8) C# Code: DB bootstrap + repository methods

> This is a minimal, working shape you can expand.

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;

public sealed class McpDb
{
    private readonly string _dbPath;

    public McpDb(string dbPath)
    {
        _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
    }

    public string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = _dbPath,
        ForeignKeys = true
    }.ToString();

    public async Task InitializeAsync()
    {
        var dir = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        await using var conn = new SqliteConnection(ConnectionString);
        await conn.OpenAsync();

        // You can store this SQL in a file and run migrations later.
        var schemaSql = @"
PRAGMA foreign_keys = ON;

CREATE TABLE IF NOT EXISTS learning_notes (
  id               INTEGER PRIMARY KEY AUTOINCREMENT,
  repo_key          TEXT NOT NULL,
  title             TEXT NOT NULL,
  problem           TEXT NOT NULL,
  solution          TEXT NOT NULL,
  root_cause        TEXT,
  applies_when      TEXT,
  confidence        TEXT NOT NULL DEFAULT 'likely',
  created_at        TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  updated_at        TEXT NOT NULL DEFAULT (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  last_verified_at  TEXT
);

CREATE INDEX IF NOT EXISTS idx_learning_notes_repo ON learning_notes(repo_key);
CREATE INDEX IF NOT EXISTS idx_learning_notes_confidence ON learning_notes(confidence);
CREATE INDEX IF NOT EXISTS idx_learning_notes_updated ON learning_notes(updated_at);

CREATE TABLE IF NOT EXISTS tags (
  id     INTEGER PRIMARY KEY AUTOINCREMENT,
  name   TEXT NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS learning_note_tags (
  learning_note_id  INTEGER NOT NULL,
  tag_id            INTEGER NOT NULL,
  PRIMARY KEY (learning_note_id, tag_id),
  FOREIGN KEY (learning_note_id) REFERENCES learning_notes(id) ON DELETE CASCADE,
  FOREIGN KEY (tag_id) REFERENCES tags(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_learning_note_tags_tag ON learning_note_tags(tag_id);

CREATE TABLE IF NOT EXISTS links (
  id               INTEGER PRIMARY KEY AUTOINCREMENT,
  learning_note_id INTEGER NOT NULL,
  label            TEXT NOT NULL,
  url              TEXT NOT NULL,
  FOREIGN KEY (learning_note_id) REFERENCES learning_notes(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_links_note ON links(learning_note_id);

CREATE VIRTUAL TABLE IF NOT EXISTS learning_notes_fts
USING fts5(
  title,
  problem,
  solution,
  root_cause,
  applies_when,
  repo_key,
  content='learning_notes',
  content_rowid='id'
);

CREATE TRIGGER IF NOT EXISTS trg_learning_notes_updated_at
AFTER UPDATE ON learning_notes
FOR EACH ROW
BEGIN
  UPDATE learning_notes
  SET updated_at = (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
  WHERE id = NEW.id;
END;

CREATE TRIGGER IF NOT EXISTS trg_learning_notes_ai
AFTER INSERT ON learning_notes
FOR EACH ROW
BEGIN
  INSERT INTO learning_notes_fts(rowid, title, problem, solution, root_cause, applies_when, repo_key)
  VALUES (NEW.id, NEW.title, NEW.problem, NEW.solution, NEW.root_cause, NEW.applies_when, NEW.repo_key);
END;

CREATE TRIGGER IF NOT EXISTS trg_learning_notes_ad
AFTER DELETE ON learning_notes
FOR EACH ROW
BEGIN
  INSERT INTO learning_notes_fts(learning_notes_fts, rowid, title, problem, solution, root_cause, applies_when, repo_key)
  VALUES ('delete', OLD.id, OLD.title, OLD.problem, OLD.solution, OLD.root_cause, OLD.applies_when, OLD.repo_key);
END;

CREATE TRIGGER IF NOT EXISTS trg_learning_notes_au
AFTER UPDATE ON learning_notes
FOR EACH ROW
BEGIN
  INSERT INTO learning_notes_fts(learning_notes_fts, rowid, title, problem, solution, root_cause, applies_when, repo_key)
  VALUES ('delete', OLD.id, OLD.title, OLD.problem, OLD.solution, OLD.root_cause, OLD.applies_when, OLD.repo_key);

  INSERT INTO learning_notes_fts(rowid, title, problem, solution, root_cause, applies_when, repo_key)
  VALUES (NEW.id, NEW.title, NEW.problem, NEW.solution, NEW.root_cause, NEW.applies_when, NEW.repo_key);
END;
";
        await conn.ExecuteAsync(schemaSql);
    }
}

public sealed record LearningNote(
    long Id,
    string RepoKey,
    string Title,
    string Problem,
    string Solution,
    string? RootCause,
    string? AppliesWhen,
    string Confidence,
    string CreatedAt,
    string UpdatedAt,
    string? LastVerifiedAt
);

public sealed class LearningNoteRepo
{
    private readonly string _connStr;

    public LearningNoteRepo(string connectionString)
    {
        _connStr = connectionString;
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connStr);
        conn.Open();
        return conn;
    }

    public async Task<long> AddLearningAsync(
        string repoKey,
        string title,
        string problem,
        string solution,
        string? rootCause = null,
        string? appliesWhen = null,
        string confidence = "likely",
        IEnumerable<string>? tags = null,
        IEnumerable<(string label, string url)>? links = null)
    {
        if (string.IsNullOrWhiteSpace(repoKey)) throw new ArgumentException("repoKey required");
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("title required");
        if (string.IsNullOrWhiteSpace(problem)) throw new ArgumentException("problem required");
        if (string.IsNullOrWhiteSpace(solution)) throw new ArgumentException("solution required");

        await using var conn = Open();
        await using var tx = conn.BeginTransaction();

        var noteId = await conn.ExecuteScalarAsync<long>(@"
INSERT INTO learning_notes (repo_key, title, problem, solution, root_cause, applies_when, confidence)
VALUES (@repoKey, @title, @problem, @solution, @rootCause, @appliesWhen, @confidence);
SELECT last_insert_rowid();
", new { repoKey, title, problem, solution, rootCause, appliesWhen, confidence }, tx);

        // tags
        if (tags != null)
        {
            foreach (var tag in tags)
            {
                var clean = tag?.Trim();
                if (string.IsNullOrWhiteSpace(clean)) continue;

                // upsert tag
                await conn.ExecuteAsync(@"
INSERT INTO tags(name) VALUES(@name)
ON CONFLICT(name) DO NOTHING;
", new { name = clean }, tx);

                var tagId = await conn.ExecuteScalarAsync<long>(@"
SELECT id FROM tags WHERE name = @name;
", new { name = clean }, tx);

                await conn.ExecuteAsync(@"
INSERT OR IGNORE INTO learning_note_tags(learning_note_id, tag_id)
VALUES(@noteId, @tagId);
", new { noteId, tagId }, tx);
            }
        }

        // links
        if (links != null)
        {
            foreach (var (label, url) in links)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;
                await conn.ExecuteAsync(@"
INSERT INTO links(learning_note_id, label, url)
VALUES(@noteId, @label, @url);
", new { noteId, label = label?.Trim() ?? "Link", url = url.Trim() }, tx);
            }
        }

        await tx.CommitAsync();
        return noteId;
    }

    public async Task<IReadOnlyList<LearningNote>> SearchAsync(
        string query,
        string? repoKey = null,
        int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<LearningNote>();
        if (limit <= 0) limit = 10;

        await using var conn = Open();

        // NOTE: FTS5 MATCH syntax differs from normal LIKE.
        // Example queries:
        //   "reminder NEAR/3 approved"
        //   ""binocular multisearch""
        //   "NU1903 OR audit"
        var sql = repoKey is null
            ? @"
SELECT n.*
FROM learning_notes_fts f
JOIN learning_notes n ON n.id = f.rowid
WHERE learning_notes_fts MATCH @query
ORDER BY bm25(learning_notes_fts) ASC
LIMIT @limit;"
            : @"
SELECT n.*
FROM learning_notes_fts f
JOIN learning_notes n ON n.id = f.rowid
WHERE n.repo_key = @repoKey
  AND learning_notes_fts MATCH @query
ORDER BY bm25(learning_notes_fts) ASC
LIMIT @limit;";

        var result = await conn.QueryAsync<LearningNote>(sql, new { query, repoKey, limit });
        return result.AsList();
    }

    public async Task<(IReadOnlyList<string> Tags, IReadOnlyList<(string Label, string Url)> Links)>
        GetMetaAsync(long noteId)
    {
        await using var conn = Open();

        var tags = await conn.QueryAsync<string>(@"
SELECT t.name
FROM learning_note_tags nt
JOIN tags t ON t.id = nt.tag_id
WHERE nt.learning_note_id = @noteId
ORDER BY t.name ASC;
", new { noteId });

        var links = await conn.QueryAsync<(string Label, string Url)>(@"
SELECT label AS Label, url AS Url
FROM links
WHERE learning_note_id = @noteId
ORDER BY id ASC;
", new { noteId });

        return (tags.AsList(), links.AsList());
    }
}
```

---

## 9) “Apply to session” formatter (MVP)

This is simple: take the top N results, then generate:
- Copilot instructions
- checklist
- gotchas

```csharp
public static class ApplyFormatter
{
    public static string BuildCopilotInstructions(IEnumerable<LearningNote> notes)
    {
        var lines = new List<string>
        {
            "### Copilot Instructions (from MCP learnings)",
            "- Use repo conventions and existing patterns first.",
            "- Prefer small, low-risk changes; add logging when behavior changes.",
            ""
        };

        foreach (var n in notes)
        {
            lines.Add($"- [{n.RepoKey}] {n.Title}: {Compact(n.Solution, 140)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Compact(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return t.Length <= max ? t : t.Substring(0, max).Trim() + "...";
    }
}
```

---

## 10) Guardrails

### Redaction (recommended)
Before saving:
- detect and redact patterns like:
  - tokens/keys: `AIza...`, `ghp_...`, `sk-...`
  - connection strings/password fields
  - secrets in env vars

### Repo boundaries
- Default to repo-scoped search first.
- Allow cross-repo search only when explicitly asked or when repoKey is unknown.

### Confidence + staleness
- Confidence: `confirmed | likely | hypothesis`
- Optional `last_verified_at` update when you confirm again after upgrades.

---

## 11) MVP Features Checklist

### Must-have
- [ ] Create/open SQLite DB
- [ ] Add learning note (with tags + links)
- [ ] FTS search (repo scoped + global)
- [ ] Apply output: Copilot instructions snippet

### Nice-to-have next
- [ ] Merge/dedupe learnings
- [ ] CLI (`mcp add`, `mcp search`, `mcp apply`)
- [ ] VS Code extension UI
- [ ] Export markdown per repo
- [ ] Session grouping + history

---

## 12) Example Learning Note Template (for consistency)

**Title:**  
**Repo:**  
**Problem:**  
**Root cause:** (optional)  
**Solution:**  
**Applies when:**  
**Gotchas:**  
**Commands/Steps:**  
**Tags:**  
**Links:**  
**Confidence:** confirmed | likely | hypothesis

---

## 13) Example: Add + Search

### Add
- repo: `example-repo`
- title: `Resend approval reminder can fire after approval is completed`
- problem: `Hiring managers receive reminder emails after completion when resend comes from stale page`
- solution: `Check approval state before sending reminder; skip resend if already completed; add log`
- tags: `email`, `approval`, `deployment`
- links: Jira/PR

### Search
- query: `reminder NEAR/3 approved`
- repo: `example-repo`

Apply result: Copilot instructions + checklist.
