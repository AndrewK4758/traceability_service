
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TraceabilityService.Data;
using TraceabilityService.Models;
using Mployee.Dotnet.Sdk.Payloads;
using Mployee.Dotnet.Sdk.Payloads.Workflow;

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

    public static TraceEventRecord ParseTraceEventJson(string traceJson)
    {
        using var doc = JsonDocument.Parse(traceJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("event_id", out var eid) || string.IsNullOrWhiteSpace(eid.GetString()))
        {
            throw new InvalidOperationException($"Trace event payload is missing mandatory property 'event_id'. JSON: {traceJson}");
        }
        var eventId = eid.GetString()!;

        if (!root.TryGetProperty("job_id", out var jid) || string.IsNullOrWhiteSpace(jid.GetString()))
        {
            throw new InvalidOperationException($"Trace event payload is missing mandatory property 'job_id'. JSON: {traceJson}");
        }
        var jobId = jid.GetString()!;

        string action;
        if (root.TryGetProperty("job_action", out var ja) && !string.IsNullOrWhiteSpace(ja.GetString()))
        {
            action = ja.GetString()!;
        }
        else if (root.TryGetProperty("event_type", out var et) && !string.IsNullOrWhiteSpace(et.GetString()) && et.GetString()!.Contains('.'))
        {
            action = et.GetString()!.Split('.', 2)[0].ToUpper();
        }
        else if (root.TryGetProperty("action", out var act) && !string.IsNullOrWhiteSpace(act.GetString()))
        {
            action = act.GetString()!;
        }
        else
        {
            throw new InvalidOperationException($"Trace event payload is missing mandatory action/job_action/event_type property. JSON: {traceJson}");
        }

        string status;
        if (root.TryGetProperty("status", out var st) && !string.IsNullOrWhiteSpace(st.GetString()))
        {
            status = st.GetString()!.ToUpper();
        }
        else if (root.TryGetProperty("event_type", out var et2) && !string.IsNullOrWhiteSpace(et2.GetString()) && et2.GetString()!.Contains('.'))
        {
            status = et2.GetString()!.Split('.', 2)[1].ToUpper();
        }
        else
        {
            throw new InvalidOperationException($"Trace event payload is missing mandatory status/event_type property. JSON: {traceJson}");
        }

        return new TraceEventRecord
        {
            EventId = eventId,
            JobId = jobId,
            ChunkId = root.TryGetProperty("chunk_id", out var ci) && ci.ValueKind != JsonValueKind.Null ? ci.GetString() : null,
            CorrelationId = root.TryGetProperty("correlation_id", out var cor) && cor.ValueKind != JsonValueKind.Null ? cor.GetString() : null,
            TenantId = root.TryGetProperty("tenant_id", out var t) && t.ValueKind != JsonValueKind.Null ? t.GetString() : null,
            UserId = root.TryGetProperty("user_id", out var u) && u.ValueKind != JsonValueKind.Null ? u.GetString() : null,
            AppId = root.TryGetProperty("app_id", out var a) && a.ValueKind != JsonValueKind.Null ? a.GetString() : null,
            DocumentType = root.TryGetProperty("document_type", out var dtType) && dtType.ValueKind != JsonValueKind.Null ? dtType.GetString() : null,
            Action = action,
            Status = status,
            Message = root.TryGetProperty("error", out var err) && err.ValueKind != JsonValueKind.Null && !string.IsNullOrWhiteSpace(err.GetString()) 
                ? err.GetString() 
                : (root.TryGetProperty("message", out var m) && m.ValueKind != JsonValueKind.Null ? m.GetString() : null),
            Metadata = root.TryGetProperty("metadata", out var meta) && meta.ValueKind != JsonValueKind.Null 
                ? meta.GetRawText() 
                : (root.TryGetProperty("extracted_data", out var ext) && ext.ValueKind != JsonValueKind.Null ? ext.GetRawText() : null),
            Timestamp = root.TryGetProperty("timestamp", out var ts) && ts.TryGetDateTime(out var dt) 
                ? dt.ToUniversalTime() 
                : DateTime.UtcNow
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Traceability NATS Subscriber starting up on JetStream...");
        
        try
        {
            var js = new NatsJSContext(_natsConnection);
            
            // Ensure the stream exists before creating a consumer
            try
            {
                await js.CreateStreamAsync(new StreamConfig("TRACE_EVENTS", new[] { "trace.>" }), cancellationToken: stoppingToken);
                _logger.LogInformation("Ensured TRACE_EVENTS stream exists.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("CreateStreamAsync failed (might already exist or permission issue): {Message}", ex.Message);
            }

            // Create the consumer if it doesn't exist
            var consumer = await js.CreateOrUpdateConsumerAsync("TRACE_EVENTS", new ConsumerConfig
            {
                Name = "TraceabilityService_Consumer",
                DurableName = "TraceabilityService_Consumer",
                FilterSubject = "trace.>",
                AckPolicy = ConsumerConfigAckPolicy.Explicit,
                DeliverPolicy = ConsumerConfigDeliverPolicy.All,
                MaxDeliver = 3,
                AckWait = TimeSpan.FromSeconds(30)
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

                    var eventRecord = ParseTraceEventJson(traceJson);

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
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                {
                    _logger.LogWarning(ex, "DbUpdateException processing trace event (possible duplicate). Acking message.");
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
