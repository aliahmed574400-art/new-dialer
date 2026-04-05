# NewDialer

NewDialer is a production-oriented outbound calling platform built as a Windows desktop client plus an ASP.NET Core backend with PostgreSQL.

## Solution structure

- `src/NewDialer.Desktop`: WPF desktop app for agents, admins, and developer operations
- `src/NewDialer.Api`: backend API for authentication, tenancy, subscriptions, and reporting
- `src/NewDialer.Domain`: core business entities and enums
- `src/NewDialer.Application`: business rules and integration abstractions
- `src/NewDialer.Infrastructure`: PostgreSQL, Excel, and Zoom integration implementations
- `src/NewDialer.Contracts`: DTOs shared by API and desktop layers
- `tests/NewDialer.Domain.Tests`: business rule tests
- `docs/architecture.md`: product architecture, assumptions, and milestone plan

## Current status

This repository now contains the initial production foundation:

- multi-project solution structure
- role-aware desktop shell for `Admin`, `Agent`, and `Developer`
- desktop sign-in and admin workspace creation flow wired to the backend API
- desktop lead sync, persisted callback loading, and live dial/hangup actions through the installed Zoom Workplace client
- desktop admin Excel import, import-batch visibility, and lead assignment controls
- live agent activity tracking for calls today, talk time, and check-in/check-out
- core domain model for leads, schedules, call attempts, agent sessions, and subscriptions
- tenant-aware authentication with admin signup, login, workspace keys, password hashing, and JWT access tokens
- Excel lead import with required-column validation and duplicate-phone skipping
- admin lead assignment APIs and agent assigned-lead retrieval
- scheduled-call create, list, and Excel export endpoints
- developer tenant overview endpoint for internal billing/trial monitoring
- subscription evaluation rules for 15-day trials and manual activation
- PostgreSQL-ready infrastructure scaffolding plus EF Core migrations
- Zoom desktop-control and Excel integration abstractions

## Build notes

The backend targets `.NET 8` and PostgreSQL. Package restore is required before the full solution can build:

```powershell
dotnet restore NewDialer.sln
dotnet build NewDialer.sln
```

To apply the current PostgreSQL schema:

```powershell
dotnet tool run dotnet-ef database update --project src/NewDialer.Infrastructure/NewDialer.Infrastructure.csproj --startup-project src/NewDialer.Api/NewDialer.Api.csproj
```

## Auth model

- each company gets a generated `workspace key`
- admins can sign in with email
- agents are expected to sign in with `username + workspace key + password`
- user emails are globally unique
- agent usernames are unique inside their tenant

## API endpoints

- `POST /api/auth/admin-signup`
- `POST /api/auth/login`
- `POST /api/auth/logout`
- `GET /api/auth/me`
- `GET /api/agents/performance`
- `GET /api/platform/overview`
- `POST /api/leads/import`
- `GET /api/leads`
- `GET /api/leads/assigned`
- `GET /api/leads/import-batches`
- `GET /api/leads/agents`
- `POST /api/leads/assignments`
- `GET /api/schedules`
- `POST /api/schedules`
- `GET /api/schedules/export`
- `POST /api/dialer/call/start`
- `POST /api/dialer/call/hangup`

## Desktop config

- desktop API configuration lives in `src/NewDialer.Desktop/appsettings.json`
- default local API URL: `http://localhost:5164/`
- if your API runs on a different URL or HTTPS port, update `Desktop:ApiBaseUrl`
- `Desktop:LaunchZoomWithDialer` starts the installed Zoom client when the dialer is opened
- `Desktop:ZoomUriScheme` defaults to `zoomphonecall`
- `Desktop:ZoomExecutablePath` can be set explicitly if Zoom is installed in a custom location

## Packaging

- `scripts/publish-desktop.ps1`: publishes the WPF desktop client for `win-x64`
- `scripts/publish-api.ps1`: publishes the backend API
- `scripts/build-installer.ps1`: publishes the desktop app and builds an Inno Setup installer when `ISCC.exe` is installed
- `installer/NewDialerDesktop.iss`: Inno Setup project for the desktop `.exe` installer

## Excel workflow

- admins upload `.xlsx` lead sheets with required columns:
  - `Name`
  - `Email`
  - `Phone` or `Phone No`
  - `Website`
  - `Service`
  - `Budget`
- upload can optionally assign imported leads to one default agent
- duplicate phone numbers already present in the same tenant are skipped
- agents can only retrieve leads assigned to their own account

## Zoom desktop workflow

- the desktop app now uses the locally installed Zoom Workplace client instead of backend-held Zoom credentials
- `Start Dialer` launches the registered `zoomphonecall:` handler on the local machine
- `Hang Up` sends Zoom's default `End current call` shortcut from the desktop app
- fully automatic calling depends on Zoom's `Automatically Call From 3rd Party Apps` policy and the user's Zoom consent settings
- the backend dialer endpoints now record call start/end activity for analytics and reporting only
- this design assumes the user is already signed in to Zoom Workplace on the same Windows machine

## Local database status

- I generated the EF Core migrations successfully
- I still could not apply them locally because PostgreSQL is not listening on `127.0.0.1:5432`
- on April 4, 2026, the official EDB PostgreSQL 17 Windows installer download was blocked from this machine with `403 Forbidden`, so PostgreSQL is still the remaining local prerequisite

See `docs/architecture.md` for the assumptions and next implementation milestones.
