using System.Text.Json;
using CliWrap;
using System.CommandLine;

Environment.SetEnvironmentVariable(
    "DOTNET_SYSTEM_CONSOLE_ALLOW_ANSI_COLOR_REDIRECTION",
    "true"
);

var argsOpitons = new Option<string>(
    name: "-a",
    description: "Extra aguments",
    getDefaultValue: () => "xyz?"
);

var commandOptions = new Option<string>(
    name: "-c",
    description: "Verb",
    getDefaultValue: () => "run"
);

var rootCommand = new RootCommand("Execute script");
rootCommand.AddOption(commandOptions);
rootCommand.AddOption(argsOpitons);
rootCommand.SetHandler(Execute, commandOptions, argsOpitons);
rootCommand.Invoke(args);

void Execute(string verb, string args) {
    Start(verb, args).GetAwaiter().GetResult();
}

async Task Start(string scriptName, string extra) {
    var ok = LoadScripts().TryGetValue(scriptName, out var script);
    if (!ok) {
        return;
    }

    var tokens = script.Split(" ", StringSplitOptions.RemoveEmptyEntries);
    var app = tokens[0];
    var args = string.Join(" ", tokens.Skip(1)) + extra.Replace("xyz?", string.Empty);

    await using var stdOut = Console.OpenStandardOutput();
    await using var stdErr = Console.OpenStandardError();

    var cli = Cli
        .Wrap(app)
        .WithArguments(args) | (stdOut, stdErr);

    try {
        await cli.ExecuteAsync();
    } catch {}
}

Dictionary<string, string> LoadScripts() {
    var configFile = "global.json";
    var json = File.ReadAllText(configFile);
    var options = new JsonSerializerOptions {
        PropertyNameCaseInsensitive = true
    };
    var obj = JsonSerializer.Deserialize<GlobalConfig>(json, options);
    return obj.Scripts;
}