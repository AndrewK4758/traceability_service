# How to Implement

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
*Note: The PostgreSQL database will be automatically migrated on startup.*

### Local Development (Windows PowerShell)
If you want to run the service directly on your machine:
```powershell
# Ensure NATS is running on localhost:4222
$env:NATS_URL="nats://localhost:4222"
dotnet build
dotnet run
```
*Note: The PostgreSQL database will be automatically migrated on startup.*
