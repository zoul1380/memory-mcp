# MCP Server Configuration for VS Code

## Setup for Cursor / Windsurf with MCP Support

### 1. Build the MCP Server

```bash
cd g:\code\memory-mcp
dotnet build MemoryMcp.Server -c Release
```

The output will be at:
```
g:\code\memory-mcp\MemoryMcp.Server\bin\Release\net8.0\MemoryMcp.Server.exe
```

### 2. Configure in VS Code / Cursor

Add to your **VS Code settings.json** (Ctrl+Shift+P → "Preferences: Open Settings (JSON)"):

```json
"mcpServers": {
  "memory": {
    "command": "dotnet",
    "args": [
      "g:\\code\\memory-mcp\\MemoryMcp.Server\\bin\\Release\\net8.0\\MemoryMcp.Server.dll"
    ]
  }
}
```

### 3. Restart VS Code

The MemoryMCP server is now available to Claude/Copilot.

## Available Tools

### 1. **add-learning**
Add a new learning note
```json
{
  "repoKey": "example-repo",
  "title": "Email reminder race condition",
  "problem": "Duplicate emails sent on retry after approval",
  "solution": "Check approval state before sending; add idempotency key",
  "rootCause": "Async in-flight message during state change",
  "appliesWhen": "When approval flow has async messaging",
  "confidence": "confirmed",
  "tags": ["email", "race-condition", "approval"],
  "links": [
    { "label": "PR", "url": "https://github.com/org/repo/pull/123" },
    { "label": "Jira", "url": "https://jira.org/browser/PROJ-456" }
  ]
}
```

### 2. **search-learning**
Search your learnings using full-text search
```json
{
  "query": "email reminder",
  "repoKey": "example-repo",
  "limit": 10
}
```

**Query syntax examples:**
- `reminder email` - both words
- `"exact phrase"` - quoted phrase must match exactly
- `word1 OR word2` - either word
- `word1 NEAR/3 word2` - words within 3 positions
- `email -duplicate` - include "email", exclude "duplicate"

### 3. **get-learning**
Get a specific learning by ID
```json
{
  "id": 42
}
```

### 4. **get-learning-meta**
Get tags and external links for a learning
```json
{
  "id": 42
}
```

### 5. **apply-instructions**
Generate Copilot instructions from search results
```json
{
  "query": "email reminder",
  "repoKey": "example-repo",
  "limit": 10
}
```

Returns:
- **instructions** - AI-ready snippets for quick reference
- **checklist** - Pre/post-change verification steps
- **gotchas** - Known issues grouped by confidence level

## Example Workflow

1. **Capture a learning:**
   ```
   @memory add-learning
   - Repo: example-repo
   - Title: API timeout on bulk imports
   - Problem: Bulk import times out after 30 seconds
   - Solution: Use streaming import with batches of 100
   - Confidence: confirmed
   - Tags: performance, imports, api
   ```

2. **Later, when working on imports again:**
   ```
   @memory apply-instructions
   - Query: bulk import timeout
   - Repo: example-repo
   ```

3. **Claude returns:** Copilot instructions, checklist, and known gotchas

## Database Location

The MCP server stores all learnings in:
- **Windows:** `%USERPROFILE%\.mcp\mcp.db`
- **macOS/Linux:** `~/.mcp/mcp.db`

The database is private to your machine and fully portable.

## Testing the Server Locally

```bash
# Build
dotnet build

# Run the server (will wait for JSON-RPC input)
dotnet run --project MemoryMcp.Server

# Send a test request (in another terminal):
echo '{"jsonrpc":"2.0","method":"initialize","id":1}' | dotnet run --project MemoryMcp.Server
```

## Troubleshooting

**"MCP server not available"** 
- Rebuild the solution
- Check the path in settings.json is correct
- Restart VS Code

**"Learning not found"**
- Ensure the database was initialized (happens on first connection)
- Check the learning ID is correct

**Search returns empty**
- Try simpler query terms
- Use `apply-instructions` to see what's stored
- Verify repo key matches what was used when adding the learning
