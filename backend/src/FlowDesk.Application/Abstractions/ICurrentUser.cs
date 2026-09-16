namespace FlowDesk.Application.Abstractions;

/// <summary>
/// The authenticated caller.
/// </summary>
/// <remarks>
/// Keeps <c>HttpContext</c> and <c>ClaimsPrincipal</c> out of the use cases. A
/// handler needs the user's identity, not the transport that carried it.
/// </remarks>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>The signed-in user's id.</summary>
    /// <exception cref="InvalidOperationException">No user is authenticated.</exception>
    Guid Id { get; }
}
