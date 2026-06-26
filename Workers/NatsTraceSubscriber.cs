
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using NATS.Client.Core;
using TraceabilityService.Data;
using TraceabilityService.Models;

namespace TraceabilityService.Workers;

public class NatsTraceSubscriber : BackgroundService
{
    private readonly ILogger<NatsTraceSubscriber> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly INatsConnection _natsConnection;

    public NatsTraceSubscriber(
        ILogger<NatsTraceSubscriber> logger, 
        IServiceProvider serviceProvider,
        INatsConnection natsConnection)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _natsConnection = natsConnection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Traceability NATS Subscriber starting up on subject 'trace.>'...");
        
        try
        {
            await foreach (var msg in _natsConnection.SubscribeAsync<byte[]>("trace.>", cancellationToken: stoppingToken))
            {
                if (msg.Data is null) continue;

                try
                {
                    var traceJson = System.Text.Encoding.UTF8.GetString(msg.Data);
                    _logger.LogDebug("Received trace event: {Json}", traceJson);

                    // Parse JSON payload dynamically or via JsonDocument (since the incoming JSON matches NATS payload structure)
                    using var doc = JsonDocument.Parse(traceJson);
                    var root = doc.RootElement;
                    
                    var eventRecord = new TraceEventRecord
                    {
                        EventId = root.GetProperty("event_id").GetString() ?? Guid.NewGuid().ToString(),
                        JobId = root.GetProperty("job_id").GetString() ?? "unknown",
                        ChunkId = root.TryGetProperty("chunk_id", out var c) ? c.GetString() : null,
                        CorrelationId = root.TryGetProperty("correlation_id", out var cor) ? cor.GetString() : null,
                        TenantId = root.TryGetProperty("tenant_id", out var t) ? t.GetString() : null,
                        UserId = root.TryGetProperty("user_id", out var u) ? u.GetString() : null,
                        AppId = root.TryGetProperty("app_id", out var a) ? a.GetString() : null,
                        Action = root.TryGetProperty("action", out var ac) ? ac.GetString() ?? "UNKNOWN" : "UNKNOWN",
                        Status = root.GetProperty("status").GetString() ?? "UNKNOWN",
                        Message = root.TryGetProperty("message", out var m) ? m.GetString() : null,
                        Metadata = root.TryGetProperty("metadata", out var meta) && meta.ValueKind != JsonValueKind.Null ? meta.GetRawText() : null,
                        Timestamp = root.TryGetProperty("timestamp", out var ts) && ts.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow
                    };

                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TraceDbContext>();
                    
                    dbContext.TraceEvents.Add(eventRecord);
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process trace event message on subject {Subject}", msg.Subject);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("NATS Trace Subscriber stopping cleanly.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in NATS Trace Subscriber.");
            throw;
        }
    }
}
