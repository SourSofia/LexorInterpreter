namespace LexorInterpreter;

class Program
{
    static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("LEXOR Interpreter v1.0");
            Console.WriteLine("Usage:  dotnet run <file.lexor>");
            Console.WriteLine("        dotnet run --repl");
            return 1;
        }

        if (args[0] == "--repl")
        {
            RunRepl();
            return 0;
        }

        if (!File.Exists(args[0]))
        {
            Console.Error.WriteLine($"Error: File not found: {args[0]}");
            return 1;
        }

        return RunSource(File.ReadAllText(args[0]));
    }

    static int RunSource(string source)
    {
        try
        {
            var tokens    = new Scanner(source).Tokenize();
            var ast       = new Parser(tokens).Parse();
            new Evaluator().Execute(ast);
            return 0;
        }
        catch (LexorException ex)
        {
            Console.Error.WriteLine($"LEXOR Error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Internal error: {ex.Message}");
            return 1;
        }
    }

    static void RunRepl()
    {
        Console.WriteLine("LEXOR Interactive Mode");
        Console.WriteLine("Type your program then press Enter on a blank line to run.");
        Console.WriteLine("Type 'exit' to quit.\n");
        while (true)
        {
            Console.Write("> ");
            var lines = new List<string>();
            string? line;
            while (!string.IsNullOrWhiteSpace(line = Console.ReadLine()))
            {
                if (line.Trim() == "exit") return;
                lines.Add(line);
            }
            if (lines.Count == 0) continue;
            RunSource(string.Join("\n", lines));
            Console.WriteLine();
        }
    }
}

public class LexorException : Exception
{
    public LexorException(string message) : base(message) { }
}