namespace Ogur.Core.Security;

/// <summary>
/// Options for configuring symmetric encryption in Ogur.
/// </summary>
public sealed class EncryptionOptions
{
    /// <summary>
    /// Gets or sets the fallback encryption key stored in configuration.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the environment variable name from which to read the key.
    /// </summary>
    public string EnvVarName { get; set; } = "OGUR_ENC_KEY";
}