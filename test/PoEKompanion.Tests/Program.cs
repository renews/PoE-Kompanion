namespace PoEKompanion.Tests;

using System;
using System.IO;
using System.Linq;
using System.Reflection;

using MessagePack;
using PoEKompanion;
using SharpHook.Data;

internal static class Program
{
    private static int Main()
    {
        AssertEqual(
            "1.1.2",
            typeof(PoEProcessDetector).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            "Assembly informational version should match release version");

        var projectSource = File.ReadAllText("PoE-Kompanion/PoEKompanion.csproj");
        AssertEqual(
            true,
            projectSource.Contains("<PackageReference Include=\"MessagePack\" Version=\"3.1.8\" />"),
            "MessagePack should use the patched 3.1.8 release");
        AssertEqual(
            true,
            projectSource.Contains("<PackageReference Include=\"Tmds.DBus.Protocol\" Version=\"0.94.1\" />"),
            "The Avalonia 12 D-Bus dependency should use its compatible version");
        AssertEqual(
            true,
            projectSource.Contains("<PackageReference Include=\"Avalonia\" Version=\"12.1.1\" />") &&
            projectSource.Contains("<PackageReference Include=\"Avalonia.Desktop\" Version=\"12.1.1\" />") &&
            projectSource.Contains("<PackageReference Include=\"Avalonia.Themes.Simple\" Version=\"12.1.1\" />"),
            "Avalonia packages should use the version with the standards-compliant Linux tray implementation");

        AssertMatches(
            "Main",
            @"S:\common\Path of Exile 2\PathOfExileSteam.exe --nopatch",
            "Proton Path of Exile 2 game process should match");

        AssertMatches(
            "Main",
            @"S:\common\Path of Exile 2\PathOfExile_x64Steam.exe --nopatch",
            "Proton Path of Exile 2 x64 Steam game process should match");

        AssertMatches(
            "PathOfExileSteam",
            string.Empty,
            "Native Path of Exile Steam process name should match");

        AssertMatches(
            "PathOfExile_x64",
            string.Empty,
            "Native Path of Exile x64 process name should match");

        AssertMatches(
            "Main",
            @"S:\common\Path of Exile\PathOfExile_x64.exe --nopatch",
            "Proton Path of Exile 1 standalone game process should match");

        AssertDoesNotMatch(
            "reaper",
            "SteamLaunch AppId=2694490 -- /home/user/.local/share/Steam/steamapps/common/Path of Exile 2/PathOfExileSteam.exe --nopatch",
            "Steam launch helper should not match");

        AssertDoesNotMatch(
            "python3",
            "/home/user/.local/share/Steam/steamapps/common/Proton - Experimental/proton waitforexitandrun /home/user/.local/share/Steam/steamapps/common/Path of Exile 2/PathOfExileSteam.exe --nopatch",
            "Proton launcher should not match");

        AssertEqual("1.2.3.4", ProcNetTcpAddressParser.HexToIp("04030201"), "IPv4 proc address should parse");
        AssertEqual("::1", ProcNetTcpAddressParser.HexToIp("00000000000000000000000001000000"), "IPv6 proc address should parse");
        AssertEqual(false, ProcNetTcpAddressParser.IsRemoteAddress("::1"), "IPv6 loopback should be ignored");
        AssertEqual(true, ProcNetTcpAddressParser.IsRemoteAddress("2600:1419:6200:61::17df:d327"), "IPv6 remote should be tracked");

        var backgroundStartInfo = BackgroundProcessStartInfoFactory.Create("/tmp/PoEKompanion", 1234);
        AssertEqual("/tmp/PoEKompanion", backgroundStartInfo.FileName, "Background helper should start as the app binary");
        AssertSequenceEqual(["--bg", "1234"], backgroundStartInfo.ArgumentList, "Background helper arguments");
        AssertEqual(false, backgroundStartInfo.UseShellExecute, "Background helper should not use a shell");
        AssertEqual(true, backgroundStartInfo.CreateNoWindow, "Background helper should not create a window");
        AssertEqual("1", backgroundStartInfo.Environment["APPIMAGELAUNCHER_DISABLE"], "Background helper should disable AppImageLauncher");

        var directKillStartInfo = SocketKillProcessStartInfoFactory.CreateDirect("1.2.3.4", 443);
        AssertEqual("/usr/bin/ss", directKillStartInfo.FileName, "Direct socket kill should call ss");
        AssertSequenceEqual(["-K", "dst", "1.2.3.4", "dport", "443"], directKillStartInfo.ArgumentList, "Direct socket kill arguments");
        AssertEqual(false, directKillStartInfo.UseShellExecute, "Direct socket kill should not use a shell");
        AssertEqual(true, directKillStartInfo.RedirectStandardOutput, "Direct socket kill should capture stdout");
        AssertEqual(true, directKillStartInfo.RedirectStandardError, "Direct socket kill should capture stderr");

        var helperKillStartInfo = SocketKillProcessStartInfoFactory.CreateHelper(5678, "1.2.3.4", 443);
        AssertEqual("sudo", helperKillStartInfo.FileName, "Helper socket kill should use sudo");
        AssertSequenceEqual(
            ["-n", "/usr/local/libexec/poe-kompanion-kill-connection", "5678", "1.2.3.4", "443"],
            helperKillStartInfo.ArgumentList,
            "Helper socket kill arguments");
        AssertEqual(false, helperKillStartInfo.UseShellExecute, "Helper socket kill should not use a shell");
        AssertEqual(true, helperKillStartInfo.RedirectStandardOutput, "Helper socket kill should capture stdout");
        AssertEqual(true, helperKillStartInfo.RedirectStandardError, "Helper socket kill should capture stderr");

        AssertEqual(
            "/tmp/AppDir/usr/share/poe-kompanion/poe-kompanion-kill-connection",
            LogoutHelperInstaller.GetBundledHelperPath("/tmp/AppDir"),
            "Bundled helper path should live in the AppDir");
        AssertEqual(true, LogoutHelperInstaller.IsValidUserName("appuser"), "Simple Linux username should be valid");
        AssertEqual(false, LogoutHelperInstaller.IsValidUserName("bad user"), "Username with spaces should be invalid");
        AssertEqual(
            "# Managed by PoE Kompanion.\nappuser ALL=(root) NOPASSWD: /usr/local/libexec/poe-kompanion-kill-connection *\n",
            LogoutHelperInstaller.CreateSudoersContent("appuser"),
            "Sudoers content should allow only the fixed helper path");
        var autoInstallStartInfo = LogoutHelperInstaller.CreateAutoInstallStartInfo("/tmp/PoE.AppImage", "appuser");
        AssertEqual("pkexec", autoInstallStartInfo.FileName, "Auto install should use pkexec for graphical auth");
        AssertSequenceEqual(
            ["/tmp/PoE.AppImage", "--install-logout-helper", "appuser"],
            autoInstallStartInfo.ArgumentList,
            "Auto install arguments");
        AssertEqual(false, autoInstallStartInfo.UseShellExecute, "Auto install should not use a shell");
        AssertEqual(true, autoInstallStartInfo.RedirectStandardOutput, "Auto install should capture stdout");
        AssertEqual(true, autoInstallStartInfo.RedirectStandardError, "Auto install should capture stderr");

        var whoamiStartInfo = CurrentUser.CreateWhoamiStartInfo();
        AssertEqual("/usr/bin/whoami", whoamiStartInfo.FileName, "Current user should be resolved through whoami");
        AssertEqual(false, whoamiStartInfo.UseShellExecute, "whoami should not use a shell");
        AssertEqual(true, whoamiStartInfo.RedirectStandardOutput, "whoami should capture stdout");
        AssertEqual(true, CurrentUser.GetEffectiveUserIdSegment().StartsWith("uid-"), "Fallback user segment should be UID-based");

        AssertEqual(
            "/run/user/1000/poe-kompanion/server.sock",
            UnixSocketIpc.GetServerSocketPath("/run/user/1000"),
            "Server IPC socket should live under the user runtime directory");
        AssertEqual(
            "/run/user/1000/poe-kompanion/client.sock",
            UnixSocketIpc.GetClientSocketPath("/run/user/1000"),
            "Client IPC socket should live under the user runtime directory");
        AssertEqual(
            "/tmp/poe-kompanion-appuser/server.sock",
            UnixSocketIpc.GetServerSocketPath(string.Empty, "appuser"),
            "Fallback IPC socket should be namespaced by user");

        var defaultConfig = new ConfigurationModel();
        AssertEqual(5, defaultConfig.ChatCommandHotkeys.Length, "Default config should expose five custom chat command hotkeys");
        AssertEqual(KeyCode.VcF2, defaultConfig.ChatCommandHotkeys[0].Hotkey.Key, "First custom command hotkey should default to F2");
        AssertEqual("/played", defaultConfig.ChatCommandHotkeys[0].Command, "First custom command should default to played");
        AssertEqual(KeyCode.VcF3, defaultConfig.ChatCommandHotkeys[1].Hotkey.Key, "Second custom command hotkey should default to F3");
        AssertEqual("/remaining", defaultConfig.ChatCommandHotkeys[1].Command, "Second custom command should default to remaining");
        AssertEqual(KeyCode.VcF4, defaultConfig.ChatCommandHotkeys[2].Hotkey.Key, "Third custom command hotkey should default to F4");
        AssertEqual("/deaths", defaultConfig.ChatCommandHotkeys[2].Command, "Third custom command should default to deaths");
        AssertEqual(KeyCode.VcF5, defaultConfig.ChatCommandHotkeys[3].Hotkey.Key, "Fourth custom command hotkey should default to F5");
        AssertEqual("/hideout", defaultConfig.ChatCommandHotkeys[3].Command, "Fourth custom command should default to hideout");
        AssertEqual(KeyCode.VcF6, defaultConfig.ChatCommandHotkeys[4].Hotkey.Key, "Fifth custom command hotkey should default to F6");
        AssertEqual("/leave", defaultConfig.ChatCommandHotkeys[4].Command, "Fifth custom command should default to leave");
        AssertEqual(100, defaultConfig.ChatCommandHotkeys[0].MinimumSubmitDelayMs, "Custom commands should wait at least 100ms before submit");
        AssertEqual(0, defaultConfig.GetChatCommandHotkeys().First().MinimumSubmitDelayMs, "Built-in exit command should not add a custom command delay");
        AssertEqual(true, defaultConfig.ChatCommandHotkeys[0].Matches(KeyCode.VcF2, false, false, false), "Custom command hotkey should match its configured key");
        AssertEqual(false, defaultConfig.ChatCommandHotkeys[0].Matches(KeyCode.VcF2, true, false, false), "Custom command hotkey should require exact modifiers");
        AssertEqual(
            "/remaining",
            ChatCommandHotkey.FindMatching(defaultConfig.GetChatCommandHotkeys(), KeyCode.VcF3, false, false, false)?.Command,
            "Configured chat command hotkeys should resolve matching custom commands");
        AssertEqual(
            100,
            ChatCommandHotkey.FindMatching(defaultConfig.GetChatCommandHotkeys(), KeyCode.VcF3, false, false, false)?.MinimumSubmitDelayMs,
            "Resolved custom command should carry the minimum submit delay");

        AssertEqual(false, defaultConfig.AutoWeaponSwap.Enabled, "Auto weapon swap should default disabled");
        AssertEqual(KeyCode.VcX, defaultConfig.AutoWeaponSwap.Key.Key, "Auto weapon swap should default to X");
        AssertEqual(KeyCode.VcF7, defaultConfig.AutoWeaponSwap.ToggleHotkey.Key, "Auto weapon swap toggle should default to F7");
        AssertEqual(400, defaultConfig.AutoWeaponSwap.IntervalMs, "Auto weapon swap should default to 400ms");

        var shortAutoWeaponSwap = AutoWeaponSwapSettings.Normalize(
            new AutoWeaponSwapSettings(true, null, 50));
        AssertEqual(true, shortAutoWeaponSwap.Enabled, "Auto weapon swap normalization should keep enabled state");
        AssertEqual(KeyCode.VcX, shortAutoWeaponSwap.Key.Key, "Auto weapon swap normalization should restore a missing key");
        AssertEqual(KeyCode.VcF7, shortAutoWeaponSwap.ToggleHotkey.Key, "Auto weapon swap normalization should restore a missing toggle hotkey");
        AssertEqual(300, shortAutoWeaponSwap.IntervalMs, "Auto weapon swap normalization should clamp the minimum interval");

        var longAutoWeaponSwap = AutoWeaponSwapSettings.Normalize(
            new AutoWeaponSwapSettings(true, new HotkeyCombo(KeyCode.VcW), new HotkeyCombo(KeyCode.VcF8), 1200));
        AssertEqual(KeyCode.VcW, longAutoWeaponSwap.Key.Key, "Auto weapon swap normalization should keep configured key");
        AssertEqual(KeyCode.VcF8, longAutoWeaponSwap.ToggleHotkey.Key, "Auto weapon swap normalization should keep configured toggle hotkey");
        AssertEqual(1000, longAutoWeaponSwap.IntervalMs, "Auto weapon swap normalization should clamp the maximum interval");
        longAutoWeaponSwap.ToggleEnabled();
        AssertEqual(false, longAutoWeaponSwap.Enabled, "Auto weapon swap toggle should turn enabled off");
        longAutoWeaponSwap.ToggleEnabled();
        AssertEqual(true, longAutoWeaponSwap.Enabled, "Auto weapon swap toggle should turn enabled on");

        var minimumMsRange = AutoWeaponSwapSettings.GetDelayRange(100);
        AssertEqual(270, minimumMsRange.MinMs, "Auto weapon swap should clamp low base intervals before subtracting 10 percent");
        AssertEqual(330, minimumMsRange.MaxMs, "Auto weapon swap should clamp low base intervals before adding 10 percent");

        var defaultMsRange = AutoWeaponSwapSettings.GetDelayRange(400);
        AssertEqual(360, defaultMsRange.MinMs, "Auto weapon swap default jitter should start at 360ms");
        AssertEqual(440, defaultMsRange.MaxMs, "Auto weapon swap default jitter should end at 440ms");

        var random = new Random(1234);
        for (var i = 0; i < 100; i++)
        {
            var delayMs = AutoWeaponSwapSettings.CreateDelayMs(400, random);
            AssertEqual(true, delayMs >= 360 && delayMs <= 440, "Auto weapon swap random delay should stay within 10 percent");
        }

        AssertEqual(true, LinuxKeyCodeMapper.TryMap(KeyCode.VcX, out var linuxXKeyCode), "Auto weapon swap should map the default X key");
        AssertEqual(45, linuxXKeyCode, "X should map to Linux KEY_X");
        AssertEqual(true, LinuxKeyCodeMapper.TryMap(KeyCode.VcF1, out var linuxF1KeyCode), "Auto weapon swap should map function keys");
        AssertEqual(59, linuxF1KeyCode, "F1 should map to Linux KEY_F1");
        AssertEqual(true, LinuxKeyCodeMapper.TryMap(KeyCode.VcSpace, out var linuxSpaceKeyCode), "Auto weapon swap should map space");
        AssertEqual(57, linuxSpaceKeyCode, "Space should map to Linux KEY_SPACE");
        AssertEqual(true, WindowManager.IsActiveWindowProcess(1234, "1234\n"), "Active window should match the same process id");
        AssertEqual(false, WindowManager.IsActiveWindowProcess(1234, "4321\n"), "Active window should reject another process id");
        AssertEqual(false, WindowManager.IsActiveWindowProcess(1234, string.Empty), "Active window should reject missing process id output");
        AssertEqual(
            true,
            WindowManager.IsActiveWindowProcess(
                2665,
                "{\"id\":10,\"title\":\"Amazon - Zen Browser\",\"app_id\":\"zen\",\"pid\":2665,\"is_focused\":true}"),
            "Active window should match Niri focused-window JSON");
        AssertEqual(
            false,
            WindowManager.IsActiveWindowProcess(
                1234,
                "{\"id\":10,\"title\":\"Amazon - Zen Browser\",\"app_id\":\"zen\",\"pid\":2665,\"is_focused\":true}"),
            "Active window should reject Niri focused-window JSON for another process");
        AssertEqual(
            true,
            WindowManager.IsActiveWindowProcess(
                75186,
                "{\"id\":12,\"title\":\"Path of Exile 2\",\"app_id\":\"steam_app_2694490\",\"pid\":1896,\"is_focused\":true}"),
            "Active window should match Niri Xwayland proxy windows for Path of Exile 2");
        AssertEqual(
            false,
            WindowManager.IsActiveWindowProcess(
                75186,
                "{\"id\":10,\"title\":\"Path of Exile 2 guide - Zen Browser\",\"app_id\":\"zen\",\"pid\":2665,\"is_focused\":true}"),
            "Active window should reject browser windows with Path of Exile in the title");
        AssertEqual(
            true,
            WindowManager.IsActiveWindowProcess(
                2665,
                "{\"nodes\":[{\"nodes\":[{\"focused\":true,\"pid\":2665}]}]}"),
            "Active window should match Sway focused tree JSON");
        AssertEqual(
            false,
            WindowManager.IsActiveWindowProcess(
                1234,
                "{\"nodes\":[{\"nodes\":[{\"focused\":true,\"pid\":2665}]}]}"),
            "Active window should reject Sway focused tree JSON for another process");

        var autoWeaponSwapMessage = new AutoWeaponSwapConfigMessage(true, (int)KeyCode.VcX, 200);
        AssertEqual(true, autoWeaponSwapMessage.Enabled, "Auto weapon swap IPC message should carry enabled state");
        AssertEqual(typeof(int), ((object)autoWeaponSwapMessage.KeyCode).GetType(), "Auto weapon swap IPC key should be encoded as an AOT-safe primitive");
        AssertEqual((int)KeyCode.VcX, Convert.ToInt32(autoWeaponSwapMessage.KeyCode), "Auto weapon swap IPC message should carry the configured key");
        AssertEqual(200, autoWeaponSwapMessage.BaseIntervalMs, "Auto weapon swap IPC message should carry the base interval");
        AssertEqual(
            autoWeaponSwapMessage,
            MessagePackSerializer.Deserialize<IpcMessage>(MessagePackSerializer.Serialize<IpcMessage>(autoWeaponSwapMessage)),
            "Auto weapon swap IPC message should serialize through the IpcMessage union");

        var builtInChatMessage = new ChatCommandMessage("/exit");
        AssertEqual(0, builtInChatMessage.MinimumSubmitDelayMs, "Built-in chat commands should keep the current submit timing");
        var customChatMessage = new ChatCommandMessage("/hideout", defaultConfig.ChatCommandHotkeys[3].MinimumSubmitDelayMs);
        AssertEqual(100, customChatMessage.MinimumSubmitDelayMs, "Custom chat command messages should carry the custom submit delay");

        var blankCommand = new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF8), " ");
        AssertEqual(false, blankCommand.Matches(KeyCode.VcF8, false, false, false), "Blank custom commands should not fire");

        var disabledCustomCommands = ChatCommandHotkey.Normalize(
            [
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF2), " "),
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF3), "/remaining"),
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF4), "/deaths"),
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF5), "/hideout"),
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF6), "/leave"),
            ]);
        AssertEqual(" ", disabledCustomCommands[0].Command, "Disabled custom commands should stay blank after normalization");
        AssertEqual(false, disabledCustomCommands[0].Matches(KeyCode.VcF2, false, false, false), "Disabled normalized custom commands should not fire");

        var migratedCustomCommands = ChatCommandHotkey.Normalize(
            [
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF5), "/hideout"),
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF3), "/remaining"),
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF4), "/leave"),
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF7), "/invite @last"),
                new ChatCommandHotkey(new HotkeyCombo(KeyCode.VcF8), "/kick @last"),
            ]);
        AssertEqual(KeyCode.VcF2, migratedCustomCommands[0].Hotkey.Key, "Previous exact defaults should migrate first row to F2");
        AssertEqual("/played", migratedCustomCommands[0].Command, "Previous exact defaults should migrate first row to played");
        AssertEqual("/remaining", migratedCustomCommands[1].Command, "Previous exact defaults should keep remaining");
        AssertEqual("/deaths", migratedCustomCommands[2].Command, "Previous exact defaults should migrate third row to deaths");
        AssertEqual("/hideout", migratedCustomCommands[3].Command, "Previous exact defaults should migrate fourth row to hideout");
        AssertEqual("/leave", migratedCustomCommands[4].Command, "Previous exact defaults should migrate fifth row to leave");

        var configurationWindowXaml = File.ReadAllText("PoE-Kompanion/Views/ConfigurationWindow.axaml");
        AssertEqual(
            true,
            configurationWindowXaml.Contains("Width=\"400\""),
            "Settings window should keep the original width");
        AssertEqual(
            true,
            configurationWindowXaml.Contains("Content=\"Custom Commands:\""),
            "Settings should label the custom command section");
        AssertEqual(
            true,
            configurationWindowXaml.Contains("ColumnDefinitions=\"200,150\""),
            "Settings should put the custom command input field before the shortcut picker");
        AssertEqual(
            false,
            configurationWindowXaml.Contains("Content=\"Command 1:\"") ||
            configurationWindowXaml.Contains("Content=\"Command 2:\"") ||
            configurationWindowXaml.Contains("Content=\"Command 3:\"") ||
            configurationWindowXaml.Contains("Content=\"Command 4:\""),
            "Settings should not show numbered custom command labels");
        AssertEqual(
            false,
            configurationWindowXaml.Contains("Name=\"HideoutHotkeyPicker\"") ||
            configurationWindowXaml.Contains("Content=\"Hideout:\""),
            "Hideout should be configured through custom commands");
        AssertEqual(
            5,
            CountOccurrences(configurationWindowXaml, "MinWidth=\"190\""),
            "Settings should expose one command input field per custom command");
        AssertEqual(
            true,
            configurationWindowXaml.Contains("PlaceholderText=\"/played\"") &&
            configurationWindowXaml.Contains("PlaceholderText=\"/deaths\"") &&
            configurationWindowXaml.Contains("PlaceholderText=\"/hideout\"") &&
            configurationWindowXaml.Contains("PlaceholderText=\"/leave\""),
            "Settings should show the current default custom command placeholders");
        AssertEqual(
            true,
            configurationWindowXaml.Contains("Content=\"Auto Weapon Swap:\""),
            "Settings should expose the auto weapon swap section");
        AssertEqual(
            true,
            configurationWindowXaml.Contains("Name=\"AutoWeaponSwapKeyPicker\""),
            "Settings should expose the auto weapon swap key picker");
        AssertEqual(
            true,
            configurationWindowXaml.Contains("Name=\"AutoWeaponSwapToggleHotkeyPicker\""),
            "Settings should expose the auto weapon swap toggle key picker");
        AssertEqual(
            false,
            configurationWindowXaml.Contains("<CheckBox") ||
            configurationWindowXaml.Contains("Content=\"Enabled\"") ||
            configurationWindowXaml.Contains("AutoWeaponSwapEnabled"),
            "Settings should not show an auto weapon swap checkbox when a toggle hotkey exists");
        AssertEqual(
            true,
            configurationWindowXaml.Contains("<Slider") &&
            configurationWindowXaml.Contains("Minimum=\"300\"") &&
            configurationWindowXaml.Contains("Maximum=\"1000\""),
            "Settings should expose a 300ms to 1000ms auto weapon swap interval slider");

        var appXaml = File.ReadAllText("PoE-Kompanion/App.axaml");
        AssertEqual(
            true,
            appXaml.Contains("<SimpleTheme />"),
            "App should include a built-in theme so TextBox controls render");

        var themeXaml = File.ReadAllText("PoE-Kompanion/Styles/PoETheme.axaml");
        AssertEqual(
            false,
            themeXaml.Contains("Style Selector=\"CheckBox\""),
            "Settings should not carry a checkbox style when no checkbox is shown");
        AssertEqual(
            true,
            themeXaml.Contains("Style Selector=\"Slider\""),
            "Settings should style the auto weapon swap range selector");

        var inputBlockerSource = File.ReadAllText("PoE-Kompanion/Views/InputBlockerOverlay.axaml.cs");
        AssertEqual(
            true,
            inputBlockerSource.Contains("public bool PositionOverPoEWindow"),
            "Input blocker positioning should report whether the overlay can be placed over PoE");
        var configurationWindowSource = File.ReadAllText("PoE-Kompanion/Views/ConfigurationWindow.axaml.cs");
        AssertEqual(
            true,
            configurationWindowSource.Contains("if (!overlay.PositionOverPoEWindow(poeProcessId.Value)) return;"),
            "Settings should not show the input blocker overlay when PoE window bounds cannot be resolved");
        AssertEqual(
            false,
            configurationWindowSource.Contains("AutoWeaponSwapEnabled"),
            "Settings code-behind should not expose an unused auto weapon swap checkbox binding");

        var keyPressTracker = new KeyPressTracker();
        AssertEqual(true, keyPressTracker.TryPress(KeyCode.VcF5), "First F5 press should be accepted");
        AssertEqual(false, keyPressTracker.TryPress(KeyCode.VcF5), "Repeated F5 key-down should be ignored");
        keyPressTracker.Release(KeyCode.VcF5);
        AssertEqual(true, keyPressTracker.TryPress(KeyCode.VcF5), "F5 should be accepted again after release");
        keyPressTracker.Clear();
        AssertEqual(false, keyPressTracker.IsControlPressed, "Clearing pressed keys should clear control state");
        keyPressTracker.TryPress(KeyCode.VcLeftControl);
        keyPressTracker.TryPress(KeyCode.VcRightControl);
        keyPressTracker.Release(KeyCode.VcLeftControl);
        AssertEqual(true, keyPressTracker.IsControlPressed, "Control should remain pressed while either control key is down");
        keyPressTracker.Release(KeyCode.VcRightControl);
        AssertEqual(false, keyPressTracker.IsControlPressed, "Control should be released after both control keys are up");

        var hotkeyCaptureCoordinator = new HotkeyCaptureCoordinator();
        AssertEqual(
            true,
            hotkeyCaptureCoordinator.TryStartCapture(out var captureTask),
            "The first shortcut picker should acquire keyboard capture");
        AssertEqual(
            false,
            hotkeyCaptureCoordinator.TryStartCapture(out _),
            "A second shortcut picker should not acquire keyboard capture concurrently");
        AssertEqual(
            true,
            hotkeyCaptureCoordinator.HandleKeyPressed(KeyCode.VcLeftControl, true, false, false),
            "Modifier presses should be consumed by active shortcut capture");
        AssertEqual(false, captureTask.IsCompleted, "Modifier presses should not finish shortcut capture");
        AssertEqual(
            true,
            hotkeyCaptureCoordinator.HandleKeyPressed(KeyCode.VcF8, true, false, false),
            "A non-modifier press should finish active shortcut capture");
        AssertEqual(
            new HotkeyCombo(KeyCode.VcF8, ctrl: true),
            captureTask.GetAwaiter().GetResult(),
            "Shortcut capture should preserve the pressed modifiers");
        AssertEqual(
            false,
            hotkeyCaptureCoordinator.HandleKeyPressed(KeyCode.VcF9, false, false, false),
            "Key presses should return to normal handling after capture finishes");

        AssertEqual(
            true,
            hotkeyCaptureCoordinator.TryStartCapture(out var cancelledCaptureTask),
            "Shortcut capture should restart after completion");
        hotkeyCaptureCoordinator.HandleKeyPressed(KeyCode.VcEscape, false, false, false);
        AssertEqual<HotkeyCombo?>(
            null,
            cancelledCaptureTask.GetAwaiter().GetResult(),
            "Escape should cancel shortcut capture");
        AssertEqual(
            true,
            hotkeyCaptureCoordinator.TryStartCapture(out var closedWindowCaptureTask),
            "Shortcut capture should restart after Escape");
        hotkeyCaptureCoordinator.CancelCapture();
        AssertEqual<HotkeyCombo?>(
            null,
            closedWindowCaptureTask.GetAwaiter().GetResult(),
            "Closing settings should cancel active shortcut capture");

        var appSource = File.ReadAllText("PoE-Kompanion/App.axaml.cs");
        AssertEqual(
            true,
            appSource.Contains("ToggleAutoWeaponSwapAsync"),
            "App should expose auto weapon swap toggle hotkey handling");
        AssertEqual(
            true,
            appSource.Contains("new EventLoopGlobalHook(GlobalHookType.Keyboard)"),
            "The application should use a keyboard-only non-blocking event-loop global hook");
        var hotkeyPickerSource = File.ReadAllText("PoE-Kompanion/Controls/HotkeyPickerButton.axaml.cs");
        AssertEqual(
            false,
            hotkeyPickerSource.Contains("SimpleGlobalHook") || hotkeyPickerSource.Contains("EventLoopGlobalHook"),
            "Shortcut pickers should share the application hook instead of creating another global hook");
        AssertEqual(
            false,
            configurationWindowSource.Contains("StopMainHookAsync"),
            "Opening settings should keep the single application hook alive");

        var poeTrackerSource = File.ReadAllText("PoE-Kompanion/PoETracker.cs");
        AssertEqual(
            true,
            poeTrackerSource.Contains("WindowManager.IsActiveWindowProcess(this.poeProcess.Id)"),
            "Auto weapon swap should only press while the active window belongs to PoE");
        var windowManagerSource = File.ReadAllText("PoE-Kompanion/WindowManager.cs");
        AssertEqual(
            true,
            windowManagerSource.Contains("hyprctl") &&
            windowManagerSource.Contains("activewindow"),
            "Active window detection should support Hyprland focused-window lookup");
        AssertEqual(
            true,
            windowManagerSource.Contains("swaymsg") &&
            windowManagerSource.Contains("get_tree"),
            "Active window detection should support Sway focused-window lookup");

        return 0;
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static void AssertMatches(string processName, string commandLine, string message)
    {
        if (PoEProcessDetector.IsPathOfExileProcess(processName, commandLine)) return;

        throw new InvalidOperationException(message);
    }

    private static void AssertDoesNotMatch(string processName, string commandLine, string message)
    {
        if (!PoEProcessDetector.IsPathOfExileProcess(processName, commandLine)) return;

        throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (Equals(expected, actual)) return;

        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }

    private static void AssertSequenceEqual(string[] expected, System.Collections.ObjectModel.Collection<string> actual, string message)
    {
        if (expected.Length == actual.Count && expected.Select((value, index) => actual[index] == value).All(matches => matches)) return;

        throw new InvalidOperationException($"{message}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
    }
}
