using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Phoenix.Core.Auditing;

namespace Phoenix.Web.Security;

/// <summary>
/// ADR-004 actor from the Blazor authentication state (scoped per circuit /
/// request). Uses the Entra object id and preferred_username claims. A
/// request with no signed-in user is recorded as system:unauthenticated -
/// every Phoenix page requires sign-in, so that should never appear.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private const string ObjectIdClaim = "http://schemas.microsoft.com/identity/claims/objectidentifier";

    private readonly AuthenticationStateProvider _authenticationState;

    public CurrentUser(AuthenticationStateProvider authenticationState) =>
        _authenticationState = authenticationState;

    public async ValueTask<AuditActor> GetActorAsync(CancellationToken cancellationToken = default)
    {
        var state = await _authenticationState.GetAuthenticationStateAsync();
        var user = state.User;

        if (user.Identity?.IsAuthenticated != true)
            return AuditActor.System("unauthenticated");

        var id = user.FindFirstValue(ObjectIdClaim)
            ?? user.FindFirstValue("oid")
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        var name = user.FindFirstValue("preferred_username")
            ?? user.Identity.Name
            ?? id;

        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("The signed-in user has no object id claim; the action is refused (ADR-004 fail closed).");

        return new AuditActor(id, name);
    }
}
