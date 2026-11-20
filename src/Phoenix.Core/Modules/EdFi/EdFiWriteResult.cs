namespace Phoenix.Core.Modules.EdFi;

/// <summary>
/// Outcome of one Ed-Fi write. Ed-Fi POST is upsert by natural key:
/// 201 = created, 200 = an existing record was updated. Anything else is a
/// failure with the parsed <see cref="Problem"/> when the API sent one.
/// </summary>
public sealed record EdFiWriteResult(
    string Resource,
    bool Success,
    int StatusCode,
    int Attempts,
    EdFiProblem? Problem)
{
    /// <summary>True when the API created a new record (HTTP 201).</summary>
    public bool Created => StatusCode == 201;
}
