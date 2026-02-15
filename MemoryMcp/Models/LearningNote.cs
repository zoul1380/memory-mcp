namespace MemoryMcp.Models;

public sealed record LearningNote(
    long Id,
    string RepoKey,
    string Title,
    string Problem,
    string Solution,
    string? RootCause,
    string? AppliesWhen,
    string Confidence,
    string CreatedAt,
    string UpdatedAt,
    string? LastVerifiedAt);

public sealed record LearningNoteInput(
    string RepoKey,
    string Title,
    string Problem,
    string Solution,
    string? RootCause = null,
    string? AppliesWhen = null,
    string Confidence = "likely",
    IEnumerable<string>? Tags = null,
    IEnumerable<(string label, string url)>? Links = null);
