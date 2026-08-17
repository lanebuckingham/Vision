namespace Vision.CredentialService.Application.Common;

/// <summary>
/// Named constants for credential business rules.
/// </summary>
public static class CredentialPolicy
{
    /// <summary>
    /// Number of days before expiration at which a credential is considered "expiring soon."
    /// </summary>
    public const int ExpiringSoonDays = 30;
}
