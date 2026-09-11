using System.Collections.Concurrent;
using DLLNelogica.Logging;

namespace DLLNelogica.Tests;

internal sealed class TradeReportProbe : IReportLog
{
    internal ConcurrentQueue<(string File, string Message)> Lines { get; } = new();
    public void WriteToFile(string fileName, string message) => Lines.Enqueue((fileName, message));
    public void WriteToFileAndConsole(string fileName, string message) => Lines.Enqueue((fileName, message));
}
