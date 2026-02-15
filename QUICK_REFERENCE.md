# MemoryMCP - Quick Reference Card

## ⚡ 60-Second Setup

### 1. Build the Server
```bash
dotnet build MemoryMcp.Server -c Release
```

### 2. Configure VS Code
Settings → JSON → Add:
```json
"mcpServers": {
  "memory": {
    "command": "dotnet",
    "args": ["G:\\code\\memory-mcp\\MemoryMcp.Server\\bin\\Release\\net9.0\\MemoryMcp.Server.dll"]
  }
}
```

### 3. Restart VS Code ✓

---

## 🎯 Quick Commands

### Save a Learning
```
@memory add-learning

Problem: What went wrong?
Solution: How to fix it
Repo: project-name
Tags: tag1, tag2
Confidence: confirmed|likely|hypothesis
```

### Find a Learning
```
@memory search-learning
Query: what are you looking for?
Repo: project-name (optional)
```

### Get Help
```
@memory apply-instructions
Query: what are you working on?
```

---

## 📝 Full Tool Reference

| Tool | Purpose | Required Params |
|------|---------|-----------------|
| **add-learning** | Save a learning | repoKey, title, problem, solution |
| **search-learning** | Find learnings | query |
| **get-learning** | Details on one | id |
| **get-learning-meta** | Tags & links | id |
| **apply-instructions** | AI-ready output | query |

---

## 💾 Database

- **Location:** `%USERPROFILE%\.mcp\mcp.db`
- **Type:** SQLite
- **Backup:** Copy `.mcp\mcp.db` to another location
- **Import:** Copy `mcp.db` back to `.mcp\mcp.db`

---

## 🔍 Search Examples

```
email reminder                    # Both words
"exact phrase" words              # Quoted phrase exact match
word1 OR word2                    # Either word
word1 NEAR/5 word2                # Words within 5 positions
email -duplicate                  # Include "email", exclude "duplicate"
```

---

## 🆘 Troubleshooting

| Issue | Solution |
|-------|----------|
| "Tool not found" | Rebuild: `dotnet build MemoryMcp.Server -c Release` |
| "Learning not found" | Check ID is correct or search for it first |
| Search empty results | Try simpler terms or search all repos |
| Server won't start | Ensure `.mcp` folder exists: `mkdir %USERPROFILE%\.mcp` |

---

## 📱 Example Workflow

### You just fixed a bug in approval notifications

**Step 1: Save it**
```
@memory add-learning

Repo: example-repo
Title: Email sent after approval already completed
Problem: Users get duplicate approval notifications
Solution: Check approval state before sending; add idempotency check
RootCause: Async message in-flight when state changed
Confidence: confirmed
Tags: email, notifications, approval, race-condition
Links: [PR https://github.com/org/repo/pull/123]
```

### Later: Working on a similar feature

**Step 2: Apply the learning**
```
@memory apply-instructions
Query: approval notification email
Repo: example-repo
```

**Claude returns:**
```
✓ Copilot Instructions
  - Check state before sending notifications
  - Use idempotency keys for async messages
  - Test with concurrent approvals

✓ Checklist
  Before submitting:
  - [ ] Verify state hasn't changed since queue
  - [ ] Test concurrent approval scenarios
  - [ ] Check no duplicate records created

✓ Gotchas (Confirmed)
  - Async messages can arrive late after state change
  - Race conditions during high load
```

---

## 🚀 Full Docs

- **Setup Guide:** `README_MCP.md`
- **MCP Details:** `MCP_IMPLEMENTATION_SUMMARY.md`
- **Specification:** `MemoryMCP.md`
- **Quick Start:** `QUICKSTART.md`

---

**Ready to go!** Start capturing learnings and let Claude remember your solutions. 🧠✨
