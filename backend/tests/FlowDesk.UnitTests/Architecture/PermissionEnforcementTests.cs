using System.Text.RegularExpressions;
using FlowDesk.Application.Tenancy;

namespace FlowDesk.UnitTests.Architecture;

/// <summary>
/// Every action in the permission matrix is actually checked somewhere.
/// </summary>
/// <remarks>
/// The matrix tests prove that each action is mapped to a role. They cannot
/// prove the mapping is used: an entry nobody consults looks exactly like one
/// that is enforced, until someone tightens it and nothing changes. Phase 16
/// found one — <c>ViewMembers</c> — which is why this test exists.
///
/// Reads the source rather than the compiled assembly, for the same reason
/// <see cref="SolutionLayout"/> does: enum values are inlined as constants, so
/// the compiled code no longer names the action it checks.
/// </remarks>
public sealed partial class PermissionEnforcementTests
{
    [Fact]
    public void Every_action_in_the_matrix_is_checked_by_a_use_case()
    {
        var applicationSource = new DirectoryInfo(
            Path.Combine(SolutionLayout.BackendRoot.FullName, "src", "FlowDesk.Application"));

        var checkedActions = applicationSource
            .EnumerateFiles("*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .SelectMany(file => PermissionCheck().Matches(File.ReadAllText(file.FullName)))
            .Select(match => match.Groups["action"].Value)
            .ToHashSet(StringComparer.Ordinal);

        var unenforced = Enum.GetNames<WorkspaceAction>()
            .Where(action => !checkedActions.Contains(action))
            .ToList();

        Assert.True(
            unenforced.Count == 0,
            "Matriste olup hiçbir use case'te denetlenmeyen eylemler: " + string.Join(", ", unenforced));
    }

    private static bool IsBuildOutput(FileInfo file) =>
        file.FullName.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.FullName.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal);

    [GeneratedRegex(
        @"WorkspacePermissions\.IsGranted\([^;]*?WorkspaceAction\.(?<action>\w+)",
        RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex PermissionCheck();
}
