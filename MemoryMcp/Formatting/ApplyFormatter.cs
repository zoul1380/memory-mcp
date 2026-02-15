using MemoryMcp.Models;

namespace MemoryMcp.Formatting;

public static class ApplyFormatter
{
    public static string BuildCopilotInstructions(IEnumerable<LearningNote> notes)
    {
        var notesList = notes.ToList();
        if (notesList.Count == 0)
            return "### Copilot Instructions (from MCP learnings)\nNo relevant learnings found.";

        var lines = new List<string>
        {
            "### Copilot Instructions (from MCP learnings)",
            "- Use repo conventions and existing patterns first.",
            "- Prefer small, low-risk changes; add logging when behavior changes.",
            ""
        };

        foreach (var n in notesList)
        {
            lines.Add($"- [{n.RepoKey}] {n.Title}: {Compact(n.Solution, 140)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildChecklist(IEnumerable<LearningNote> notes)
    {
        var notesList = notes.ToList();
        if (notesList.Count == 0)
            return "### Checklist (from MCP learnings)\nNo relevant learnings found.";

        var lines = new List<string>
        {
            "### Checklist (from MCP learnings)",
            ""
        };

        foreach (var n in notesList)
        {
            lines.Add($"- [ ] {n.Title}");
            if (!string.IsNullOrWhiteSpace(n.AppliesWhen))
                lines.Add($"  - When: {Compact(n.AppliesWhen, 100)}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    public static string BuildGotchasSummary(IEnumerable<LearningNote> notes)
    {
        var notesList = notes.ToList();
        if (notesList.Count == 0)
            return "### Known Gotchas\nNone found.";

        var lines = new List<string>
        {
            "### Known Gotchas (from MCP learnings)",
            ""
        };

        var confirmed = notesList.Where(n => n.Confidence == "confirmed").ToList();
        if (confirmed.Count > 0)
        {
            lines.Add("**Confirmed issues:**");
            foreach (var n in confirmed)
            {
                lines.Add($"- {n.Title}: {Compact(n.Problem, 120)}");
            }
            lines.Add("");
        }

        var other = notesList.Where(n => n.Confidence != "confirmed").ToList();
        if (other.Count > 0)
        {
            lines.Add("**Other observations:**");
            foreach (var n in other)
            {
                lines.Add($"- {n.Title} ({n.Confidence})");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Compact(string? text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return t.Length <= max ? t : t.Substring(0, max).Trim() + "...";
    }
}
