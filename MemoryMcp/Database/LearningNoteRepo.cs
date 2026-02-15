using Dapper;
using MemoryMcp.Models;
using Microsoft.Data.Sqlite;

namespace MemoryMcp.Database;

public sealed class LearningNoteRepo
{
    private readonly string _connStr;

    public LearningNoteRepo(string connectionString)
    {
        _connStr = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connStr);
        conn.Open();
        return conn;
    }

    public async Task<long> AddLearningAsync(LearningNoteInput input)
    {
        if (string.IsNullOrWhiteSpace(input.RepoKey))
            throw new ArgumentException("repoKey required", nameof(input.RepoKey));
        if (string.IsNullOrWhiteSpace(input.Title))
            throw new ArgumentException("title required", nameof(input.Title));
        if (string.IsNullOrWhiteSpace(input.Problem))
            throw new ArgumentException("problem required", nameof(input.Problem));
        if (string.IsNullOrWhiteSpace(input.Solution))
            throw new ArgumentException("solution required", nameof(input.Solution));

        await using var conn = Open();
        await using var tx = conn.BeginTransaction();

        var noteId = await conn.ExecuteScalarAsync<long>(
            @"INSERT INTO learning_notes (repo_key, title, problem, solution, root_cause, applies_when, confidence)
              VALUES (@RepoKey, @Title, @Problem, @Solution, @RootCause, @AppliesWhen, @Confidence);
              SELECT last_insert_rowid();",
            input,
            tx);

        // tags
        if (input.Tags != null)
        {
            foreach (var tag in input.Tags)
            {
                var clean = tag?.Trim();
                if (string.IsNullOrWhiteSpace(clean)) continue;

                // upsert tag
                await conn.ExecuteAsync(
                    @"INSERT INTO tags(name) VALUES(@Name) ON CONFLICT(name) DO NOTHING;",
                    new { Name = clean },
                    tx);

                var tagId = await conn.ExecuteScalarAsync<long>(
                    @"SELECT id FROM tags WHERE name = @Name;",
                    new { Name = clean },
                    tx);

                await conn.ExecuteAsync(
                    @"INSERT OR IGNORE INTO learning_note_tags(learning_note_id, tag_id) VALUES(@NoteId, @TagId);",
                    new { NoteId = noteId, TagId = tagId },
                    tx);
            }
        }

        // links
        if (input.Links != null)
        {
            foreach (var (label, url) in input.Links)
            {
                if (string.IsNullOrWhiteSpace(url)) continue;
                await conn.ExecuteAsync(
                    @"INSERT INTO links(learning_note_id, label, url) VALUES(@NoteId, @Label, @Url);",
                    new { NoteId = noteId, Label = label?.Trim() ?? "Link", Url = url.Trim() },
                    tx);
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

        var sql = repoKey is null
            ? @"
SELECT 
    n.id, 
    n.repo_key as RepoKey, 
    n.title as Title, 
    n.problem as Problem, 
    n.solution as Solution, 
    n.root_cause as RootCause, 
    n.applies_when as AppliesWhen, 
    n.confidence as Confidence, 
    n.created_at as CreatedAt, 
    n.updated_at as UpdatedAt, 
    n.last_verified_at as LastVerifiedAt
FROM learning_notes_fts f
JOIN learning_notes n ON n.id = f.rowid
WHERE learning_notes_fts MATCH @query
ORDER BY bm25(learning_notes_fts) ASC
LIMIT @limit;"
            : @"
SELECT 
    n.id, 
    n.repo_key as RepoKey, 
    n.title as Title, 
    n.problem as Problem, 
    n.solution as Solution, 
    n.root_cause as RootCause, 
    n.applies_when as AppliesWhen, 
    n.confidence as Confidence, 
    n.created_at as CreatedAt, 
    n.updated_at as UpdatedAt, 
    n.last_verified_at as LastVerifiedAt
FROM learning_notes_fts f
JOIN learning_notes n ON n.id = f.rowid
WHERE n.repo_key = @repoKey
  AND learning_notes_fts MATCH @query
ORDER BY bm25(learning_notes_fts) ASC
LIMIT @limit;";

        var result = await conn.QueryAsync<LearningNote>(sql, new { query, repoKey, limit });
        return result.AsList();
    }

    public async Task<LearningNote?> GetByIdAsync(long id)
    {
        await using var conn = Open();
        var result = await conn.QuerySingleOrDefaultAsync<LearningNote>(@"
SELECT 
    id, 
    repo_key as RepoKey, 
    title as Title, 
    problem as Problem, 
    solution as Solution, 
    root_cause as RootCause, 
    applies_when as AppliesWhen, 
    confidence as Confidence, 
    created_at as CreatedAt, 
    updated_at as UpdatedAt, 
    last_verified_at as LastVerifiedAt
FROM learning_notes
WHERE id = @id;
", new { id });
        return result;
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

        var links = await conn.QueryAsync<(string, string)>(@"
SELECT label, url
FROM links
WHERE learning_note_id = @noteId
ORDER BY id ASC;
", new { noteId });

        return (tags.AsList(), links.Select(l => (l.Item1, l.Item2)).AsList());
    }

    public async Task<bool> DeleteAsync(long id)
    {
        await using var conn = Open();
        var result = await conn.ExecuteAsync(@"DELETE FROM learning_notes WHERE id = @id;", new { id });
        return result > 0;
    }

    public async Task<int> CountAsync(string? repoKey = null)
    {
        await using var conn = Open();
        if (repoKey is null)
        {
            return await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM learning_notes;");
        }
        return await conn.ExecuteScalarAsync<int>(@"SELECT COUNT(*) FROM learning_notes WHERE repo_key = @repoKey;", new { repoKey });
    }

    public async Task<IReadOnlyList<LearningNote>> GetByRepoAsync(string repoKey, int limit = 50)
    {
        if (string.IsNullOrWhiteSpace(repoKey)) return Array.Empty<LearningNote>();

        await using var conn = Open();
        var result = await conn.QueryAsync<LearningNote>(@"
SELECT 
    id, 
    repo_key as RepoKey, 
    title as Title, 
    problem as Problem, 
    solution as Solution, 
    root_cause as RootCause, 
    applies_when as AppliesWhen, 
    confidence as Confidence, 
    created_at as CreatedAt, 
    updated_at as UpdatedAt, 
    last_verified_at as LastVerifiedAt
FROM learning_notes
WHERE repo_key = @repoKey
ORDER BY updated_at DESC
LIMIT @limit;
", new { repoKey, limit });
        return result.AsList();
    }
}
