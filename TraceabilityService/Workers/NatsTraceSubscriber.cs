using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.JetStream.Models;
using TraceabilityService.Data;
using TraceabilityService.Models;
using Npgsql;

namespace TraceabilityService.Workers;

public class TraceEventDto
{
    [JsonPropertyName("event_id")]
    public string? EventId { get; set; }
    
    [JsonPropertyName("job_id")]
    public string? JobId { get; set; }

    [JsonPropertyName("chunk_id")]
    public string? ChunkId { get; set; }

    [JsonPropertyName("correlation_id")]
    public string? CorrelationId { get; set; }

    [JsonPropertyName("tenant_id")]
    public string? TenantId { get; set; }

    [JsonPropertyName("app_id")]
    public string? AppId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("document_type")]
    public string? DocumentType { get; set; }

    [JsonPropertyName("job_action")]
    public string? JobAction { get; set; }

    [JsonPropertyName("event_type")]
    public string? EventType { get; set; }

    [JsonPropertyName("action")]
    public string? Action { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("metadata")]
    public JsonElement? Metadata { get; set; }

    [JsonPropertyName("extracted_data")]
    public JsonElement? ExtractedData { get; set; }

    [JsonPropertyName("timestamp")]
    public DateTime? Timestamp { get; set; }
}

public class NatsTraceSubscriber : BackgroundService
{
    private readonly ILogger<NatsTraceSubscriber> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly INatsConnection _natsConnection;
    private readonly IConfiguration _configuration;

    public NatsTraceSubscriber(
        ILogger<NatsTraceSubscriber> logger, 
        IServiceProvider serviceProvider,
        INatsConnection natsConnection,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _natsConnection = natsConnection;
        _configuration = configuration;
    }

    public static TraceEventRecord ParseTraceEventJson(string traceJson)
    {
        var dto = JsonSerializer.Deserialize<TraceEventDto>(traceJson);
        if (dto == null) throw new InvalidOperationException($"Failed to deserialize trace event payload: {traceJson}");

        if (string.IsNullOrWhiteSpace(dto.EventId)) throw new InvalidOperationException($"Trace event payload is missing mandatory property 'event_id'. JSON: {traceJson}");
        if (string.IsNullOrWhiteSpace(dto.JobId)) throw new InvalidOperationException($"Trace event payload is missing mandatory property 'job_id'. JSON: {traceJson}");

        string action = !string.IsNullOrWhiteSpace(dto.JobAction) ? dto.JobAction! :
                        (!string.IsNullOrWhiteSpace(dto.EventType) && dto.EventType!.Contains('.')) ? dto.EventType.Split('.', 2)[0].ToUpper() :
                        !string.IsNullOrWhiteSpace(dto.Action) ? dto.Action! :
                        throw new InvalidOperationException($"Trace event payload is missing mandatory action property. JSON: {traceJson}");

        string status = !string.IsNullOrWhiteSpace(dto.Status) ? dto.Status!.ToUpper() :
                        (!string.IsNullOrWhiteSpace(dto.EventType) && dto.EventType!.Contains('.')) ? dto.EventType.Split('.', 2)[1].ToUpper() :
                        throw new InvalidOperationException($"Trace event payload is missing mandatory status property. JSON: {traceJson}");

        if (!dto.Timestamp.HasValue) throw new InvalidOperationException($"Trace event payload is missing mandatory timestamp property. JSON: {traceJson}");

        string? metaString = dto.Metadata.HasValue && dto.Metadata.Value.ValueKind != JsonValueKind.Null && dto.Metadata.Value.ValueKind != JsonValueKind.Undefined ? dto.Metadata.Value.GetRawText() :
                             dto.ExtractedData.HasValue && dto.ExtractedData.Value.ValueKind != JsonValueKind.Null && dto.ExtractedData.Value.ValueKind != JsonValueKind.Undefined ? dto.ExtractedData.Value.GetRawText() : null;

        return new TraceEventRecord
        {
            EventId = dto.EventId,
            Identity = new InfrastructureIdentity
            {
                TenantId = dto.TenantId ?? string.Empty,
                AppId = dto.AppId ?? string.Empty,
                JobId = dto.JobId
            },
            ChunkId = dto.ChunkId,
            CorrelationId = dto.CorrelationId,
            UserId = dto.UserId,
            DocumentType = dto.DocumentType,
            Action = action,
            Status = status,
            Message = !string.IsNullOrWhiteSpace(dto.Error) ? dto.Error : dto.Message,
            Metadata = metaString,
            Timestamp = dto.Timestamp.Value.ToUniversalTime()
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Traceability NATS Subscriber starting up on JetStream...");
        
        var streamName = _configuration["Nats:TraceEventsStreamName"] ?? "TRACE_EVENTS";
        var consumerName = _configuration["Nats:TraceEventsConsumerName"] ?? "TraceabilityService_Consumer";
        var filterSubject = _configuration["Nats:TraceEventsFilterSubject"] ?? "trace.>";

        try
        {
            var js = new NatsJSContext(_natsConnection);
            
            var consumer = await js.CreateOrUpdateConsumerAsync(streamName, new ConsumerConfig
            {
                Name = consumerName,
                DurableName = consumerName,
                FilterSubject = filterSubject,
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
                    
                    dbContext.TraceEvents.Add(eventRecord);
                    await dbContext.SaveChangesAsync(stoppingToken);

                    // Acknowledge the message upon successful processing
                    await msg.AckAsync(cancellationToken: stoppingToken);
                }
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
                {
                    _logger.LogWarning(ex, "Duplicate trace event detected (Unique constraint violation). Acking message.");
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
