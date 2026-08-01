# Core Components

The Traceability Service is a decoupled .NET 10 background worker and gRPC service designed to provide centralized auditing, logging, and traceability across the entire document processing ecosystem.

## 1. NATS JetStream (Worker)
Runs a `.NET BackgroundService` that subscribes to the wildcard `trace.>` subject on the NATS broker. Any microservice (Go, Node, .NET) can emit JSON events to this topic, and the Traceability Service will automatically parse and persist them without blocking the publisher.

## 2. Npgsql Entity Framework Core (Database)
Uses PostgreSQL to persist `TraceEventRecord` objects. The schema uses database indexing on `JobId`, `CorrelationId`, and `Timestamp` for lightning-fast querying.

## 3. gRPC (Querying)
Exposes a gRPC endpoint (`TraceabilityQueryService`) for frontend client apps to query the status of a specific job or an entire correlation ID trace.
