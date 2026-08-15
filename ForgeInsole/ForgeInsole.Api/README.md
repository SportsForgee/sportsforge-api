# Forge Insole Telemetry API

A standalone telemetry service for Forge Insole devices, built as a separate bounded
context inside the SportsForge repo so it can be handed to an external partner
(Khoi Tech) and later extracted into its own repository with minimal friction.

It shares **no code, no database, and no auth scheme** with the main SportsForge
API (`backend/Api`) — the only thing "reused" is the telemetry-generation math,
which was ported from `hardware-integration/simulators/forge-insole-sim/index.mjs`
into `Services/TelemetryGenerator.cs` (IMU accel/gyro fields are new; the original
JS simulator never emitted those).

## Running locally

1. SQL Server (LocalDB or SQL Express) reachable at the connection string in
   `appsettings.Development.json` (`ForgeInsoleDb` — a distinct database from
   `SportsForgeDb`, may live on the same instance).
2. From `backend/ForgeInsole/ForgeInsole.Api`:
   ```
   dotnet run
   ```
   Migrations apply automatically on startup (`db.Database.Migrate()` in
   `Program.cs`), including the seed of 6 insoles (`forge-insole-01`..`06`).
3. API listens on `http://localhost:5290`. Swagger UI at `/swagger` in Development.

## Authentication

Every `/api/v1/insoles/*` endpoint requires an `X-Api-Key` header. In dev the key
is `ForgeInsole:PartnerApiKey` in `appsettings.Development.json`
(`ForgeInsole-Dev-PartnerKey-Khoi-2026`) — **fine for local-only use**. The real
partner key must **never** be committed: set it via an environment variable
(`ForgeInsole__PartnerApiKey`) or a secrets manager in any shared/production
environment, and rotate the dev key if it's ever pasted somewhere public.

## Endpoints (all under `/api/v1/insoles`, all require `X-Api-Key`)

| Method | Path                          | Description                                   |
|--------|-------------------------------|------------------------------------------------|
| GET    | `/`                            | List all insoles + metadata                    |
| GET    | `/{id}`                        | Single insole detail                           |
| GET    | `/{id}/telemetry?from=&to=&limit=` | Historical readings (limit defaults to 100, capped at 1000) |
| GET    | `/{id}/stream`                 | Live readings via Server-Sent Events (`text/event-stream`) |

`GET /{id}/stream` subscribes to the same in-memory broadcast the background
generator publishes to (`Services/TelemetryBroadcaster.cs`) — it does not run a
second generator, so what you see live always matches what lands in the DB.

## How the data is generated

`InsoleTelemetryHostedService` (a `BackgroundService`) ticks every
`ForgeInsole:GenerationIntervalMs` (default 2000ms), generates one reading per
`Active` insole via `TelemetryGenerator.NextReading()`, saves it, and publishes it
to any live SSE subscribers. Each hosted-service run is tracked as one
`SimulationSession` row.

## EXTRACTION PATH

This folder (`backend/ForgeInsole/`) is designed to be lifted out wholesale:

1. Copy `backend/ForgeInsole/` into a new repository root (keep the
   `ForgeInsole.Api/` + `ForgeInsole.Data/` layout, or flatten it — nothing
   inside references a path outside this folder).
2. The standalone `ForgeInsole.sln` at this folder's root already only
   references these two projects — `dotnet build`/`dotnet run` should work
   immediately in the new repo with no changes.
3. Move the `ForgeInsoleDb` connection string and `ForgeInsole:PartnerApiKey`
   out of `appsettings.Development.json` and into the new repo's own
   secrets/env-var setup.
4. Remove the two `<Project>` entries for `ForgeInsole.Api`/`ForgeInsole.Data`
   from `sportsforge.slnx` in the old repo.
5. Rotate the partner API key as part of the cutover (treat the old key as
   burned the moment the new repo exists independently).
6. Nothing else changes — there are no `ProjectReference`s into any
   SportsForge project, no shared `DbContext`, no shared auth scheme, and no
   foreign keys into `SportsForgeDb`. That boundary was enforced from the
   start specifically so this step would be this short.
