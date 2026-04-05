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
    private const ushort VkControl = 0x11;
    private const ushort VkShift = 0x10;
    private const ushort VkE = 0x45;
    private const uint KeyEventFKeyUp = 0x0002;
    private const int SwRestore = 9;

    private readonly DesktopAppOptions _options;
    private string? _cachedExecutablePath;

    public ZoomDesktopDialerClient(DesktopAppOptions options)
    {
        _options = options;
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken)
    {
        if (!_options.LaunchZoomWithDialer || IsZoomRunning())
        {
            return;
        }

        var executablePath = DiscoverExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return;
        }

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
        if (string.IsNullOrWhiteSpace(normalizedPhoneNumber))
        {
            throw new InvalidOperationException("A valid phone number is required before Zoom can dial it.");
        }

        await WarmUpAsync(cancellationToken);

        var uriScheme = string.IsNullOrWhiteSpace(_options.ZoomUriScheme) ? "zoomphonecall" : _options.ZoomUriScheme.Trim();
        var zoomUri = $"{uriScheme}:{Uri.EscapeDataString(normalizedPhoneNumber)}";
        if (!TryLaunchZoomUrl(zoomUri))
        {
            throw new InvalidOperationException(
                "Zoom desktop dialing is not available. Confirm Zoom Workplace is installed and the zoomphonecall protocol is registered on this machine.");
        }

        await DelayAsync(_options.ZoomActionDelayMs, cancellationToken);
    }

    public async Task HangUpAsync(CancellationToken cancellationToken)
    {
        TryActivateZoomWindow();
        await DelayAsync(_options.ZoomActionDelayMs, cancellationToken);
        SendKeyChord(VkControl, VkShift, VkE);
    }

    private bool TryLaunchZoomUrl(string zoomUri)
    {
        var executablePath = DiscoverExecutablePath();
        if (!string.IsNullOrWhiteSpace(executablePath))
        {
            return LaunchProcess(new ProcessStartInfo
            {
                FileName = executablePath,
                Arguments = $"--url=\"{zoomUri}\"",
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
            });
        }

        return LaunchProcess(new ProcessStartInfo
        {
            FileName = zoomUri,
            UseShellExecute = true,
        });
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
            if (File.Exists(normalizedPath))
            {
                _cachedExecutablePath = normalizedPath;
                return _cachedExecutablePath;
            }
        }

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
        foreach (var character in phoneNumber)
        {
            if (char.IsDigit(character) || character == '+')
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
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
