using System.Text.Json;
using DLLNelogica.Configuration;

namespace DLLNelogica.Application;

internal sealed class ApplicationRunner
{
    private readonly JsonConfigurationLoader _configurationLoader;
    private readonly MarketDataApplication _application;

    internal ApplicationRunner(
        JsonConfigurationLoader configurationLoader,
        MarketDataApplication application)
    {
        _configurationLoader = configurationLoader;
        _application = application;
    }

    internal async Task<int> RunAsync()
    {
        try
        {
            var options = _configurationLoader.Load();
            ReportLoadedConfiguration(options);
            return await _application.RunAsync(options).ConfigureAwait(false);
        }
        catch (FileNotFoundException)
        {
            TryWriteLine("O arquivo appsettings.json não foi encontrado.", true);
        }
        catch (UnauthorizedAccessException)
        {
            TryWriteLine("Acesso negado ao appsettings.json.", true);
        }
        catch (IOException)
        {
            TryWriteLine("Não foi possível ler o appsettings.json.", true);
        }
        catch (JsonException exception)
        {
            var line = exception.LineNumber.HasValue ? exception.LineNumber.Value + 1 : 0;
            var position = exception.BytePositionInLine.HasValue ? exception.BytePositionInLine.Value + 1 : 0;
            TryWriteLine($"JSON inválido na linha {line}, posição {position}.", true);
        }
        catch (ConfigurationException exception)
        {
            TryWriteLine(exception.Message, true);
        }

        return 1;
    }

    private static void ReportLoadedConfiguration(ApplicationOptions options)
    {
        TryWriteLine("Configuração carregada e validada com sucesso.");
        TryWriteLine($"Processo x64: {Environment.Is64BitProcess}");

        if (ConfigurationValidator.HasOuterWhitespace(options.Credenciais))
        {
            TryWriteLine(
                "Aviso: uma ou mais credenciais contêm espaços nas extremidades e serão enviadas sem alteração.");
        }

        if (ConfigurationValidator.HasOuterWhitespace(options.MarketData.Instruments))
        {
            TryWriteLine(
                "Aviso: um ou mais instrumentos contêm espaços nas extremidades e serão enviados sem alteração.");
        }
    }

    private static void TryWriteLine(string message, bool isError = false)
    {
        try
        {
            if (isError)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                Console.WriteLine(message);
            }
        }
        catch
        {
            // Falha de saída não altera a validação da configuração.
        }
    }
}
