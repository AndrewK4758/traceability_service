
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using TraceabilityService.Data;
using TraceabilityService.Services;
using TraceabilityService.Workers;

var builder = WebApplication.CreateBuilder(args);

// Add NATS Client
var natsUrl = builder.Configuration["NATS_URL"] ?? "nats://localhost:4222";
builder.Services.AddSingleton<INatsConnection>(provider => 
    new NatsConnection(new NatsOpts { Url = natsUrl }));

// Add Entity Framework Core (SQLite for local dev/testing)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=traceability.db";
builder.Services.AddDbContext<TraceDbContext>(options =>
    options.UseSqlite(connectionString));

// Add Grpc
builder.Services.AddGrpc();

// Add Background Workers
builder.Services.AddHostedService<NatsTraceSubscriber>();

var app = builder.Build();

// Auto-migrate the database on startup (for dev convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TraceDbContext>();
    db.Database.EnsureCreated();
}

app.MapGrpcService<TraceabilityQueryServiceImpl>();

app.MapGet("/", () => "Traceability gRPC Service is running.");

app.Run();
