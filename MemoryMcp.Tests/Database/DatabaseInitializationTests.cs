using Dapper;
using MemoryMcp.Database;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MemoryMcp.Tests.Database;

public class DatabaseInitializationTests : IDisposable
{
    private readonly TestDbFixture _fixture = new();

    public void Dispose() => _fixture.Dispose();

    [Fact]
    public async Task InitializeAsync_CreatesDatabase()
    {
        // Arrange
        var db = new McpDb(_fixture.DbPath);

        // Act
        await db.InitializeAsync();

        // Assert
        Assert.True(File.Exists(_fixture.DbPath));
    }

    [Fact]
    public async Task InitializeAsync_CreatesAllTables()
    {
        // Arrange
        var db = new McpDb(_fixture.DbPath);

        // Act
        await db.InitializeAsync();

        // Assert - verify tables exist by querying sqlite_master
        using var conn = new SqliteConnection(db.ConnectionString);
        await conn.OpenAsync();

        var tables = await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'");

        var tableList = tables.ToList();
        Assert.Contains("learning_notes", tableList);
        Assert.Contains("tags", tableList);
        Assert.Contains("learning_note_tags", tableList);
        Assert.Contains("links", tableList);
    }

    [Fact]
    public async Task InitializeAsync_CreatesFtsTable()
    {
        // Arrange
        var db = new McpDb(_fixture.DbPath);

        // Act
        await db.InitializeAsync();

        // Assert
        using var conn = new SqliteConnection(db.ConnectionString);
        await conn.OpenAsync();

        var exists = await conn.ExecuteScalarAsync(
            @"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='learning_notes_fts'");

        Assert.NotNull(exists);
        Assert.True((long)exists! > 0);
    }

    [Fact]
    public async Task InitializeAsync_CreatesTriggers()
    {
        // Arrange
        var db = new McpDb(_fixture.DbPath);

        // Act
        await db.InitializeAsync();

        // Assert
        using var conn = new SqliteConnection(db.ConnectionString);
        await conn.OpenAsync();

        var triggers = await conn.QueryAsync<string>(
            "SELECT name FROM sqlite_master WHERE type='trigger'");

        var triggerList = triggers.ToList();
        Assert.Contains("trg_learning_notes_ai", triggerList);
        Assert.Contains("trg_learning_notes_ad", triggerList);
        Assert.Contains("trg_learning_notes_au", triggerList);
    }

    [Fact]
    public async Task InitializeAsync_IsIdempotent()
    {
        // Arrange
        var db = new McpDb(_fixture.DbPath);

        // Act
        await db.InitializeAsync();
        await db.InitializeAsync(); // Second call

        // Assert - should not throw
        var repo = new LearningNoteRepo(db.ConnectionString);
        var count = await repo.CountAsync();
        Assert.Equal(0, count);
    }
}
