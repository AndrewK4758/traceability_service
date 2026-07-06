
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
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
        _logger.LogInformation("Traceability NATS Subscriber starting up on JetStream...");
        
        try
        {
            var js = new NatsJSContext(_natsConnection);
            
            // Create the consumer if it doesn't exist
            var consumer = await js.CreateOrUpdateConsumerAsync("TRACE_EVENTS", new ConsumerConfig
            {
                Name = "TraceabilityService_Consumer",
                DurableName = "TraceabilityService_Consumer",
                FilterSubject = "trace.>",
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                DeliverPolicy = ConsumerConfigDeliverPolicy.All
            }, cancellationToken: stoppingToken);
            
            _logger.LogInformation("NATS consumer created. Subscribing to subject: {Subject}", consumer.Info.Config.FilterSubject);

            await foreach (var msg in consumer.ConsumeAsync<byte[]>(cancellationToken: stoppingToken))
            {
                if (msg.Data is null) 
                {
                    await msg.AckAsync(cancellationToken: stoppingToken);
                    continue;
                }

                try
                {
                    var traceJson = System.Text.Encoding.UTF8.GetString(msg.Data);
                    _logger.LogDebug("Received trace event: {Json}", traceJson);

                    // Parse JSON payload dynamically or via JsonDocument (since the incoming JSON matches NATS payload structure)
                    using var doc = JsonDocument.Parse(traceJson);
                    var root = doc.RootElement;
                    
                    var eventTypeStr = root.TryGetProperty("event_type", out var et) ? et.GetString() ?? "" : "";
                    var action = "UNKNOWN";
                    var status = "UNKNOWN";
                    
                    if (eventTypeStr.Contains('.'))
                    {
                        var parts = eventTypeStr.Split('.', 2);
                        action = parts[0].ToUpper();
                        status = parts[1].ToUpper();
                    }

                    var stage = root.TryGetProperty("stage", out var s) ? s.GetString() : "UNKNOWN";
                    var jobAction = root.TryGetProperty("job_action", out var ja) ? ja.GetString() : "UNKNOWN";

                    var eventRecord = new TraceEventRecord
                    {
                        EventId = root.TryGetProperty("event_id", out var eid) ? eid.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
                        JobId = root.TryGetProperty("job_id", out var jid) ? jid.GetString() ?? "unknown" : "unknown",
                        ChunkId = root.TryGetProperty("chunk_id", out var ci) ? ci.GetString() : null,
                        CorrelationId = root.TryGetProperty("correlation_id", out var cor) ? cor.GetString() : null,
                        TenantId = root.TryGetProperty("tenant_id", out var t) ? t.GetString() : null,
                        UserId = root.TryGetProperty("user_id", out var u) ? u.GetString() : null,
                        AppId = root.TryGetProperty("app_id", out var a) ? a.GetString() : null,
                        DocumentType = root.TryGetProperty("document_type", out var dtType) ? dtType.GetString() : null,
                        Action = jobAction,
                        Status = status,
                        Message = root.TryGetProperty("error", out var err) && !string.IsNullOrEmpty(err.GetString()) ? err.GetString() : (root.TryGetProperty("message", out var m) ? m.GetString() : null),
                        Metadata = root.TryGetProperty("metadata", out var meta) ? meta.GetRawText() : (root.TryGetProperty("extracted_data", out var ext) ? ext.GetRawText() : null),
                        Timestamp = root.TryGetProperty("timestamp", out var ts) && ts.TryGetDateTime(out var dt) ? dt : DateTime.UtcNow
                    };

                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<TraceDbContext>();
                    
                    var exists = await dbContext.TraceEvents.AnyAsync(e => e.EventId == eventRecord.EventId, stoppingToken);
                    if (!exists)
                    {
                        dbContext.TraceEvents.Add(eventRecord);
                        await dbContext.SaveChangesAsync(stoppingToken);
                    }

                    // Acknowledge the message upon successful processing
                    await msg.AckAsync(cancellationToken: stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process trace event message on subject {Subject}", msg.Subject);
                    await msg.NakAsync(cancellationToken: stoppingToken);
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
