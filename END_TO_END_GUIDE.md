# End-to-End Guide: Using MemoryMCP with Claude in VS Code

## Overview

This guide shows you how to use MemoryMCP to capture, search, and apply learnings in your day-to-day coding work with Claude in VS Code.

## Part 1: Initial Setup (One-time)

### Step 1.1: Build the Server
```bash
cd g:\code\memory-mcp
dotnet build MemoryMcp.Server -c Release
```

**Output location:**
```
G:\code\memory-mcp\MemoryMcp.Server\bin\Release\net8.0\MemoryMcp.Server.dll
```

### Step 1.2: Configure VS Code

1. Open Command Palette: `Ctrl+Shift+P`
2. Search for: "Preferences: Open Settings (JSON)"
3. Add this to your `settings.json`:

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

4. Save and restart VS Code

### Step 1.3: Verify Setup
- After restarting, open a new Claude session in VS Code
- You should see a "memory" option in the MCP tools list

✓ Setup complete!

---

## Part 2: Real-World Scenario

### Scenario: Debugging an Email Race Condition

#### Day 1: You discover and fix a bug

**Your situation:**
You're debugging the "approval notifications" feature. You discover that sometimes users get duplicate email notifications when they click "approve" twice quickly. After investigation, you find the root cause: an async message is already in flight when the approval state changes.

**Step 2.1: Capture This Learning**

In VS Code, open Claude chat and use:

```
@memory add-learning

I just debugged an important issue and want to save it for the team.

Repo: example-repo
Title: Duplicate approval notification emails on rapid clicks
Problem: Users receive duplicate notifications when clicking approve multiple times or refreshing after clicking once. This happens on retry or page refresh.
Solution: Before sending the approval notification email, query the approval state. If already approved, skip sending. Also add an idempotency key to the email message.
RootCause: Async approval message is in-flight and pending when user clicks again or refreshes. The approval handler processes even though state already changed.
AppliesWhen: In systems with async approval messaging, especially when users might retry or refresh the approval page
Confidence: confirmed
Tags: email, notifications, race-condition, async-messaging, approval-flow
Links:
  - PR: https://github.com/company/monolith/pull/4521
  - Jira: https://jira.company.org/browse/PROJ-7834
  - Slack: https://company.slack.com/archives/C123456/p1707835420001200
```

Claude executes the tool and confirms:
```
✓ Learning note created (ID: 42)
  Title: Duplicate approval notification emails on rapid clicks
```

**What just happened:**
- The learning is now stored in your database
- It's searchable and applies to the repo "example-repo"
- The ID is 42 (you can reference this later)
- It's marked as "confirmed" so Claude will prioritize it

---

#### Day 8: Working on a similar feature

**Your situation:**
You're now working on "order confirmation emails" in the same repo. Before you start, you want advice on async notification flows to avoid similar bugs.

**Step 2.2: Apply the Learning**

```
@memory apply-instructions
Query: async notification race condition
Repo: example-repo
```

Claude searches and returns:

```
### Copilot Instructions
Based on your learnings in example-repo:

- [example-repo] Duplicate approval notifications: Before sending email, 
  check approval state hasn't changed. Add idempotency key to message.
  
- [example-repo] Test async flows with rapid user retries to catch race 
  conditions early.

### Checklist (Pre-submission verification)
- [ ] Verify state check happens before sending notification
- [ ] Add idempotency key to async message payload
- [ ] Test with rapid/concurrent requests to same notification
- [ ] Verify no duplicate records created in notification log
- [ ] Check error handling when state changes mid-flight

### Gotchas (by confidence level)
Confirmed: Async messages can arrive after state change - always check
         state before side effects
Likely:   Page refreshes can trigger retries of in-flight messages
         Consider timeouts for pending operations
```

**What just happened:**
- Claude searched your learnings for "async notification race condition"
- Found your earlier learning about approval emails
- Generated a checklist specific to your current task
- Warned you about confirmed and likely gotchas

You follow the checklist and avoid the same bug! ✓

---

#### Day 15: Reviewing Someone Else's PR

**Your situation:**
A teammate submits a PR for "payment reminder notifications". You want to check if your learnings about async issues apply.

**Step 2.3: Smart Code Review**

In the PR comments, you write:

```
@memory search-learning
Query: notification async race condition
```

Claude returns:

```
Found 2 learnings:

1. ID: 42 - Duplicate approval notification emails on rapid clicks
   Repo: example-repo
   Confidence: confirmed
   Problem: Async messages in-flight when state changes
   Solution: Check state before sending, add idempotency key

2. ID: 38 - Order email sent twice in checkout flow
   Repo: e-commerce
   Confidence: likely
   Problem: Retry logic in task queue sends duplicate orders
   Solution: Check order state in idempotent handler
```

You can now reference these in your PR comments:

```
Good work on the payment reminder feature! A few notes from prior 
learnings (#42, #38):

1. Make sure to check payment status BEFORE sending the reminder 
   (not after queueing)
2. Add an idempotency key to the message
3. Test with concurrent payment attempts to catch race conditions

See #42 for how we fixed similar issue in approval flow.
```

Your teammate reviews the learning and implements the safeguards. ✓

---

## Part 3: Advanced Usage

### Get Details on a Specific Learning

```
@memory get-learning
ID: 42
```

Returns:
```
ID: 42
RepoKey: example-repo
Title: Duplicate approval notification emails on rapid clicks
Problem: Users receive duplicate notifications when clicking approve multiple times
Solution: Check approval state before sending; add idempotency key
RootCause: Async message in-flight when user retries
AppliesWhen: Async approval messaging workflows
Confidence: confirmed
CreatedAt: 2024-02-15T14:32:00Z
UpdatedAt: 2024-02-15T14:32:00Z
```

### Get Tags and Links for a Learning

```
@memory get-learning-meta
ID: 42
```

Returns:
```
Tags: [email, notifications, race-condition, async-messaging, approval-flow]
Links:
  - PR: https://github.com/company/monolith/pull/4521
  - Jira: https://jira.company.org/browse/PROJ-7834
```

### Search with Advanced Queries

```
@memory search-learning
Query: email NEAR/5 async
Repo: example-repo
Limit: 5
```

Finds notes where "email" and "async" are within 5 words of each other.

---

## Part 4: Organization Tips

### Use Tags Strategically

Good tag taxonomy:
```
By Feature:
  - email, notifications, payments, approvals, auth

By Pattern:
  - race-condition, memory-leak, timeout, parsing-error

By Severity:
  - security, critical, performance, nice-to-have

By Confidence:
  - confirmed, likely, hypothesis (use in searching)
```

### Query Examples

```
# Find all confirmed security issues
@memory search-learning
Query: confirmed AND security

# Find performance problems in payment flow
@memory search-learning
Query: payment NEAR/3 performance
Repo: payment-service

# Find current unknowns to investigate
@memory search-learning
Query: hypothesis

# Find likely gotchas when deploying
@memory search-learning
Query: deployment
Limit: 20
```

---

## Part 5: Team Workflows

### Share Learning IDs
Instead of explaining the issue again, just reference:
```
See learning #42 for how we fixed this in the approval flow
```

Teammates can:
```
@memory get-learning
ID: 42
```

### Capture Learnings from Communications

When someone shares important context in Slack:
```
@memory add-learning
RepoKey: my-app
Title: Database migration required for new feature
Problem: Old schema doesn't support new payment types
Solution: Run migration script before deploying
Links:
  - Slack: https://slack.com/archives/C123456/p170834234234234
  - Confluence: https://company.atlassian.net/wiki/spaces/ENG/pages/12345
```

### Periodic Review
Once a month:
```
@memory search-learning
Query: hypothesis
Repo: my-team-repo
```

Verify if hypotheses became confirmed learnings or should be updated.

---

## Part 6: Backup & Recovery

### Backup Your Learnings
The database is stored at:
```
%USERPROFILE%\.mcp\mcp.db
```

Backup it regularly:
```bash
# Create backup
Copy-Item "$env:USERPROFILE\.mcp\mcp.db" "$env:USERPROFILE\.mcp\mcp.db.backup.$(Get-Date -Format 'yyyy-MM-dd')"

# Restore from backup
Copy-Item "$env:USERPROFILE\.mcp\mcp.db.backup.2024-02-15" "$env:USERPROFILE\.mcp\mcp.db"
```

### Export to Markdown
You can query all learnings:
```
@memory search-learning
Query: *  (searches everything)
Limit: 1000
```

Then copy into a markdown file for backup.

---

## Common Patterns

### Learning Template (Copy & Paste)
```
@memory add-learning
Repo: [project-name]
Title: [Short title]
Problem: [What went wrong]
Solution: [How to fix it]
RootCause: [Why it happened - optional]
AppliesWhen: [Situation where this applies - optional]
Confidence: confirmed|likely|hypothesis
Tags: [tag1, tag2, tag3]
Links:
  - [type]: [url]
```

### Review Checklist Template
```
Before submitting this PR:

@memory apply-instructions
Query: [related feature/problem]
Repo: [this repo]

Then verify:
- [ ] Addressed all Competent-level gotchas
- [ ] Accounted for Likely issues
- [ ] Added checklist items to PR description
```

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| "Tool not found in MCP menu" | Restart VS Code after updating settings.json |
| "Learning not saving" | Check database path `%USERPROFILE%\.mcp\mcp.db` exists |
| "Search returns 0 results" | Try with fewer/simpler search terms |
| "Want to delete a learning" | Not yet supported; consider marking as deprecated in title |
| "Need to update a learning" | Not yet supported; add new one with updated info |

---

## What's Next?

**Use MemoryMCP to:**
1. ✓ Capture bugs you've fixed
2. ✓ Document gotchas you've learned
3. ✓ Share deployment steps
4. ✓ Reference solutions in code reviews
5. ✓ Build team knowledge base

**Future features** (not yet implemented):
- Update/edit existing learnings
- Delete learnings
- Team-wide sync
- Web UI for browsing
- Automatic tag suggestions
- Search analytics

---

**You're all set!** Start capturing learnings and let Claude help you remember solutions. 🚀
