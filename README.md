# Traceability Service

The **Traceability Service** is a decoupled .NET 10 background worker and gRPC service designed to provide centralized auditing, logging, and traceability across the entire document processing ecosystem.

## Architecture
This service uses an Event-Driven Architecture to decouple itself from the rest of the application:
1. **NATS JetStream (Worker)**: It runs a `.NET BackgroundService` that subscribes to the wildcard `trace.>` subject on the NATS broker. Any microservice (Go, Node, .NET) can emit JSON events to this topic, and the Traceability Service will automatically parse and persist them without blocking the publisher.
2. **Pomelo Entity Framework Core (Database)**: It uses MariaDB to persist `TraceEventRecord` objects. The schema uses database indexing on `JobId`, `CorrelationId`, and `Timestamp` for lightning-fast querying.
3. **gRPC (Querying)**: It exposes a gRPC endpoint (`TraceabilityQueryService`) for frontend client apps to query the status of a specific job or an entire correlation ID trace.

## Requirements
* .NET 10.0 SDK
* NATS Server

## 🚀 Running the Service

### Docker (All Platforms - Recommended)
This service is fully integrated into the root `docker-compose.yml`. To start the entire stack (AI Service, Orchestrator, Traceability, and NATS):
```bash
cd ..
docker-compose up --build -d
```

### Local Development (Linux / macOS)
If you want to run the service directly on your machine:
```bash
# Ensure NATS is running on localhost:4222
export NATS_URL="nats://localhost:4222"
dotnet build
dotnet run
```
*Note: The MariaDB database will be automatically migrated on startup.*

### Local Development (Windows PowerShell)
If you want to run the service directly on your machine:
```powershell
# Ensure NATS is running on localhost:4222
$env:NATS_URL="nats://localhost:4222"
dotnet build
dotnet run
```
*Note: The MariaDB database will be automatically migrated on startup.*

## gRPC Endpoints
The service implements `traceability.proto` with the following RPCs:
* `GetJobTrace(GetJobTraceRequest)` - Retrieve all events and the overall status for a specific Job ID.
* `GetCorrelationTrace(GetCorrelationTraceRequest)` - Retrieve a full timeline of events across all microservices for a specific Correlation ID.
