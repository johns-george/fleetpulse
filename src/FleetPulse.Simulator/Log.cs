namespace FleetPulse.Simulator;

public static class Log
{
    public static void Info(string message) => Write(ConsoleColor.Gray, "INFO", message);
    public static void Heartbeat(string message) => Write(ConsoleColor.DarkCyan, "BEAT", message);
    public static void Command(string message) => Write(ConsoleColor.Green, "CMD ", message);
    public static void Warn(string message) => Write(ConsoleColor.Yellow, "WARN", message);
    public static void Error(string message) => Write(ConsoleColor.Red, "ERR ", message);

    private static void Write(ConsoleColor color, string level, string message)
    {
        var original = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write($"[{DateTime.Now:HH:mm:ss}] ");
        Console.ForegroundColor = color;
        Console.Write($"{level} ");
        Console.ForegroundColor = original;
        Console.WriteLine(message);
    }
}
