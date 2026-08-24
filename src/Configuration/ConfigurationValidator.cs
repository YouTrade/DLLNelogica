namespace DLLNelogica;

internal static class ConfigurationValidator
{
    internal static List<string> GetIssues(CredenciaisOptions credentials)
    {
        var issues = new List<string>();

        ValidateValue(credentials.Key, "Credenciais.Key", issues);
        ValidateValue(credentials.User, "Credenciais.User", issues);
        ValidateValue(credentials.Password, "Credenciais.Password", issues);

        return issues;
    }

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
}
