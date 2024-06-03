using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text.Json;
using System.Threading.Tasks;
using CliWrap;

public class Program {
    public static void Main(String[] args) {
        var script = new ScriptExecutor();
        script.Invoke(args);
    }
}

public record ControlOptions(
    Stream OutputStream,
    Stream ErrorStream,
    CancellationTokenSource Graceful,
    CancellationTokenSource Force
);

public class ScriptExecutor {
    private Option<string> argsOptions;
    private Argument<string> commandOptions;
    private RootCommand rootCommand;

    public ScriptExecutor() {
        Environment.SetEnvironmentVariable("DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION", "true");

        argsOptions = new Option<string>(
            name: "-a",
            description: "Extra arguments"
        );

        commandOptions = new Argument<string>(
            name: "command",
            description: "Script name"
        );

        rootCommand = new RootCommand("Execute script");
        rootCommand.AddArgument(commandOptions);
        rootCommand.AddOption(argsOptions);
        rootCommand.SetHandler(Execute, commandOptions, argsOptions);
    }

    public void Invoke(string[] args) {
        rootCommand.Invoke(args);
    }

    private void Execute(string verb, string args) {

        StartAll(args).GetAwaiter().GetResult();
    }

    private async Task StartAll(string extra) {
        var scripts = LoadScripts();

        await using var stdOut = Console.OpenStandardOutput();
        await using var stdErr = Console.OpenStandardError();

        using var forcefulCts = new CancellationTokenSource();
        using var gracefulCts = new CancellationTokenSource();

        Console.CancelKeyPress += (sender, e) => {
            Console.WriteLine("Ctrl+C pressed in the terminal");
            gracefulCts.Cancel();
            Environment.Exit(0);
        };

        foreach (var item in scripts) {
            try {
                var options = new ControlOptions(
                    ErrorStream: stdErr,
                    OutputStream: stdOut,
                    Graceful: gracefulCts,
                    Force: forcefulCts
                );
                await Start(item, options);
            } catch (Exception ex) {
                throw;
            }
        }

        while (Console.ReadLine() != "xyz") { }
    }

    private async Task<CommandTask<CommandResult>> Start(KeyValuePair<string, string> command, ControlOptions options) {
        var outputStram = options.OutputStream;
        var errorStream = options.ErrorStream;

        var (name, script) = command;
        var tokens = script.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        var (app, args) =
            tokens.Length switch {
                1 => (tokens[0], ""),
                > 1 => (tokens[0], string.Join(" ", tokens.Skip(1))),
                _ => ("", "")
            };

        var cli = Cli
            .Wrap(app)
            .WithArguments(args) | (outputStram, errorStream);

        return cli.ExecuteAsync(options.Graceful.Token);
    }

    private Dictionary<string, string> LoadScripts() {
        var configFile = "runner.json";
        var json = File.ReadAllText(configFile);
        var options = new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true
        };
        var obj = JsonSerializer.Deserialize<GlobalConfig>(json, options);
        return obj.Scripts;
    }
}
