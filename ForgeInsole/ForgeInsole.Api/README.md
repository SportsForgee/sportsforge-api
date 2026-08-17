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
| GET    | `/`                            | List all insoles + metadata (incl. `source`: `Simulated`/`Device`) |
| GET    | `/devices`                     | Live connection state of each configured physical insole |
| GET    | `/{id}`                        | Single insole detail                           |
| GET    | `/{id}/telemetry?from=&to=&limit=` | Historical readings (limit defaults to 100, capped at 1000) |
| GET    | `/{id}/stream`                 | Live readings via Server-Sent Events (`text/event-stream`) |

`GET /{id}/stream` subscribes to the same in-memory broadcast the background
generator publishes to (`Services/TelemetryBroadcaster.cs`) — it does not run a
second generator, so what you see live always matches what lands in the DB.

## Where the data comes from

Two sources, both landing in the same tables and the same SSE fan-out. Every
insole and every reading carries a `source` field so you can always tell which:

**Simulated** — `InsoleTelemetryHostedService` ticks every
`ForgeInsole:GenerationIntervalMs` (default 2000ms), generates one reading per
insole that is `Active` **and** `Source == Simulated` via
`TelemetryGenerator.NextReading()`, saves it, and publishes it to any live SSE
subscribers. Each hosted-service run is tracked as one `SimulationSession` row.

**Real hardware** — `DeviceIngestHostedService` connects out to each configured
ESP32. See the next section.

## Streaming from a real Forge Insole (ESP32, WiFi firmware)

The WiFi firmware is a WebSocket *server* on port 81 with no outbound HTTP
client, so **this API connects out to the board** — no firmware changes needed.
The only requirement is that the machine running this API can reach the ESP32 on
the network.

### 1. Get the board on WiFi and note its address

Flash the WiFi sketch, open Serial at 115200, and read the banner it prints every
5 seconds:

```
[net] WiFi OK "DANIEL"  192.168.1.42  -51 dBm  |  dashboard http://192.168.1.42/  |  ws://192.168.1.42:81  |  0 client(s), 0 frames sent
```

Confirm it independently before involving this API at all — open
`http://192.168.1.42/` in a browser. You should get a plain-text status page.

### 2. Point the API at it

In `appsettings.Development.json`:

```jsonc
"ForgeInsole": {
  "DeviceIngest": {
    "Devices": [
      { "Host": "forge-insole-l.local", "WsPort": 81, "Password": "112233" }
    ]
  }
}
```

`Host` takes the firmware's mDNS name (`MDNS_HOST` in the sketch, resolvable on
Windows 10+/macOS) **or** a plain IP. Use the IP if `.local` doesn't resolve —
many guest and corporate networks filter mDNS. `Password` must match
`DEVICE_PASSWORD` in the sketch.

Add a second entry for the right-foot unit; each device connects and reconnects
independently.

For a second insole or a production key, prefer env vars over the file:

```
ForgeInsole__DeviceIngest__Devices__1__Host=192.168.1.43
ForgeInsole__DeviceIngest__Devices__1__Password=…
```

### 3. Run it and check the connection

```
dotnet run
```

```
curl -H "X-Api-Key: ForgeInsole-Dev-PartnerKey-Khoi-2026" http://localhost:5290/api/v1/insoles/devices
```

`state` walks `Connecting` → `Authenticating` → `Streaming`. When something is
wrong, `lastError` says what:

| `lastError` | Meaning |
|---|---|
| `Unable to connect to the remote server` | Wrong host/IP, board off, or a different subnet |
| `device rejected the configured password …` | `Password` ≠ `DEVICE_PASSWORD` |
| `no frame received for 15s …` | Connected, then went silent — usually the brown-out the firmware warns about (check the 3.3V rail / add a 100µF cap) |

`mpuOk: false` or `mpuStalled: true` means the board is streaming but its IMU is
not healthy — accel/gyro values will be flat zeros. Pressure data is unaffected.

### 4. The device registers itself

On its first frame the device upserts its own `Insole` row from the identity in
the firmware (`DEVICE_ID`, `DEVICE_NAME`, `UNIT_FOOT`, `FW_VERSION`) — so
`FRG-2026-P01` appears in `GET /api/v1/insoles` with `"source": "Device"`
alongside the six seeded simulated ones. Set `InsoleId` in config to file its
readings under a different id instead.

Marking an insole `Device` is also what stops the simulator generating fake
readings for that same id. To turn simulation off entirely, set
`ForgeInsole:SimulationEnabled=false`.

### Sampling and mapping caveats

- SSE subscribers get **every** frame at the firmware's full 10 Hz. Only DB
  writes are throttled (`PersistIntervalMs`, default 1s), and each window stores
  its **most-loaded** frame rather than whichever arrived on the tick — a
  fixed-period sample aliases against the step cycle and would mostly record the
  foot mid-swing.
- The firmware has four pressure zones; this API's schema has three. `midfoot` is
  the mean of the two arch sensors.
- `cadence` is **single-foot** steps/min from the device, roughly half the
  two-foot cadence the simulator produces.
- `impactForce` from a device is an **uncalibrated relative load index**, not
  body weights — FSRs are not load cells.
- `strideAsymmetryPct` from a device is medial/lateral imbalance *within* that
  foot; true stride asymmetry needs both feet compared.
- `timestamp` is stamped on arrival; the ESP32 reports `millis()` since boot and
  has no RTC.

Full field-by-field mapping: [`docs/forge-insole/ARCHITECTURE.md` §9](../../../docs/forge-insole/ARCHITECTURE.md).

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
