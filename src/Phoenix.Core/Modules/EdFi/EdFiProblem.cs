namespace Phoenix.Core.Modules.EdFi;

/// <summary>
/// An Ed-Fi ODS/API error response (RFC 7807 Problem Details, as ODS/API
/// 7.3 returns it - served as application/json). Shapes observed live:
/// a 400 carries <see cref="ValidationErrors"/> keyed by JSON path
/// ("$.firstName"); a 401/403 carries <see cref="Errors"/>; a 409 carries
/// only type/title/detail (for example
/// urn:ed-fi:api:data-conflict:unresolved-reference).
/// </summary>
public sealed record EdFiProblem(
    string? Type,
    string? Title,
    int? Status,
    string? Detail,
    string? CorrelationId,
    IReadOnlyList<string> Errors,
    IReadOnlyDictionary<string, IReadOnlyList<string>> ValidationErrors)
{
    /// <summary>
    /// One line for logs and the sync report: title, status, detail, then
    /// every validation and error message. Contains no request data.
    /// </summary>
    public string Describe()
    {
        var parts = new List<string>();

        var head = Title ?? Type ?? "Ed-Fi error";
        parts.Add(Status is null ? head : $"{head} ({Status})");

        if (!string.IsNullOrWhiteSpace(Detail))
            parts.Add(Detail!);

        foreach (var (field, messages) in ValidationErrors)
            parts.Add($"{field}: {string.Join(" ", messages)}");

        parts.AddRange(Errors);

        return string.Join(" | ", parts);
    }
}
