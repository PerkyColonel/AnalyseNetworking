using CDNS.Client.Models;
using CDNS.Client.UDP;
using CDNS.Shared;
using CDNS.Shared.Helpers;
using CDNS.Shared.Models;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Net;
using System.Text.Json;

namespace CDNS.Client;

class Program
{
    private static bool running = true;
    private static IPAddress? iPAddress = null;
    private static int? port = null;

    static void Main(string[] args)
    {
        ConfigureConnection();

        while (running)
            ShowConfigurationMenu().Invoke();
    }

    private static void ConfigureConnection()
    {
        AnsiConsole.MarkupLine("[bold green]Welcome to the DNS Client![/]");
        AnsiConsole.MarkupLine("[bold]Please configure the connection to the DNS Server:[/]");

        // prompt if the user wants to use the default settings
        var useDefault = AnsiConsole.Prompt(new ConfirmationPrompt("Do you want to use the default settings?"));
        if (!useDefault)
        {
            var ipString = AnsiConsole.Prompt(new TextPrompt<string>("Enter the IP address of the DNS Server:").Validate((ip) =>
            {
                if (IPAddress.TryParse(ip, out var ipAddress))
                {
                    iPAddress = ipAddress;
                    return ValidationResult.Success();
                }
                return ValidationResult.Error("Invalid IP address.");
            }));

            iPAddress = IPAddress.Parse(ipString);
            port = AnsiConsole.Prompt(new TextPrompt<int>("Enter the port of the DNS Server:"));
        }
    }

    private static Action ShowConfigurationMenu()
    {
        List<MenuOption<Action>> choices = [];

        // Loop through Lookups folder and offer the user to choose a preset or search for a domain
        Directory.GetFiles(@"Lookups", "*.json").ToList().ForEach(file =>
        {
            FileInfo fileInfo = new(file);
            choices.Add(new MenuOption<Action>(fileInfo.Name, () => { LoadLookups(file); }));
        });

        choices.Add(new MenuOption<Action>("Search for a domain", () => { SearchDomain(); }));
        choices.Add(new MenuOption<Action>("Exit client", () => { running = false; }));

        return AnsiConsole.Prompt(new SelectionPrompt<MenuOption<Action>>()
                    .Title("Choose one of the [green]presets[/] or fill in a domain to look up:")
                    .PageSize(10)
                    .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
                    .AddChoices(choices
                    )).Value;
    }

    private static void SearchDomain()
    {
        var recordTypes = Enum.GetValues(typeof(DnsRecordType)).Cast<DnsRecordType>().ToList();

        var recordType = AnsiConsole.Prompt(new SelectionPrompt<DnsRecordType>()
            .Title("What type of record do you want to look up?")
            .PageSize(10)
            .AddChoices(recordTypes));
        var domainName = AnsiConsole.Prompt(new TextPrompt<string>("Which domain do you want to look up?"));

        CreateClientUDP([new DnsLookup() { Name = domainName, Type = recordType.ToString() }]);
    }

    private static void LoadLookups(string lookupFilePath)
    {
        List<DnsLookup>? dnsLookups = [];
        if (File.Exists(lookupFilePath))
        {
            string lookupData = File.ReadAllText(lookupFilePath);

            dnsLookups = JsonSerializer.Deserialize<List<DnsLookup>>(lookupData);
            if (dnsLookups == null)
                LogHelper.Log(LogLevel.Error, $"No DNS Lookup requests found in {lookupFilePath}.", role: RoleType.Client);
        }

        if (dnsLookups != null && dnsLookups.Count > 0)
            CreateClientUDP(dnsLookups);
    }

    private static void CreateClientUDP(List<DnsLookup> dnsLookups)
    {
        if (iPAddress != null && port != null)
            new ClientUDP(iPAddress, port.Value).Start(dnsLookups);
        else
            new ClientUDP().Start(dnsLookups);
    }
}
