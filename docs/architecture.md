# NewDialer Architecture

## Product goal

Build a production-grade outbound sales dialer with:

- automated sequential dialing from imported Excel leads
- Zoom Workplace desktop call control for dial and hangup
- admin and agent role separation
- schedule management and export
- subscription, 15-day trial, and developer override controls
- Windows desktop installation for end users

## Recommended architecture

### Desktop client

- Technology: `WPF` on `.NET 8`
- Purpose: agent/admin/developer UI, local call control, secure login, and responsive desktop workflow

### Backend API

- Technology: `ASP.NET Core Web API`
- Purpose: authentication, tenant isolation, subscriptions, lead ownership, reporting, and developer oversight

### Database

- Technology: `PostgreSQL`
- Access: `Entity Framework Core` with `Npgsql`

### Excel processing

- Technology: `ClosedXML`
- Purpose: import lead sheets and export scheduled-call sheets without requiring Microsoft Excel to be installed

### Installer

- Packaging direction: `WiX Toolset` or `MSIX`
- Recommendation: start with `WiX` if you want a traditional installer EXE/MSI experience

## Core modules

### Automated dialer

- import Excel leads with columns: `Name`, `Email`, `Phone`, `Website`, `Service`, `Budget`
- queue leads for continuous outbound dialing
- show current lead in the dialer panel
- actions:
  - `Start Dialer`
  - `Hang Up`
  - `Pause`
  - `Resume`
  - `Stop`

### Scheduled calls

- open a modal to select date, time, timezone, and notes
- save schedule data centrally
- export the schedule list to Excel

### Admin panel

- upload and delete lead sheets
- manage agents
- manage subscription status
- inspect scheduled calls
- download data exports
- track calls per day, duration, and check-in/check-out

### Agent panel

- see assigned leads
- run the dialer
- hang up active calls
- schedule callbacks
- call scheduled leads one by one
- no upload, delete, lead editing, or lead copy/export permissions

### Developer panel

- see every tenant/admin account
- see trial start and end dates
- manually activate or extend subscriptions
- see renewals or upcoming end dates

## Important suggestions

- Treat this as a `desktop + cloud backend` product, not a single local-database app.
- Keep all subscription checks on the server.
- Use queue locking so two agents do not call the same lead at the same time.
- Keep Zoom call control on the desktop when the customer is already signed in to Zoom Workplace locally, and keep analytics on the backend.

## Assumptions in this scaffold

- one company account maps to one tenant
- agents only see leads assigned to them
- Zoom Workplace will be installed on the dialing PC and already signed in before use
- phase 1 billing supports both manual activation and future online billing integration
- scheduled-call export is an Excel workbook generated from database data
- the desktop app authenticates against a hosted API
- agent sign-in uses a generated tenant `workspace key` so usernames can stay tenant-scoped

## Confirmed product decisions

1. Agents only see leads assigned to them.
2. Zoom is controlled from the installed desktop client instead of backend-stored credentials.
3. Billing will support both manual activation and online billing.
4. The current implementation order is `auth + database`, then `Excel`, then `Zoom`, then `installer`.

## Current backend status

- admin self-signup creates:
  - tenant
  - admin user
  - 15-day trial subscription
- login supports:
  - email + password
  - username + workspace key + password
- JWT access tokens include:
  - user id
  - tenant id
  - role
  - workspace key
- initial PostgreSQL migration exists in `src/NewDialer.Infrastructure/Persistence/Migrations`
- lead import supports:
  - Excel validation for required columns
  - optional default-agent assignment at import time
  - duplicate-phone skipping inside a tenant
- lead APIs support:
  - admin lead listing
  - admin import batch listing
  - admin bulk lead assignment
  - agent assigned-lead retrieval
- schedule APIs support:
  - Excel export of scheduled calls
- dialer APIs now support:
  - desktop-triggered call activity logging
  - agent/admin dialer actions through the API

## Current environment blocker

- the codebase is ready to apply EF Core migrations
- local database update is currently blocked because PostgreSQL was not running on `127.0.0.1:5432`
- once PostgreSQL is available, run:
  - `dotnet tool run dotnet-ef database update --project src/NewDialer.Infrastructure/NewDialer.Infrastructure.csproj --startup-project src/NewDialer.Api/NewDialer.Api.csproj`

## Zoom integration note

- based on Zoom's official desktop support guidance, the current implementation uses:
  - the registered `zoomphonecall:` protocol to open dialing in the installed Zoom client
  - Zoom's documented desktop `End current call` shortcut for local hangup
- fully automatic call placement also depends on Zoom's `Automatically Call From 3rd Party Apps` account policy and the user's consent duration inside Zoom
- I did not confirm a public unauthenticated desktop hangup API for arbitrary external apps, so the backend no longer depends on Zoom credentials for this phase
