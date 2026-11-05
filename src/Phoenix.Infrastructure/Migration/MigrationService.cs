using Phoenix.Core.DataMigration;
using Phoenix.Core.Students;

namespace Phoenix.Infrastructure.DataMigration;

public sealed class MigrationService : IMigrationService
{
    private readonly IStudentIdentityService _identity;

    public MigrationService(IStudentIdentityService identity) => _identity = identity;

    public async Task<MigrationResult> RunAsync(
        ISisSourceConnector source, bool apply, CancellationToken cancellationToken = default)
    {
        var records = await source.ReadStudentsAsync(cancellationToken);
        var rows = new List<MigrationRowResult>(records.Count);

        foreach (var record in records)
        {
            var existing = await _identity.GetByIdentifierAsync(
                source.SourceSystem, record.ExternalId, cancellationToken);

            MigrationAction action;
            if (existing is not null)
            {
                action = MigrationAction.Matched;
            }
            else
            {
                action = MigrationAction.Created;
                if (apply)
                {
                    await _identity.ResolveOrCreateAsync(
                        source.SourceSystem,
                        record.ExternalId,
                        new StudentDetails(
                            record.FirstName, record.MiddleName, record.LastName, record.BirthDate),
                        cancellationToken);
                }
            }

            rows.Add(new MigrationRowResult(
                record.ExternalId,
                $"{record.FirstName} {record.LastName}",
                action));
        }

        return new MigrationResult(source.SourceSystem, apply, rows);
    }
}
