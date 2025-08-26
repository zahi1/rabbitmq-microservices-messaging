# Distributed Gas Pressure System Using RabbitMQ

A distributed simulation that models gas pressure in a sealed container. One process (server) owns the container state and applies periodic temperature changes. Independent processes (clients) connect over a message broker to add or remove gas mass depending on pressure limits. Communication uses RabbitMQ with request/response messages and correlation IDs.

## Overview

- The container’s temperature changes every 2 seconds.
- Pressure is computed from mass and temperature using an idealized formula.
- Input clients add mass only when pressure is below a lower limit.
- Output clients remove mass only when pressure is above an upper limit.
- If pressure drops below the implosion limit or exceeds the explosion limit, the container is destroyed and the simulation resets.

## Project structure / architecture

- `GasPressure` — server process
  - `Server.cs` bootstraps logging and hosts `GasPressureService`.
  - `GasPressureService.cs` exposes operations over RabbitMQ (request/response style).
  - `GasPressureLogic.cs` keeps the container state and logic (temperature, pressure, mass, limits, background temperature changes). Contracts don’t sleep; only the background task does.
- `GasPressureContract` — contracts shared by all components
  - `IGasPressureService.cs` and DTOs like `MassAdjustmentResult` and `RPCMessage`.
- `RabbitMQGasPressure` — input client process
  - `Client.cs` uses a `GasPressureClient` to call the server; when pressure < lower limit, it increases mass by a random amount.
- `Output` — output client process
  - `Client.cs` uses a `GasPressureClient` to call the server; when pressure > upper threshold, it decreases mass by a random amount.

### Message-based communication

- Exchange: `GasPressure.Exchange` (direct)
- Server queue: `GasPressure.Service` (bound to the exchange with the same routing key)
- Clients create a unique, exclusive reply queue per instance:
  - Input: `GasPressure.InputClient_<guid>`
  - Output: `GasPressure.OutputClient_<guid>`
- Request/response flow:
  1. Client publishes to `GasPressure.Service` with `BasicProperties.CorrelationId` and `ReplyTo` set to its own reply queue.
  2. Server handles actions like `Call_GetPressure`, `Call_IsDestroyed`, `Call_IncreaseMass`, `Call_DecreaseMass`.
  3. Server responds to `ReplyTo` with `Result_<Method>` and the same `CorrelationId`.
  4. Client filters replies by `CorrelationId` and `Result_<Method>` before completing the call.

### Component behavior (constraints enforced by the server)

- Input components are not allowed to add mass if pressure is at or above the lower (input) limit.
- Output components are not allowed to remove mass if pressure is at or below the upper (output) limit.
- Destruction condition: pressure below implosion limit or above explosion limit. After destruction, the server resets the container state.

Key parameters (in `GasPressure/GasPressureLogic.cs`):
- PressureLimit (lower): 100
- UpperPressureLimit: 150
- ExplosionLimit: 200
- ImplosionLimit: 10
- Temperature change period: every 2 seconds with a random delta in [-15, 15]

Note: Clients use simple thresholds in their local decision logic; the server is the source of truth and enforces limits. For example, the Output client may request removals above 100, but the server only allows removals above 150.

## How to run

Prerequisites:
- .NET 8 SDK
- RabbitMQ broker running locally

Start a local RabbitMQ broker (choose one):

- Homebrew (macOS):
```bash
brew install rabbitmq
brew services start rabbitmq
```

- Windows (PowerShell, requires Chocolatey):
```powershell
# In an elevated (Administrator) PowerShell
choco install rabbitmq -y

# Ensure the RabbitMQ Windows service is installed and running
rabbitmq-service.bat install
rabbitmq-service.bat start

# Optional: enable the Management UI at http://localhost:15672 (guest/guest)
rabbitmq-plugins enable rabbitmq_management
```

- Docker:
```bash
docker run -d --name rmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

Build the solution:
```bash
dotnet build RabbitMQGasPressure.sln
```

Run each component in its own terminal:

- Server:
```bash
dotnet run --project GasPressure/GasPressure.csproj
```

- Input client:
```bash
dotnet run --project RabbitMQGasPressure/Input.csproj
```

- Output client:
```bash
dotnet run --project Output/Output.csproj
```

You should see logs in each window showing temperature changes, pressure, and when mass is added/removed. The server logs will also indicate when the container is destroyed and reset.

## Design goals satisfied

- Clear, code-first contracts (`IGasPressureService`) separate from logic and transport.
- Operations in the service contract do not sleep; background work runs in a separate thread.
- Components are separate processes, operate in continuous cycles, and log to stdout.
- Message flow uses correlation IDs and per-client reply queues to support 1:N clients safely.
- No shared-state concurrency issues in the contract methods; state access is synchronized.

## Configuration

Tune limits in `GasPressure/GasPressureLogic.cs`:
- `PressureLimit`, `UpperPressureLimit`, `ExplosionLimit`, `ImplosionLimit`
- Initial mass and temperature
- Temperature change range and period

## Troubleshooting

- If clients appear idle, check the server logs and ensure RabbitMQ is running.
- Verify the exchange and queue exist via the RabbitMQ Management UI (http://localhost:15672, default guest/guest) when using the management image.
- If you restart RabbitMQ while components run, restart the apps so they recreate queues and consumers.

## License

MIT — see [`LICENSE`](./LICENSE).
