using CDNS.Client.Models;
using CDNS.Client.UDP;
using CDNS.Shared.Helpers;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Text.Json;

namespace CDNS.Client;

class Program
{
    private static bool running = true;

    static void Main(string[] args)
    {
        while (running)
            ShowConfigurationMenu().Invoke();
    }

    private static Action ShowConfigurationMenu()
        => AnsiConsole.Prompt(new SelectionPrompt<MenuOption<Action>>()
            .Title("Choose one of the [green]presets[/] or fill in a domain to look up:")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
            .AddChoices(
                new MenuOption<Action>("Preset 1", () => { LoadLookups(@"Configurations/Lookups1.json"); }),
                new MenuOption<Action>("Preset 2", () => { LoadLookups(@"Configurations/Lookups2.json"); }),
                new MenuOption<Action>("Preset 3", () => { LoadLookups(@"Configurations/Lookups3.json"); }),
                new MenuOption<Action>("Search for a domain", () => { SearchDomain(); }),
                new MenuOption<Action>("Exit client", () => { running = false; })
            )).Value;

    private static void SearchDomain()
    {
        var domainName = AnsiConsole.Prompt(new TextPrompt<string>("Which domain do you want to look up?"));

        CreateClientUDP([domainName]);
    }

    private static void LoadLookups(string lookupFilePath)
    {
        List<string>? dnsLookups = new List<string>();
        if (File.Exists(lookupFilePath))
        {
            string lookupData = File.ReadAllText(lookupFilePath);

            dnsLookups = JsonSerializer.Deserialize<List<string>>(lookupData);
            if (dnsLookups == null)
                LogHelper.Log(LogLevel.Error, $"No DNS Lookup requests found in {lookupFilePath}.", role: Shared.RoleType.Client);
        }

        if (dnsLookups != null && dnsLookups.Count > 0)
            CreateClientUDP(dnsLookups);
    }

    private static void CreateClientUDP(List<string> dnsLookups)
        => new ClientUDP().Start(dnsLookups);
}
