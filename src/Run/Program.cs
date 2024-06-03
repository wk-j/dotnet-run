using System.Text.Json;
using CliWrap;
using System.CommandLine;

Environment.SetEnvironmentVariable(
    "DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION",
    "true"
);

var argsOpitons = new Option<string>(
    name: "-a",
    description: "Extra aguments"
);

var commandOptions = new Argument<string>(
    name: "command",
    description: "Script name"
);

var rootCommand = new RootCommand("Execute script");
rootCommand.AddArgument(commandOptions);
rootCommand.AddOption(argsOpitons);
rootCommand.SetHandler(Execute, commandOptions, argsOpitons);
rootCommand.Invoke(args);

void Execute(string verb, string args) {
    Start(verb, args).GetAwaiter().GetResult();
}

async Task Start(string scriptName, string extra) {
    var ok = LoadScripts().TryGetValue(scriptName, out var script);
    if (!ok) {
        Console.WriteLine("Cannot find script (Name={0})", scriptName);
        return;
    }

    var tokens = script.Split(" ", StringSplitOptions.RemoveEmptyEntries);
    var (app, args) =
        tokens.Length switch {
            1 => (tokens[0], ""),
            > 1 => (tokens[0], string.Join(" ", tokens.Skip(1)) + " " + extra),
            _ => ("", "")
        };

    await using var stdOut = Console.OpenStandardOutput();
    await using var stdErr = Console.OpenStandardError();

    var cli = Cli
        .Wrap(app)
        .WithArguments(args) | (stdOut, stdErr);

    try {
        await cli.ExecuteAsync();
    } catch { }
}

Dictionary<string, string> LoadScripts() {
    var configFile = "run.json";
    var json = File.ReadAllText(configFile);
    var options = new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true
    };
    var obj = JsonSerializer.Deserialize<GlobalConfig>(json, options);
    return obj.Scripts;
}
