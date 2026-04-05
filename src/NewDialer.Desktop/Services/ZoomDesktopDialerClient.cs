using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using NewDialer.Desktop.Configuration;

namespace NewDialer.Desktop.Services;

public sealed class ZoomDesktopDialerClient
{
    private const string ZoomProcessName = "Zoom";
    private const int ShellExecuteSuccessThreshold = 32;
    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkE = 0x45;
    private const uint KeyEventFKeyUp = 0x0002;
    private const int SwRestore = 9;

    private readonly DesktopAppOptions _options;
    private readonly string _logPath = Path.Combine(AppContext.BaseDirectory, "dialer-debug.log");
    private string? _cachedExecutablePath;

    public ZoomDesktopDialerClient(DesktopAppOptions options)
    {
        _options = options;
    }

    public int AutoNextDialDelayMs => _options.AutoNextDialDelayMs <= 0 ? 3000 : _options.AutoNextDialDelayMs;

    public async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        Log("WarmUpAsync invoked.");

        if (!_options.LaunchZoomWithDialer || IsZoomRunning())
        {
            Log($"Warm up skipped. LaunchZoomWithDialer={_options.LaunchZoomWithDialer}, IsZoomRunning={IsZoomRunning()}.");
            return;
        }

        var executablePath = DiscoverExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Log("Zoom executable not found during warm up.");
            return;
        }

        Log($"Launching Zoom executable for warm up: {executablePath}");
        LaunchProcess(new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath),
        });

        await DelayAsync(_options.ZoomLaunchDelayMs, cancellationToken);
    }

    public async Task StartCallAsync(string phoneNumber, CancellationToken cancellationToken)
    {
        var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber);
        Log($"StartCallAsync received number '{phoneNumber}', normalized to '{normalizedPhoneNumber}'.");
        if (string.IsNullOrWhiteSpace(normalizedPhoneNumber))
        {
            throw new InvalidOperationException("A valid phone number is required before Zoom can dial it.");
        }

        await WarmUpAsync(cancellationToken);

        if (!TryLaunchDialRequest(normalizedPhoneNumber))
        {
            Log("TryLaunchDialRequest returned false.");
            throw new InvalidOperationException(
                "Zoom desktop dialing is not available. Confirm Zoom Workplace is installed and that Zoom is the default app for zoomphonecall, tel, or callto links on this machine.");
        }

        Log("Dial request launched. Attempting to activate Zoom window.");
        TryActivateZoomWindow();
        await DelayAsync(_options.ZoomActionDelayMs, cancellationToken);
    }

    public async Task HangUpAsync(CancellationToken cancellationToken)
    {
        TryActivateZoomWindow();
        await DelayAsync(_options.ZoomActionDelayMs, cancellationToken);
        SendKeyChord(VkControl, VkShift, VkE);
    }

    private bool TryLaunchDialRequest(string normalizedPhoneNumber)
    {
        foreach (var dialUri in BuildDialUris(normalizedPhoneNumber))
        {
            Log($"Trying ShellExecute dial URI: {dialUri}");
            if (LaunchUriWithShellExecute(dialUri))
            {
                Log($"ShellExecute accepted dial URI: {dialUri}");
                return true;
            }

            Log($"ShellExecute rejected dial URI: {dialUri}");
        }

        var executablePath = DiscoverExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            Log("Zoom executable could not be discovered for direct launch fallback.");
            return false;
        }

        foreach (var dialUri in BuildDialUris(normalizedPhoneNumber))
        {
            Log($"Trying direct Zoom launch with URI: {dialUri}");
            if (LaunchProcess(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--url=\"{dialUri}\"",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
            }))
            {
                Log($"Direct Zoom launch succeeded with URI: {dialUri}");
                return true;
            }
        }

        Log("Trying plain Zoom executable launch as final fallback.");
        var launched = LaunchProcess(new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executablePath),
        });

        Log($"Plain Zoom executable launch result: {launched}");
        return launched;
    }

    private IEnumerable<string> BuildDialUris(string normalizedPhoneNumber)
    {
        var preferredScheme = string.IsNullOrWhiteSpace(_options.ZoomUriScheme)
            ? "zoomphonecall"
            : _options.ZoomUriScheme.Trim().TrimEnd(':');

        var supportedSchemes = new[] { preferredScheme, "zoomphonecall", "tel", "callto" }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var scheme in supportedSchemes)
        {
            yield return FormatDialUri(scheme, normalizedPhoneNumber);
        }
    }

    private static string FormatDialUri(string scheme, string normalizedPhoneNumber)
    {
        return scheme.Equals("zoomphonecall", StringComparison.OrdinalIgnoreCase)
            ? $"{scheme}://{normalizedPhoneNumber}"
            : $"{scheme}:{normalizedPhoneNumber}";
    }

    private string? DiscoverExecutablePath()
    {
        if (!string.IsNullOrWhiteSpace(_cachedExecutablePath) && File.Exists(_cachedExecutablePath))
        {
            return _cachedExecutablePath;
        }

        var candidates = new[]
        {
            _options.ZoomExecutablePath,
            ReadZoomPathFromRegistry(@"HKEY_CLASSES_ROOT\zoomphonecall\shell\open\command"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Zoom", "bin", "Zoom.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Zoom", "bin", "Zoom.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Zoom", "bin", "Zoom.exe"),
        };

        foreach (var candidate in candidates.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var normalizedPath = candidate!.Trim().Trim('"');
            Log($"Checking Zoom executable candidate: {normalizedPath}");
            if (File.Exists(normalizedPath))
            {
                _cachedExecutablePath = normalizedPath;
                Log($"Using Zoom executable: {_cachedExecutablePath}");
                return _cachedExecutablePath;
            }
        }

        Log("No Zoom executable candidates were found.");
        return null;
    }

    private static string? ReadZoomPathFromRegistry(string keyPath)
    {
        var value = Registry.GetValue(keyPath, string.Empty, null) as string;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmedValue = value.Trim();
        if (trimmedValue.StartsWith("\"", StringComparison.Ordinal))
        {
            var closingQuoteIndex = trimmedValue.IndexOf('"', 1);
            if (closingQuoteIndex > 1)
            {
                return trimmedValue[1..closingQuoteIndex];
            }
        }

        var separatorIndex = trimmedValue.IndexOf(' ');
        return separatorIndex <= 0 ? trimmedValue : trimmedValue[..separatorIndex];
    }

    private static bool IsZoomRunning()
    {
        return Process.GetProcessesByName(ZoomProcessName).Any();
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(phoneNumber.Length);
        var hadLeadingPlus = phoneNumber.TrimStart().StartsWith("+", StringComparison.Ordinal);

        foreach (var character in phoneNumber)
        {
            if (char.IsDigit(character))
            {
                builder.Append(character);
            }
        }

        var digits = builder.ToString();
        if (string.IsNullOrWhiteSpace(digits))
        {
            return string.Empty;
        }

        if (hadLeadingPlus)
        {
            return $"+{digits}";
        }

        return digits.Length switch
        {
            10 => $"+1{digits}",
            11 when digits.StartsWith("1", StringComparison.Ordinal) => $"+{digits}",
            _ => $"+{digits}",
        };
    }

    private bool LaunchUriWithShellExecute(string dialUri)
    {
        try
        {
            var result = ShellExecute(IntPtr.Zero, "open", dialUri, null, null, SwRestore);
            var code = result.ToInt64();
            Log($"ShellExecute result for '{dialUri}' was {code}.");
            return code > ShellExecuteSuccessThreshold;
        }
        catch (Exception exception)
        {
            Log($"ShellExecute exception for '{dialUri}': {exception.Message}");
            return false;
        }
    }

    private static bool LaunchProcess(ProcessStartInfo startInfo)
    {
        try
        {
            Process.Start(startInfo);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void TryActivateZoomWindow()
    {
        var process = Process.GetProcessesByName(ZoomProcessName)
            .FirstOrDefault(x => x.MainWindowHandle != IntPtr.Zero);

        if (process is null)
        {
            return;
        }

        ShowWindow(process.MainWindowHandle, SwRestore);
        SetForegroundWindow(process.MainWindowHandle);
    }

    private void Log(string message)
    {
        try
        {
            var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(_logPath, line);
        }
        catch
        {
        }
    }

    private static async Task DelayAsync(int delayMilliseconds, CancellationToken cancellationToken)
    {
        if (delayMilliseconds <= 0)
        {
            return;
        }

        await Task.Delay(delayMilliseconds, cancellationToken);
    }

    private static void SendKeyChord(params ushort[] virtualKeys)
    {
        if (virtualKeys.Length == 0)
        {
            return;
        }

        var inputs = new INPUT[virtualKeys.Length * 2];
        var inputIndex = 0;

        foreach (var virtualKey in virtualKeys)
        {
            inputs[inputIndex++] = CreateKeyboardInput(virtualKey, keyUp: false);
        }

        foreach (var virtualKey in virtualKeys.Reverse())
        {
            inputs[inputIndex++] = CreateKeyboardInput(virtualKey, keyUp: true);
        }

        var sentInputCount = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sentInputCount == 0)
        {
            throw new InvalidOperationException(
                "Zoom desktop is open, but Windows blocked the End current call shortcut. Confirm Zoom allows the default Ctrl+Shift+E shortcut.");
        }
    }

    private static INPUT CreateKeyboardInput(ushort virtualKey, bool keyUp)
    {
        return new INPUT
        {
            Type = 1,
            Data = new InputUnion
            {
                Keyboard = new KEYBDINPUT
                {
                    VirtualKey = virtualKey,
                    ScanCode = 0,
                    Flags = keyUp ? KeyEventFKeyUp : 0,
                    Time = 0,
                    ExtraInfo = IntPtr.Zero,
                },
            },
        };
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int sizeOfInputStructure);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr windowHandle, int command);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr ShellExecute(
        IntPtr hwnd,
        string? operation,
        string file,
        string? parameters,
        string? directory,
        int showCommand);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }
}
