
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using TraceabilityService.Data;
using TraceabilityService.Protos;
using Google.Protobuf;
using Document.Orchestrator.V1;

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

    public override async Task<SaveJobStateResponse> SaveJobState(SaveJobStateRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Saving job state for Job ID: {JobId} with Status: {Status}", request.JobId, request.Status);

        var cancellationToken = context.CancellationToken;
        var existing = await _dbContext.JobStates.FirstOrDefaultAsync(x => x.JobId == request.JobId, cancellationToken);

        if (existing == null)
        {
            existing = new Models.JobStateEntity
            {
                JobId = request.JobId,
                Status = request.Status,
                OriginalRequestBytes = request.OriginalRequest?.ToByteArray() ?? Array.Empty<byte>()
            };
            _dbContext.JobStates.Add(existing);
        }
        else
        {
            existing.Status = request.Status;
            if (request.OriginalRequest != null)
            {
                existing.OriginalRequestBytes = request.OriginalRequest.ToByteArray();
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SaveJobStateResponse { Success = true };
    }

    public override async Task<GetPendingJobsResponse> GetPendingJobs(GetPendingJobsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Retrieving pending jobs");

        var cancellationToken = context.CancellationToken;
        var pendingEntities = await _dbContext.JobStates
            .Where(x => x.Status == "PENDING")
            .ToListAsync(cancellationToken);

        var response = new GetPendingJobsResponse();

        foreach (var entity in pendingEntities)
        {
            try
            {
                var originalReq = SubmitJobRequest.Parser.ParseFrom(entity.OriginalRequestBytes);
                response.Jobs.Add(new JobStateRecord
                {
                    JobId = entity.JobId,
                    Status = entity.Status,
                    OriginalRequest = originalReq
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize pending job {JobId}", entity.JobId);
            }
        }

        return response;
    }

    public override async Task<GetJobStateResponse> GetJobState(GetJobStateRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Retrieving job state for Job ID: {JobId}", request.JobId);

        var cancellationToken = context.CancellationToken;
        var entity = await _dbContext.JobStates.FirstOrDefaultAsync(x => x.JobId == request.JobId, cancellationToken);

        if (entity == null)
        {
            return new GetJobStateResponse { Found = false };
        }

        try
        {
            var originalReq = SubmitJobRequest.Parser.ParseFrom(entity.OriginalRequestBytes);
            return new GetJobStateResponse
            {
                Found = true,
                Job = new JobStateRecord
                {
                    JobId = entity.JobId,
                    Status = entity.Status,
                    OriginalRequest = originalReq
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize job state for {JobId}", entity.JobId);
            return new GetJobStateResponse { Found = false };
        }
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
            Timestamp = record.Timestamp.ToString("o"),
            DocumentType = record.DocumentType ?? string.Empty
        };
    }
}
