namespace PoEKompanion;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SharpHook.Data;

public class ConfigurationModel
{
    public HotkeyCombo LogoutHotkey { get; set; } = new(KeyCode.VcBackQuote);
    public HotkeyCombo OpenSettingsHotkey { get; set; } = new(KeyCode.VcF10);
    public HotkeyCombo HideoutHotkey { get; set; } = new(KeyCode.VcF5);
    public HotkeyCombo ExitHotkey { get; set; } = new(KeyCode.VcSpace, ctrl: true, shift: true);
    public ChatCommandHotkey[] ChatCommandHotkeys { get; set; } = ChatCommandHotkey.GetDefaults();

    public IEnumerable<ChatCommandHotkey> GetChatCommandHotkeys()
    {
        yield return new ChatCommandHotkey(this.ExitHotkey, "/exit", 0);

        foreach (var chatCommandHotkey in this.ChatCommandHotkeys)
        {
            yield return chatCommandHotkey;
        }
    }
}

public sealed class ChatCommandHotkey
{
    public const int CUSTOM_COMMAND_MINIMUM_SUBMIT_DELAY_MS = 100;

    private const int CUSTOM_COMMAND_COUNT = 5;

    public HotkeyCombo Hotkey { get; set; }
    public string Command { get; set; }
    public int MinimumSubmitDelayMs { get; set; }

    [JsonConstructor]
    public ChatCommandHotkey(
        HotkeyCombo? hotkey,
        string? command,
        int minimumSubmitDelayMs = CUSTOM_COMMAND_MINIMUM_SUBMIT_DELAY_MS)
    {
        this.Hotkey = hotkey ?? new HotkeyCombo(KeyCode.VcF4);
        this.Command = command ?? string.Empty;
        this.MinimumSubmitDelayMs = minimumSubmitDelayMs;
    }

    public static ChatCommandHotkey[] GetDefaults() =>
    [
        new(new HotkeyCombo(KeyCode.VcF2), "/played"),
        new(new HotkeyCombo(KeyCode.VcF3), "/remaining"),
        new(new HotkeyCombo(KeyCode.VcF4), "/deaths"),
        new(new HotkeyCombo(KeyCode.VcF5), "/hideout"),
        new(new HotkeyCombo(KeyCode.VcF6), "/leave"),
    ];

    public static ChatCommandHotkey? FindMatching(IEnumerable<ChatCommandHotkey> chatCommandHotkeys, KeyCode keyCode, bool ctrl, bool shift, bool alt) =>
        chatCommandHotkeys.FirstOrDefault(chatCommandHotkey => chatCommandHotkey.Matches(keyCode, ctrl, shift, alt));

    public static ChatCommandHotkey[] Normalize(ChatCommandHotkey[]? chatCommandHotkeys, HotkeyCombo? hideoutHotkey = null)
    {
        var defaults = GetDefaults();
        var source = new List<ChatCommandHotkey>();

        if (chatCommandHotkeys is not null)
        {
            foreach (var chatCommandHotkey in chatCommandHotkeys)
            {
                if (chatCommandHotkey is not null)
                {
                    source.Add(chatCommandHotkey);
                }
            }
        }

        if (IsLegacyDefaultSet(source))
        {
            source = defaults.ToList();
        }

        if (source.Count < CUSTOM_COMMAND_COUNT &&
            !source.Any(chatCommandHotkey => string.Equals(chatCommandHotkey.Command, "/hideout", StringComparison.OrdinalIgnoreCase)))
        {
            source.Insert(0, new ChatCommandHotkey(hideoutHotkey ?? defaults[0].Hotkey, "/hideout"));
        }

        var normalized = new ChatCommandHotkey[CUSTOM_COMMAND_COUNT];

        for (var i = 0; i < CUSTOM_COMMAND_COUNT; i++)
        {
            var chatCommandHotkey = i < source.Count
                ? source[i]
                : defaults[i];

            if (chatCommandHotkey is null || chatCommandHotkey.Hotkey is null || !Enum.IsDefined(typeof(KeyCode), chatCommandHotkey.Hotkey.Key))
            {
                normalized[i] = defaults[i];
                continue;
            }

            chatCommandHotkey.Command ??= string.Empty;
            chatCommandHotkey.Command = chatCommandHotkey.Command switch
            {
                "/invite" => "/invite @last",
                "/kick" => "/kick @last",
                _ => chatCommandHotkey.Command,
            };
            chatCommandHotkey.MinimumSubmitDelayMs = Math.Max(
                CUSTOM_COMMAND_MINIMUM_SUBMIT_DELAY_MS,
                chatCommandHotkey.MinimumSubmitDelayMs);
            normalized[i] = chatCommandHotkey;
        }

        return normalized;
    }

    private static bool IsLegacyDefaultSet(IReadOnlyList<ChatCommandHotkey> source) =>
        source.Count switch
        {
            4 =>
                IsCommand(source[0], KeyCode.VcF4, "/leave") &&
                IsCommand(source[1], KeyCode.VcF3, "/remaining") &&
                IsCommand(source[2], KeyCode.VcF7, "/invite", "/invite @last") &&
                IsCommand(source[3], KeyCode.VcF8, "/kick", "/kick @last"),
            5 =>
                IsCommand(source[0], KeyCode.VcF5, "/hideout") &&
                IsCommand(source[1], KeyCode.VcF3, "/remaining") &&
                IsCommand(source[2], KeyCode.VcF4, "/leave") &&
                IsCommand(source[3], KeyCode.VcF7, "/invite", "/invite @last") &&
                IsCommand(source[4], KeyCode.VcF8, "/kick", "/kick @last"),
            _ => false,
        };

    private static bool IsCommand(ChatCommandHotkey chatCommandHotkey, KeyCode keyCode, params string[] commands) =>
        chatCommandHotkey.Hotkey?.Key == keyCode &&
        commands.Contains(chatCommandHotkey.Command, StringComparer.OrdinalIgnoreCase);

    public bool Matches(KeyCode keyCode, bool ctrl, bool shift, bool alt) =>
        !string.IsNullOrWhiteSpace(this.Command) &&
        this.Hotkey.Matches(keyCode, ctrl, shift, alt);
}

[JsonSerializable(typeof(ConfigurationModel))]
[JsonSerializable(typeof(HotkeyCombo))]
[JsonSerializable(typeof(ChatCommandHotkey))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class ConfigJsonContext : JsonSerializerContext
{
}

public static class ConfigurationManager
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "poe-kompanion"
    );

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "config.json");

    public static async Task<ConfigurationModel> LoadAsync()
    {
        try
        {
            if (!File.Exists(ConfigFilePath)) return GetDefault();

            await using var stream = File.OpenRead(ConfigFilePath);
            var config = await JsonSerializer.DeserializeAsync(stream, ConfigJsonContext.Default.ConfigurationModel);

            if (config is null)
            {
                Console.WriteLine("Warning: Configuration file is empty or invalid. Using defaults.");
                return GetDefault();
            }

            if (!Enum.IsDefined(typeof(KeyCode), config.LogoutHotkey.Key))
            {
                Console.WriteLine("Warning: Invalid logout hotkey in configuration. Using default.");
                config.LogoutHotkey = new HotkeyCombo(KeyCode.VcBackQuote);
            }

            if (!Enum.IsDefined(typeof(KeyCode), config.OpenSettingsHotkey.Key))
            {
                Console.WriteLine("Warning: Invalid settings hotkey in configuration. Using default.");
                config.OpenSettingsHotkey = new HotkeyCombo(KeyCode.VcF10);
            }

            if (!Enum.IsDefined(typeof(KeyCode), config.HideoutHotkey.Key))
            {
                Console.WriteLine("Warning: Invalid hideout hotkey in configuration. Using default.");
                config.HideoutHotkey = new HotkeyCombo(KeyCode.VcF5);
            }

            if (!Enum.IsDefined(typeof(KeyCode), config.ExitHotkey.Key))
            {
                Console.WriteLine("Warning: Invalid exit hotkey in configuration. Using default.");
                config.ExitHotkey = new HotkeyCombo(KeyCode.VcSpace, ctrl: true, shift: true);
            }

            config.ChatCommandHotkeys = ChatCommandHotkey.Normalize(config.ChatCommandHotkeys, config.HideoutHotkey);

            return config;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to load configuration: {ex.Message}. Using defaults.");
            return GetDefault();
        }
    }

    public static async Task SaveAsync(ConfigurationModel config)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            var tempPath = ConfigFilePath + ".tmp";

            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, config, ConfigJsonContext.Default.ConfigurationModel);
            }

            File.Move(tempPath, ConfigFilePath, overwrite: true);

            Console.WriteLine($"Configuration saved to {ConfigFilePath}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    private static ConfigurationModel GetDefault() => new();
}
