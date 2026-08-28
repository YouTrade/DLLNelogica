namespace DLLNelogica.Configuration;

internal sealed class ConfigurationException : Exception
{
    internal ConfigurationException(string message)
        : base(message)
    {
    }
}
