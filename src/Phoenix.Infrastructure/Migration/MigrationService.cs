using Phoenix.Core.DataMigration;
using Phoenix.Core.Students;

namespace Phoenix.Infrastructure.DataMigration;

/// <summary>
/// See <see cref="IMigrationService"/>. A record is matched to a student by
/// its primary id only. Its extra identifiers (PHX-9.1) are then added as
/// mapped rows under "&lt;Source&gt;:&lt;Kind&gt;": one already mapped to the
/// same student is left alone, one mapped to a different student is reported
/// as a conflict and never moved, a blank one is ignored. Each added
/// identifier is audited by the identity service.
/// </summary>
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
            var student = await _identity.GetByIdentifierAsync(
                source.SourceSystem, record.ExternalId, cancellationToken);

            MigrationAction action;
            if (student is not null)
            {
                action = MigrationAction.Matched;
            }
            else
            {
                action = MigrationAction.Created;
                if (apply)
                {
                    student = await _identity.ResolveOrCreateAsync(
                        source.SourceSystem,
                        record.ExternalId,
                        new StudentDetails(
                            record.FirstName, record.MiddleName, record.LastName, record.BirthDate),
                        cancellationToken);
                }
            }

            var added = 0;
            var conflicts = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var identifier in record.ExtraIdentifiers)
            {
                if (string.IsNullOrWhiteSpace(identifier.Kind) || string.IsNullOrWhiteSpace(identifier.Value))
                    continue;

                var kind = identifier.Kind.Trim();
                if (!seen.Add(kind))
                    continue; // one value per kind per record; the first wins

                var value = identifier.Value.Trim();
                var system = SourceIdentifier.SourceSystemFor(source.SourceSystem, kind);
                var owner = await _identity.GetByIdentifierAsync(system, value, cancellationToken);

                if (owner is not null)
                {
                    // In a dry run a record that would create its student has
                    // no student yet, so any existing owner is someone else.
                    if (student is null || owner.Id != student.Id)
                        conflicts.Add(kind);
                    continue;
                }

                if (apply && student is not null)
                    await _identity.AddIdentifierAsync(student.Id, system, value, cancellationToken);

                added++;
            }

            rows.Add(new MigrationRowResult(
                record.ExternalId,
                $"{record.FirstName} {record.LastName}",
                action,
                added,
                conflicts));
        }

        return new MigrationResult(source.SourceSystem, apply, rows);
    }
}
