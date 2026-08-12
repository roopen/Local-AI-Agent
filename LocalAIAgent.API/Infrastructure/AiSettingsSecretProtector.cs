using Microsoft.AspNetCore.DataProtection;

namespace LocalAIAgent.API.Infrastructure;

public interface IAiSettingsSecretProtector
{
    bool IsProtected(string value);
    string Protect(string plaintext);
    string Unprotect(string protectedOrLegacyValue);
    bool TryUnprotect(string protectedOrLegacyValue, out string plaintext);
}

public sealed class AiSettingsSecretProtector(IDataProtectionProvider dataProtectionProvider)
    : IAiSettingsSecretProtector
{
    private const string Prefix = "dp:v1:";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(
        "LocalAIAgent.AiSettings.ApiKey.v1");

    public bool IsProtected(string value) => value.StartsWith(Prefix, StringComparison.Ordinal);

    public string Protect(string plaintext) => string.IsNullOrEmpty(plaintext)
        ? string.Empty
        : Prefix + _protector.Protect(plaintext);

    public string Unprotect(string protectedOrLegacyValue)
    {
        if (string.IsNullOrEmpty(protectedOrLegacyValue))
            return string.Empty;

        return IsProtected(protectedOrLegacyValue)
            ? _protector.Unprotect(protectedOrLegacyValue[Prefix.Length..])
            : protectedOrLegacyValue;
    }

    public bool TryUnprotect(string protectedOrLegacyValue, out string plaintext)
    {
        try
        {
            plaintext = Unprotect(protectedOrLegacyValue);
            return true;
        }
        catch
        {
            plaintext = string.Empty;
            return false;
        }
    }
}
