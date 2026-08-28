using System.Globalization;
using System.Text;

namespace DLLNelogica.Logging;

internal sealed class TeeTextWriter(TextWriter consoleWriter, DailyLogSink sink) : TextWriter
{
    public override Encoding Encoding => consoleWriter.Encoding;

    public override void Flush()
    {
        sink.Flush(consoleWriter);
    }

    public override void Write(char value)
    {
        sink.Write(consoleWriter, value.ToString(CultureInfo.InvariantCulture));
    }

    public override void Write(string? value)
    {
        sink.Write(consoleWriter, value);
    }

    public override void Write(char[] buffer, int index, int count)
    {
        sink.Write(consoleWriter, new string(buffer, index, count));
    }

    public override void WriteLine()
    {
        sink.Write(consoleWriter, Environment.NewLine);
    }

    public override void WriteLine(string? value)
    {
        sink.Write(consoleWriter, string.Concat(value, Environment.NewLine));
    }
}
