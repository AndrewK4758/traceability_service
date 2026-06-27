
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using TraceabilityService.Data;
using TraceabilityService.Protos;

namespace TraceabilityService.Services;

public class TraceabilityQueryServiceImpl : TraceabilityQuery.TraceabilityQueryBase
{
    private readonly TraceDbContext _dbContext;
    private readonly ILogger<TraceabilityQueryServiceImpl> _logger;

    public TraceabilityQueryServiceImpl(TraceDbContext dbContext, ILogger<TraceabilityQueryServiceImpl> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public override async Task<GetJobTraceResponse> GetJobTrace(GetJobTraceRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received GetJobTrace request for Job ID: {JobId}", request.JobId);

        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;

        var events = await _dbContext.TraceEvents
            .Where(e => e.JobId == request.JobId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        var response = new GetJobTraceResponse
        {
            JobId = request.JobId,
            Status = DetermineOverallStatus(events)
        };

        foreach (var ev in events)
        {
            response.Events.Add(MapToProto(ev));
        }

        return response;
    }

    public override async Task<GetCorrelationTraceResponse> GetCorrelationTrace(GetCorrelationTraceRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received GetCorrelationTrace request for Correlation ID: {CorrelationId}", request.CorrelationId);

        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;

        var events = await _dbContext.TraceEvents
            .Where(e => e.CorrelationId == request.CorrelationId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

        var response = new GetCorrelationTraceResponse
        {
            CorrelationId = request.CorrelationId
        };

        foreach (var ev in events)
        {
            response.Events.Add(MapToProto(ev));
        }

        return response;
    }

    private string DetermineOverallStatus(List<Models.TraceEventRecord> events)
    {
        if (events.Count == 0) return "UNKNOWN";
        
        var hasFailed = events.Any(e => e.Status.Equals("FAILED", StringComparison.OrdinalIgnoreCase));
        if (hasFailed) return "FAILED";

        var hasCompleted = events.Any(e => e.Action == "JOB" && e.Status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase));
        if (hasCompleted) return "COMPLETED";

        return "IN_PROGRESS";
    }

    private TraceEvent MapToProto(Models.TraceEventRecord record)
    {
        return new TraceEvent
        {
            EventId = record.EventId,
            JobId = record.JobId,
            ChunkId = record.ChunkId ?? string.Empty,
            CorrelationId = record.CorrelationId ?? string.Empty,
            TenantId = record.TenantId ?? string.Empty,
            UserId = record.UserId ?? string.Empty,
            AppId = record.AppId ?? string.Empty,
            Action = record.Action,
            Status = record.Status,
            Message = record.Message ?? string.Empty,
            Metadata = record.Metadata ?? string.Empty,
            Timestamp = record.Timestamp.ToString("o")
        };
    }
}
