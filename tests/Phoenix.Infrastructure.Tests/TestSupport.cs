using Phoenix.Core.Auditing;
using Phoenix.Infrastructure.Auditing;
using Phoenix.Infrastructure.Data;
using Phoenix.Infrastructure.Students;

namespace Phoenix.Infrastructure.Tests;

/// <summary>Shared builders for services that need the ADR-004 audit trail.</summary>
internal static class TestSupport
{
    public static readonly AuditActor TestActor = new("00000000-0000-0000-0000-0000000000aa", "tester@phoenixsis.test");

    public static AuditLog Audit(PhoenixDbContext db, ICurrentUser? user = null) =>
        new(db, user ?? new FakeCurrentUser(TestActor));

    public static StudentIdentityService Identity(PhoenixDbContext db, ICurrentUser? user = null) =>
        new(db, Audit(db, user));
}

internal sealed class FakeCurrentUser(AuditActor actor) : ICurrentUser
{
    public ValueTask<AuditActor> GetActorAsync(CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(actor);
}

/// <summary>An actor source that always fails - proves ADR-004 fail closed.</summary>
internal sealed class FailingCurrentUser : ICurrentUser
{
    public ValueTask<AuditActor> GetActorAsync(CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Audit store unavailable.");
}
