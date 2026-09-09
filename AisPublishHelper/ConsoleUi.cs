namespace AisPublishHelper;

internal static class ConsoleUi
{
    public static void Header()
    {
        Console.WriteLine("AIS Publish Helper");
        Console.WriteLine("Publishes a new AIS version: build, copy AppServer binaries, upload installer zip.");
        Console.WriteLine();
    }

    public static bool ConfirmStep(string title, params string[] details)
    {
        Separator();
        Console.WriteLine(title);
        foreach (var line in details)
        {
            Console.WriteLine("  " + line);
        }

        Console.WriteLine();
        Console.Write("Press Enter to continue (or type q then Enter to abort): ");
        var answer = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(answer) &&
            (answer.Equals("q", StringComparison.OrdinalIgnoreCase) ||
             answer.Equals("n", StringComparison.OrdinalIgnoreCase)))
        {
            Console.WriteLine("Aborted by user.");
            return false;
        }

        Console.WriteLine();
        return true;
    }

    public static void Success(string message)
    {
        WriteColored(ConsoleColor.Green, message);
        Console.WriteLine();
    }

    public static void Fail(string message)
    {
        WriteColored(ConsoleColor.Red, message);
        Console.WriteLine("Stopping. Remaining steps will not run.");
        Console.WriteLine();
    }

    public static void Info(string message) => Console.WriteLine(message);

    public static void Separator()
    {
        Console.WriteLine("------------------------------------------------------------");
    }

    private static void WriteColored(ConsoleColor color, string message)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }
}
