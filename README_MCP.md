# MemoryMCP - MCP Server for VS Code Integration

## What This Does

MemoryMCP is a **Model Context Protocol (MCP) server** that exposes your learning memory system as tools available to Claude/Copilot in VS Code. This lets you:

- **Store learnings** from debugging sessions, code reviews, deployments
- **Search** these learnings using fast full-text search
- **Apply** relevant learnings to your current work automatically
- **Generate** AI-ready instructions, checklists, and gotcha lists

## Quick Start

### 1. Build the Server

```bash
cd g:\code\memory-mcp
dotnet build MemoryMcp.Server -c Release
```

The executable will be at:
```
G:\code\memory-mcp\MemoryMcp.Server\bin\Release\net8.0\MemoryMcp.Server.dll
```

### 2. Configure VS Code

Edit your VS Code settings.json (Ctrl+Shift+P → "Preferences: Open Settings (JSON)"):

```json
{
  "mcpServers": {
    "memory": {
      "command": "dotnet",
      "args": [
        "G:\\code\\memory-mcp\\MemoryMcp.Server\\bin\\Release\\net8.0\\MemoryMcp.Server.dll"
      ]
    }
  }
}
```

Or copy `vscode-settings-example.json` to your settings.

### 3. Restart VS Code

Done! The memory tools are now available to Claude when you use the MCP menu.

## Available Tools

### 1. `add-learning`
**Add a new learning note to your memory**

```
@memory add-learning
Repo: example-repo
Title: Email reminder race condition
Problem: Duplicate emails sent when user clicks resend after approval completes
Solution: Check approval state before sending reminder; skip if already completed; add idempotency token
Root Cause: Async message still in-flight when approval state changed
Applies When: Async messaging with approval workflows
Confidence: confirmed
Tags: email, race-condition, approval
Links:
  - PR: https://github.com/org/repo/pull/789
  - Jira: https://jira.org/browse/PROJ-456
```

**Parameters:**
- `repoKey` (required) - Repository identifier (e.g., "example-repo")
- `title` (required) - Short title
- `problem` (required) - Problem description or symptoms
- `solution` (required) - Solution steps
- `rootCause` (optional) - Root cause analysis
- `appliesWhen` (optional) - When this applies
- `confidence` (optional) - confirmed|likely|hypothesis (default: likely)
- `tags` (optional) - Array of tags for categorization
- `links` (optional) - Array of {label, url} for external references

**Returns:** Learning note ID and confirmation

---

### 2. `search-learning`
**Search your learnings with full-text search**

```
@memory search-learning
Query: reminder email race condition
Repo: example-repo
Limit: 10
```

**Query Syntax (FTS5):**
- `reminder email` - Both words (AND)
- `"exact phrase"` - Quoted phrase must match
- `word1 OR word2` - Either word
- `word1 NEAR/3 word2` - Words within 3 positions
- `email -duplicate` - Include email, exclude duplicate

**Parameters:**
- `query` (required) - Search query
- `repoKey` (optional) - Filter by repo (searches all if omitted)
- `limit` (optional) - Max results (default: 10)

**Returns:** List of matching learning notes with full content

---

### 3. `get-learning`
**Retrieve a specific learning note by ID**

```
@memory get-learning
ID: 42
```

**Parameters:**
- `id` (required) - Learning note ID

**Returns:** Full learning note details

---

### 4. `get-learning-meta`
**Get tags and external links for a learning**

```
@memory get-learning-meta
ID: 42
```

**Parameters:**
- `id` (required) - Learning note ID

**Returns:** Tags and links associated with the note

---

### 5. `apply-instructions`
**Generate Copilot instructions from search results**

```
@memory apply-instructions
Query: bulk import timeout
Repo: example-repo
Limit: 10
```

This searches your learnings and returns three formatted outputs:

1. **Instructions** - AI-ready snippets for quick reference
2. **Checklist** - Pre/post-change verification steps
3. **Gotchas** - Known issues grouped by confidence (confirmed/likely/hypothesis)

**Parameters:**
- `query` (required) - Search query
- `repoKey` (optional) - Filter by repo
- `limit` (optional) - Max results (default: 10)

**Returns:** Formatted instructions, checklist, and gotchas summary

---

## Example Workflow

### Scenario: You just fixed a tricky email bug

**Step 1: Capture the learning**
```
@memory add-learning
Repo: example-repo
Title: Email race condition in approval reminders
Problem: Reminder emails sent after approval already completed
Solution: Add state check before sending reminder payload
Confidence: confirmed
Tags: email, race-condition
```
→ Returns: `Learning note 42 created`

### Step 2: Later, working on a similar feature

```
@memory apply-instructions
Query: approval reminder duplicate
Repo: example-repo
```

→ Claude returns:
```
### Copilot Instructions
- [example-repo] Email race condition: Check state before sending; skip if already approved
- [example-repo] Use idempotency tokens for async messaging

### Checklist
Before submitting:
- [ ] Verify approval state hasn't changed since message queued
- [ ] Test with concurrent approval attempts
- [ ] Confirm no duplicate records created

### Gotchas
Confirmed: Async messages can arrive after state change
Likely: Race conditions when queue is backed up
```

## Database Location

All learnings are stored in a local SQLite database:

- **Windows:** `%USERPROFILE%\.mcp\mcp.db`
- **macOS/Linux:** `~/.mcp/mcp.db`

The database is:
- **Local only** - No cloud sync
- **Portable** - Backup/move the `.mcp` folder
- **Fast** - Indexes and FTS5 for instant search

## Architecture

```
┌─────────────────────────────────────┐
│  VS Code + Claude/Copilot           │
│  (MCP Client)                       │
└────────────────┬────────────────────┘
                 │ JSON-RPC 2.0
                 │ (stdio)
┌────────────────▼────────────────────┐
│  MemoryMCP Server                   │
│  - Tool handlers                    │
│  - Request/response parsing         │
│  - MCP protocol implementation      │
└────────────────┬────────────────────┘
                 │ SQL/Dapper
┌────────────────▼────────────────────┐
│  SQLite Database                    │
│  - FTS5 full-text search            │
│  - Triggers for sync                │
│  - Indexed for performance          │
└─────────────────────────────────────┘
```

## Troubleshooting

### "Tool not found" in VS Code
1. Rebuild the server: `dotnet build MemoryMcp.Server -c Release`
2. Update your settings.json with the correct path
3. Restart VS Code

### "Learning note not found"
- Ensure you're using the correct learning ID
- Check that the database initialized correctly (first startup creates schema)

### Search returns no results
- Try different query terms (case-insensitive)
- Verify the repo key matches (use exact name when adding)
- Search without repo filter to check globally

### Server crashes on startup
- Ensure the `.mcp` directory exists and is writable
- Check the database isn't corrupted (delete and rebuild)
- Review server logs in VS Code output panel

## Development

### Run Tests
```bash
dotnet test MemoryMcp.sln
# 46 tests covering database, repository, formatting, and integration
```

### Debug the Server
The server writes diagnostic info to stderr:
```bash
dotnet run --project MemoryMcp.Server
# Watch the output panel in VS Code for debug messages
```

### Modify Server Behavior
Edit `MemoryMcp.Server/MemoryMcpServer.cs`:
- `GetTools()` - Add/remove/modify tools
- `HandleXxx()` - Change tool behavior
- `Program.cs` - Modify request routing

## Implementation Notes

### Protocol
- Uses **JSON-RPC 2.0** over stdio
- All requests must include `jsonrpc`, `method`, and optional `params`/`id`
- Responses include `result`, `error`, or both with matching `id`

### Tools vs Resources
- Tools (implemented) - Compute/action endpoints
- Resources (future) - Read-only data served to Claude

### Performance
- FTS5 indexing makes searches instant (even with 10k+ learnings)
- Indexed queries on repo_key, confidence, updated_at
- Transaction-safe operations for data integrity

## Future Enhancements

- [ ] Auto-import learnings from PR descriptions
- [ ] Team-wide learning sync with permissions
- [ ] Session history and rollback
- [ ] Web UI for managing learnings
- [ ] Export to markdown per repo
- [ ] Learn from conversation history
- [ ] Confidence auto-adjust based on verification

---

**Ready to use.** Start capturing learnings and let Claude help you remember solutions!
