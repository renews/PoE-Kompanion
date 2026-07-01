# Code Guidelines for PoEKompanion

## Git Workflow

- **Local commits allowed**: Create commits, branches, merges, etc. as needed
- **NEVER push**: Do not push to remote repositories under any circumstances
- **Phase-based workflow**: When working in phases:
  - Complete the phase implementation
  - Report to the user for validation
  - Wait for explicit user approval before committing
  - Only commit after user confirms the phase is acceptable
- The user handles all remote operations manually

## General Style

- **Prefer `var`**: Use `var` for local variable declarations instead of explicit types
- **Always use `this.` prefix**: All member access must be prefixed with `this.` for clarity
  ```csharp
  // Good
  this.currentHotkey = newHotkey;
  this.InitHook();

  // Bad
  currentHotkey = newHotkey;
  InitHook();
  ```
- **Naming conventions**:
  - Use `UPPER_SNAKE_CASE` for private const fields
  ```csharp
  // Good
  private const string HELPER_PATH = "/usr/local/libexec/poe-kompanion-kill-connection";

  // Bad
  private const string HelperPath = "/usr/local/libexec/poe-kompanion-kill-connection";
  ```
- **Method visibility**: Make methods `static` when they don't access instance state
  ```csharp
  // Good - static method that doesn't need instance
  private static void NotifyInitializationSuccess()
  {
      NotificationManager.SendInfo("initialized successfully!");
  }

  // Bad - instance method unnecessarily
  private void NotifyInitializationSuccess()
  {
      NotificationManager.SendInfo("initialized successfully!");
  }
  ```
- **Ternary expressions**: Use ternary expressions for simple conditional assignments
  ```csharp
  // Good - simple ternary
  return received == 0 ? null : MessagePackSerializer.Deserialize<IpcMessage>(buffer);

  // Good - ternary with complex expression on new line
  this.ipc?.SendAsync(proc?.Id is not null
      ? new NotificationMessage("Path of Exile process detected!", false)
      : new NotificationMessage("Path of Exile closed", false));

  // Bad - unnecessary if/else for simple return
  if (received == 0)
  {
      return null;
  }
  else
  {
      return MessagePackSerializer.Deserialize<IpcMessage>(buffer);
  }
  ```
- **Expression-bodied members**: Use expression bodies for simple methods/properties
  ```csharp
  // Good - simple expression body
  public static void SendInfo(string message) => Send(message, "normal");
  private static void Send(string message, string urgency) => Send("PoE Kompanion", message, urgency);

  // Acceptable - regular method body for clarity
  private static void Send(string title, string message, string urgency)
  {
      // method implementation
  }
  ```
- **Curly braces**:
  - **Always use curly braces** for all conditional statements and loops, except for single-line guard statements
  - **Guard statements exception**: Single-line conditions that return/throw should be on one line without curly braces
  ```csharp
  // Good - guard statement
  if (value is null) return;
  if (count < 0) throw new ArgumentException();

  // Good - regular conditional with braces
  if (oldHook is not null)
  {
      await Task.Run(() => oldHook.Dispose());
  }

  // Bad - multi-statement without braces
  if (oldHook is not null)
      await Task.Run(() => oldHook.Dispose());

  // Bad - guard with braces
  if (value is null)
  {
      return;
  }
  ```
- **Parameter order in records**: In MessagePack records, order parameters by importance/frequency of use
  ```csharp
  // Good - message first (most important), title last with default
  public sealed record NotificationMessage(
      [property: Key(0)] string Message,
      [property: Key(1)] bool IsError,
      [property: Key(2)] string Title = "PoE Kompanion"
  ) : IpcMessage;
  ```
- **Exception handling**: Silent exceptions are acceptable when failure is expected and non-critical
  ```csharp
  // Good - silent catch for non-critical notification failures
  catch (Exception) { /* nom */ }

  // Also acceptable with comment
  catch (Exception)
  {
      // Notification failure is non-critical, ignore
  }
  ```

## Documentation

- **No doc comments** unless specifically requested
- **Minimal comments**: Only comment non-trivial logic
- Code should be self-explanatory through clear naming

## Avalonia UI Patterns

- **Prefer data binding over direct control manipulation**: Use XAML bindings instead of finding controls in code-behind
  ```csharp
  // Good - use binding in XAML
  <controls:HotkeyPickerButton SelectedKeyCode="{Binding LogoutHotkey}" />

  // Bad - find control and manipulate directly
  var picker = this.FindControl<HotkeyPickerButton>("LogoutHotkeyPicker");
  picker.SelectedKeyCode = value;
  ```
- **Use Avalonia's StyledProperty system**: For custom controls, implement properties as `StyledProperty` for proper binding support
  ```csharp
  // Good - proper Avalonia property
  public static readonly StyledProperty<KeyCode> SelectedKeyCodeProperty =
      AvaloniaProperty.Register<HotkeyPickerButton, KeyCode>(
          nameof(SelectedKeyCode),
          KeyCode.VcBackQuote,
          defaultBindingMode: BindingMode.TwoWay);

  public KeyCode SelectedKeyCode
  {
      get => this.GetValue(SelectedKeyCodeProperty);
      set => this.SetValue(SelectedKeyCodeProperty, value);
  }
  ```
- **Property change notifications**: Use static constructor with `AddClassHandler` for property change callbacks
  ```csharp
  static HotkeyPickerButton()
  {
      SelectedKeyCodeProperty.Changed.AddClassHandler<HotkeyPickerButton>((control, args) =>
      {
          control.OnPropertyChanged(nameof(KeyDisplayName));
      });
  }
  ```
- **Element name binding for internal control properties**: Use `x:Name` and `{Binding #ElementName.Property}` for bindings within a control
  ```xml
  <UserControl x:Name="Root">
      <Button Content="{Binding #Root.KeyDisplayName}" />
  </UserControl>
  ```
- **Keep models simple**: Data models should be POCOs without UI concerns (no INotifyPropertyChanged on models)
- **Windows/Views as ViewModels**: Let windows/views handle INotifyPropertyChanged and act as their own view models for simple scenarios

## Asynchronous Programming

- **NEVER use `async void`**: Always return `Task` for async methods
  ```csharp
  // Good - returns Task
  private async Task SaveConfiguration() { ... }
  private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = this.SaveConfiguration();

  // Bad - async void
  private async void OnSaveClick(object? sender, RoutedEventArgs e) { ... }
  ```
- **Prefer async I/O**: Use async/await for all I/O operations whenever possible
  - Use `File.ReadAllTextAsync()` / `File.WriteAllTextAsync()` instead of synchronous variants
  - Use `JsonSerializer.SerializeAsync()` / `JsonSerializer.DeserializeAsync()` instead of synchronous variants
  - Use async Stream-based APIs whenever available
- **Parallelization**: Use parallelization when it's trivial to implement
- **IAsyncEnumerable**: Use `IAsyncEnumerable<T>` where it makes sense for streaming data
- **NEVER use arbitrary delays**: Do not use `Task.Delay()` or `Thread.Sleep()` with hardcoded durations as a workaround for synchronization
  ```csharp
  // Bad - arbitrary delay hoping something completes
  await Task.Delay(500);
  this.ipc = UnixSocketIpc.CreateClient();

  // Good - use proper async coordination mechanisms
  await this.serverReadyTaskCompletionSource.Task;
  this.ipc = UnixSocketIpc.CreateClient();

  // Good - wait for actual condition
  while (!File.Exists(socketPath))
  {
      await Task.Yield();
  }
  ```
  - Use `TaskCompletionSource<T>` for signaling between async operations
  - Use `SemaphoreSlim` for async-compatible locking
  - Use condition variables or polling with `Task.Yield()` if you must wait for a condition
  - Structure your code so dependencies are explicit, not time-based

## Complete Example

```csharp
// Good - follows all conventions
public class ConfigurationWindow : Window, INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private ConfigurationModel currentConfig;
    private KeyCode logoutHotkey;

    public KeyCode LogoutHotkey
    {
        get => this.logoutHotkey;
        set
        {
            if (this.logoutHotkey == value) return;
            this.logoutHotkey = value;
            this.OnPropertyChanged();
        }
    }

    public ConfigurationWindow()
    {
        this.InitializeComponent();
        this.DataContext = this;
        _ = this.LoadConfiguration();
    }

    private async Task LoadConfiguration()
    {
        this.currentConfig = await ConfigurationManager.LoadAsync();
        this.LogoutHotkey = this.currentConfig.LogoutHotkey;
    }

    private async Task SaveConfiguration()
    {
        this.currentConfig.LogoutHotkey = this.LogoutHotkey;
        await ConfigurationManager.SaveAsync(this.currentConfig);
        this.Close();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = this.SaveConfiguration();

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

// XAML with proper binding
<controls:HotkeyPickerButton SelectedKeyCode="{Binding LogoutHotkey}" />
```
