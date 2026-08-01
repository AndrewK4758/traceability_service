# Service Communication

## gRPC Endpoints
The service implements `traceability.proto` with the following RPCs:
* `GetJobTrace(GetJobTraceRequest)` - Retrieve all events and the overall status for a specific Job ID.
* `GetCorrelationTrace(GetCorrelationTraceRequest)` - Retrieve a full timeline of events across all microservices for a specific Correlation ID.

## 📜 Data Contracts & No Preemptive Serialization

This service adheres to the strict **No Preemptive Serialization** mandate. 
- All JSON-based structural state must be parsed natively using `google.protobuf.Struct` (or `structpb.Struct` in Go, `Dictionary<string, object>` in C#).
- Preemptive stringification (e.g. converting nested objects to strings before the final persistence layer) is forbidden to prevent schema drift, whitespace corruption, and nested parsing bugs.
- Boundary interfaces rely on typed object mappings rather than dynamic regex or fuzzy key lookups.
