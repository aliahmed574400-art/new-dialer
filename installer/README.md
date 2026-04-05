# Desktop Installer

This folder contains the setup packaging scaffold for the WPF desktop client.

## Build steps

1. Publish the desktop app:

```powershell
.\scripts\publish-desktop.ps1
```

2. Build the setup installer with Inno Setup 6:

```powershell
.\scripts\build-installer.ps1
```

If `ISCC.exe` is installed in the standard Inno Setup 6 directory, the generated setup executable will be placed in `artifacts/installer`.

The build script also checks the user-local `winget` install path:

- `%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`

## Notes

- The installer packages the self-contained `win-x64` desktop publish output.
- Update `MyAppVersion`, publisher details, URL, and icons in `installer/NewDialerDesktop.iss` before production release.
- The desktop app reads its backend URL from `appsettings.json`, so the published installer includes that file too.
