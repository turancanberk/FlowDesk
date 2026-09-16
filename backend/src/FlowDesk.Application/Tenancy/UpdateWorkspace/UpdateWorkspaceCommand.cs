namespace FlowDesk.Application.Tenancy.UpdateWorkspace;

/// <param name="Name">
/// The new display name. The slug is not changeable: it is the public address
/// of the workspace, and changing it would break every saved link and
/// bookmark.
/// </param>
public sealed record UpdateWorkspaceCommand(string Name);
