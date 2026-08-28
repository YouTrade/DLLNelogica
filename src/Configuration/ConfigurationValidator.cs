namespace DLLNelogica.Configuration;

internal static class ConfigurationValidator
{
    internal static List<string> GetIssues(CredentialsOptions credentials)
    {
        var issues = new List<string>();

        ValidateValue(credentials.Key, "Credenciais.Key", issues);
        ValidateValue(credentials.User, "Credenciais.User", issues);
        ValidateValue(credentials.Password, "Credenciais.Password", issues);

        return issues;
    }

    internal static bool HasOuterWhitespace(CredentialsOptions credentials) =>
        HasOuterWhitespace(credentials.Key) ||
        HasOuterWhitespace(credentials.User) ||
        HasOuterWhitespace(credentials.Password);

    private static void ValidateValue(string value, string propertyName, List<string> issues)
    {
        if (value.Length == 0)
        {
            issues.Add($"{propertyName} está vazio.");
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add($"{propertyName} contém apenas espaços.");
        }
    }

    private static bool HasOuterWhitespace(string value) =>
        value.Length > 0 && (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]));
}
