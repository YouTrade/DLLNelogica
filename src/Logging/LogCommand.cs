namespace DLLNelogica.Logging;

internal readonly record struct LogCommand(
    DateTime Timestamp,
    string FileName,
    LogLinePrefix Prefix,
    TextWriter? ConsoleWriter,
    string? Value,
    TaskCompletionSource? FlushCompletion)
{
    internal static LogCommand Write(
        DateTime timestamp,
        string fileName,
        LogLinePrefix prefix,
        TextWriter? consoleWriter,
        string value) =>
        new(timestamp, fileName, prefix, consoleWriter, value, null);

    internal static LogCommand Flush(TextWriter consoleWriter, TaskCompletionSource completion) =>
        new(default, ReportFiles.Session, LogLinePrefix.SessionStamp, consoleWriter, null, completion);
}
