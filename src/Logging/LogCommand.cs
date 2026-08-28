namespace DLLNelogica.Logging;

internal readonly record struct LogCommand(
    DateTime Timestamp,
    TextWriter ConsoleWriter,
    string? Value,
    TaskCompletionSource? FlushCompletion)
{
    internal static LogCommand Write(DateTime timestamp, TextWriter consoleWriter, string value) =>
        new(timestamp, consoleWriter, value, null);

    internal static LogCommand Flush(TextWriter consoleWriter, TaskCompletionSource completion) =>
        new(default, consoleWriter, null, completion);
}
