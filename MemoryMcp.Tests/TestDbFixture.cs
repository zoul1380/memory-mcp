namespace MemoryMcp.Tests;

public sealed class TestDbFixture : IDisposable
{
    private readonly string _dbPath;
    public string DbPath { get; }

    public TestDbFixture()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"mcp-test-{Guid.NewGuid()}.db");
        DbPath = _dbPath;
    }

    public string ConnectionString => new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
    {
        DataSource = _dbPath,
        ForeignKeys = true
    }.ToString();

    public void Dispose()
    {
        if (File.Exists(_dbPath))
        {
            try
            {
                File.Delete(_dbPath);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }
}
