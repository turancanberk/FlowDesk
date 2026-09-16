namespace FlowDesk.Application.Abstractions;

/// <summary>
/// Supplies the current time.
/// </summary>
/// <remarks>
/// Token expiry, rotation windows and replay detection are all time-dependent,
/// and a test that has to wait fourteen days to prove a refresh token expired
/// is not a test. Reading the clock through this contract lets those rules be
/// exercised deterministically.
/// </remarks>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
