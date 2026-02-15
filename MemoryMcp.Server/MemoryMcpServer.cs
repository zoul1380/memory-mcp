using System.Text.Json;
using MemoryMcp;
using MemoryMcp.Database;
using MemoryMcp.Models;
using MemoryMcp.Formatting;

namespace MemoryMcp.Server;

/// <summary>
/// MCP Server for MemoryMCP - exposes learning note management as tools
/// </summary>
public class MemoryMcpServer
{
    private readonly McpDb _db;
    private readonly LearningNoteRepo _repo;
    private readonly McpJsonRpcHandler _handler;
    private readonly string _dbPath;

    public MemoryMcpServer(string? customDbPath = null)
    {
        _dbPath = customDbPath ?? GetDefaultDbPath();
        _db = new McpDb(_dbPath);
        _handler = new McpJsonRpcHandler();
        _repo = new LearningNoteRepo(_db.ConnectionString);
    }

    public async Task InitializeAsync()
    {
        await _db.InitializeAsync();
    }

    public List<McpJsonRpcHandler.Tool> GetTools()
    {
        return new()
        {
            new McpJsonRpcHandler.Tool
            {
                Name = "add-learning",
                Description = "Add a new learning note with problem, solution, tags, and links",
                InputSchema = JsonSerializer.Deserialize<object>("""
                {
                  "type": "object",
                  "properties": {
                    "repoKey": { "type": "string", "description": "Repository identifier (e.g., 'example-repo')" },
                    "title": { "type": "string", "description": "Short title of the learning" },
                    "problem": { "type": "string", "description": "Problem description or symptoms" },
                    "solution": { "type": "string", "description": "Solution or steps to resolve" },
                    "rootCause": { "type": "string", "description": "Root cause (optional)" },
                    "appliesWhen": { "type": "string", "description": "When this applies (optional)" },
                    "confidence": { "type": "string", "enum": ["confirmed", "likely", "hypothesis"], "description": "Confidence level" },
                    "tags": { "type": "array", "items": { "type": "string" }, "description": "Tags for categorization" },
                    "links": { 
                      "type": "array", 
                      "items": { 
                        "type": "object",
                        "properties": {
                          "label": { "type": "string" },
                          "url": { "type": "string" }
                        }
                      },
                      "description": "External links (PR, Jira, docs, etc.)"
                    }
                  },
                  "required": ["repoKey", "title", "problem", "solution"]
                }
                """) ?? new { }
            },
            new McpJsonRpcHandler.Tool
            {
                Name = "search-learning",
                Description = "Search learning notes using full-text search",
                InputSchema = JsonSerializer.Deserialize<object>("""
                {
                  "type": "object",
                  "properties": {
                    "query": { "type": "string", "description": "Search query (supports FTS5 syntax: 'word1 OR word2', 'NEAR/3', quoted phrases)" },
                    "repoKey": { "type": "string", "description": "Filter by repository (optional, searches all if omitted)" },
                    "limit": { "type": "integer", "description": "Max results (default: 10)" }
                  },
                  "required": ["query"]
                }
                """) ?? new { }
            },
            new McpJsonRpcHandler.Tool
            {
                Name = "get-learning",
                Description = "Get a specific learning note by ID",
                InputSchema = JsonSerializer.Deserialize<object>("""
                {
                  "type": "object",
                  "properties": {
                    "id": { "type": "integer", "description": "Learning note ID" }
                  },
                  "required": ["id"]
                }
                """) ?? new { }
            },
            new McpJsonRpcHandler.Tool
            {
                Name = "get-learning-meta",
                Description = "Get tags and external links for a learning note",
                InputSchema = JsonSerializer.Deserialize<object>("""
                {
                  "type": "object",
                  "properties": {
                    "id": { "type": "integer", "description": "Learning note ID" }
                  },
                  "required": ["id"]
                }
                """) ?? new { }
            },
            new McpJsonRpcHandler.Tool
            {
                Name = "apply-instructions",
                Description = "Generate Copilot instructions from search results",
                InputSchema = JsonSerializer.Deserialize<object>("""
                {
                  "type": "object",
                  "properties": {
                    "query": { "type": "string", "description": "Search query" },
                    "repoKey": { "type": "string", "description": "Filter by repository (optional)" },
                    "limit": { "type": "integer", "description": "Max results (default: 10)" }
                  },
                  "required": ["query"]
                }
                """) ?? new { }
            }
        };
    }

    public async Task<McpJsonRpcHandler.JsonRpcResponse<object>> HandleToolCall(
        string toolName,
        JsonElement? parameters)
    {
        try
        {
            var result = toolName switch
            {
                "add-learning" => await HandleAddLearning(parameters),
                "search-learning" => await HandleSearchLearning(parameters),
                "get-learning" => await HandleGetLearning(parameters),
                "get-learning-meta" => await HandleGetMeta(parameters),
                "apply-instructions" => await HandleApplyInstructions(parameters),
                _ => throw new InvalidOperationException($"Unknown tool: {toolName}")
            };

            // Wrap result in MCP tool response format
            var jsonResult = System.Text.Json.JsonSerializer.Serialize(result);
            var toolResponse = new McpJsonRpcHandler.ToolCallResponse
            {
                Content = new List<McpJsonRpcHandler.ToolContent>
                {
                    new() { Type = "text", Text = jsonResult }
                }
            };

            return new McpJsonRpcHandler.JsonRpcResponse<object>
            {
                Result = toolResponse
            };
        }
        catch (Exception ex)
        {
            return new McpJsonRpcHandler.JsonRpcResponse<object>
            {
                Error = new McpJsonRpcHandler.JsonRpcError
                {
                    Code = -32603,
                    Message = "Internal error: " + ex.Message
                }
            };
        }
    }

    private async Task<object> HandleAddLearning(JsonElement? parameters)
    {
        if (!parameters.HasValue) throw new ArgumentException("Missing parameters");
        var p = parameters.Value;

        var repoKey = p.GetProperty("repoKey").GetString() ?? throw new ArgumentException("repoKey required");
        var title = p.GetProperty("title").GetString() ?? throw new ArgumentException("title required");
        var problem = p.GetProperty("problem").GetString() ?? throw new ArgumentException("problem required");
        var solution = p.GetProperty("solution").GetString() ?? throw new ArgumentException("solution required");
        var rootCause = p.TryGetProperty("rootCause", out var rcElem) ? rcElem.GetString() : null;
        var appliesWhen = p.TryGetProperty("appliesWhen", out var awElem) ? awElem.GetString() : null;
        var confidence = p.TryGetProperty("confidence", out var confElem) ? confElem.GetString() ?? "likely" : "likely";

        List<string>? tags = null;
        if (p.TryGetProperty("tags", out var tagsElem) && tagsElem.ValueKind != System.Text.Json.JsonValueKind.Null)
        {
            tags = tagsElem.EnumerateArray()
                .Select(x => x.GetString() ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();
        }

        List<(string label, string url)>? links = null;
        if (p.TryGetProperty("links", out var linksElem) && linksElem.ValueKind != System.Text.Json.JsonValueKind.Null)
        {
            links = linksElem.EnumerateArray()
                .Select(x => (x.GetProperty("label").GetString() ?? "Link", x.GetProperty("url").GetString() ?? ""))
                .Where(x => !string.IsNullOrWhiteSpace(x.Item2))
                .ToList();
        }

        var input = new LearningNoteInput(repoKey, title, problem, solution, rootCause, appliesWhen, confidence, tags, links);
        var id = await _repo.AddLearningAsync(input);

        return new { id, message = $"Learning note created: {title}" };
    }

    private async Task<object> HandleSearchLearning(JsonElement? parameters)
    {
        if (!parameters.HasValue) throw new ArgumentException("Missing parameters");
        var p = parameters.Value;

        var query = p.GetProperty("query").GetString() ?? throw new ArgumentException("query required");
        var repoKey = p.TryGetProperty("repoKey", out var rkElem) ? rkElem.GetString() : null;
        var limit = p.TryGetProperty("limit", out var limitElem) ? limitElem.GetInt32() : 10;

        var results = await _repo.SearchAsync(query, repoKey, limit);

        return new
        {
            count = results.Count,
            results = results.Select(r => new
            {
                r.Id,
                r.RepoKey,
                r.Title,
                r.Problem,
                r.Solution,
                r.RootCause,
                r.AppliesWhen,
                r.Confidence,
                r.CreatedAt,
                r.UpdatedAt
            }).ToList()
        };
    }

    private async Task<object> HandleGetLearning(JsonElement? parameters)
    {
        if (!parameters.HasValue) throw new ArgumentException("Missing parameters");
        var p = parameters.Value;

        var id = p.GetProperty("id").GetInt64();
        var result = await _repo.GetByIdAsync(id);

        if (result == null)
            throw new InvalidOperationException($"Learning note {id} not found");

        return new
        {
            result.Id,
            result.RepoKey,
            result.Title,
            result.Problem,
            result.Solution,
            result.RootCause,
            result.AppliesWhen,
            result.Confidence,
            result.CreatedAt,
            result.UpdatedAt
        };
    }

    private async Task<object> HandleGetMeta(JsonElement? parameters)
    {
        if (!parameters.HasValue) throw new ArgumentException("Missing parameters");
        var p = parameters.Value;

        var id = p.GetProperty("id").GetInt64();
        var (tags, links) = await _repo.GetMetaAsync(id);

        return new
        {
            id,
            tags = tags.ToList(),
            links = links.Select(x => new { x.Label, x.Url }).ToList()
        };
    }

    private async Task<object> HandleApplyInstructions(JsonElement? parameters)
    {
        if (!parameters.HasValue) throw new ArgumentException("Missing parameters");
        var p = parameters.Value;

        var query = p.GetProperty("query").GetString() ?? throw new ArgumentException("query required");
        var repoKey = p.TryGetProperty("repoKey", out var rkElem) ? rkElem.GetString() : null;
        var limit = p.TryGetProperty("limit", out var limitElem) ? limitElem.GetInt32() : 10;

        var results = await _repo.SearchAsync(query, repoKey, limit);

        var instructions = ApplyFormatter.BuildCopilotInstructions(results);
        var checklist = ApplyFormatter.BuildChecklist(results);
        var gotchas = ApplyFormatter.BuildGotchasSummary(results);

        return new
        {
            query,
            repoKey = repoKey ?? "(all repos)",
            matchCount = results.Count,
            instructions,
            checklist,
            gotchas
        };
    }

    private static string GetDefaultDbPath()
    {
        var userProfile = Environment.GetEnvironmentVariable("USERPROFILE") ?? Environment.GetEnvironmentVariable("HOME") ?? ".";
        return Path.Combine(userProfile, ".mcp", "mcp.db");
    }
}
