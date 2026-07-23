
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

// Add Entity Framework Core (PostgreSQL for infrastructural data)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Host=localhost;Database=artemis_ai;Username=ak;Password=ak";
builder.Services.AddDbContext<TraceDbContext>(options =>
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure();
        npgsqlOptions.MigrationsHistoryTable(Microsoft.EntityFrameworkCore.Migrations.HistoryRepository.DefaultTableName, "traceability");
    }));

// Add Grpc with increased max message size
var maxMessageSizeMb = builder.Configuration.GetValue<int?>("GRPC_MAX_MESSAGE_SIZE_MB") ?? 100;
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = maxMessageSizeMb * 1024 * 1024;
});

// Add Background Workers
builder.Services.AddHostedService<NatsTraceSubscriber>();

var app = builder.Build();

// Auto-migrate the database on startup (for dev convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TraceDbContext>();
    try 
    {
        db.Database.Migrate();
    } 
    catch (Exception ex) 
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.MapGrpcService<TraceabilityQueryServiceImpl>();

app.MapGet("/", () => "Traceability gRPC Service is running.");

app.Run();
