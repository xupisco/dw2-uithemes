namespace DW2UIThemes;

internal static class Log
{
    private static readonly object gate = new();
    private static StreamWriter writer;

    public static void Start(string path)
    {
        try
        {
            writer = new StreamWriter(path, append: false) { AutoFlush = true };
        }
        catch
        {
            writer = null;
        }
    }

    public static void Info(string text) => Write("INFO ", text);

    public static void Warn(string text) => Write("WARN ", text);

    public static void Error(string text) => Write("ERROR", text);

    private static void Write(string level, string text)
    {
        lock (gate)
        {
            writer?.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {level} {text}");
        }
    }
}
