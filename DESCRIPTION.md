# Gas Pressure Components: Container (Server), Input, Output

A distributed simulation of a sealed gas container where pressure evolves from temperature and mass. The server maintains container state and applies periodic temperature changes. Independent input and output components connect over a message broker to add or remove gas mass based on pressure thresholds. Communication uses request/response messages with correlation IDs over RabbitMQ.

Core behavior:
- Temperature changes randomly every 2 seconds in the server.
- Pressure is computed from mass and temperature using an idealized formula.
- Input components add mass only when pressure is below a lower limit.
- Output components remove mass only when pressure is above an upper limit.
- If pressure drops below an implosion limit or exceeds an explosion limit, the container is destroyed and the simulation resets.

Components:
- GasPressure (server/state/logic)
- GasPressureContract (interfaces and message contract)
- RabbitMQGasPressure (input client)
- Output (output client)

Message flow:
- Direct exchange: `GasPressure.Exchange`
- Server queue: `GasPressure.Service`
- Each client has a unique reply queue (auto-generated per instance)
- Request/response messages: `Call_Method` -> `Result_Method` with a matching CorrelationId
