namespace FlowDesk.Application.Tenancy.CreateWorkspace;

/// <param name="Name">Display name, e.g. "Acme Teknoloji".</param>
/// <param name="Slug">
/// Optional URL identifier. When omitted it is derived from the name, so a
/// Turkish-speaking user is not asked to invent an ASCII address.
/// </param>
public sealed record CreateWorkspaceCommand(string Name, string? Slug);
