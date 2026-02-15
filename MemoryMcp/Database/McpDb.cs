using Dapper;
using Microsoft.Data.Sqlite;

namespace MemoryMcp.Database;

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
