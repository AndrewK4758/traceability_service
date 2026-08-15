using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using TraceabilityService.Data;
using TraceabilityService.Models;
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
        _logger.LogInformation("Received GetJobTrace request for Job ID: {JobId}", request.Identity.JobId);

        var events = await _dbContext.TraceEvents
            .Where(e => e.Identity.TenantId == request.Identity.TenantId 
                     && e.Identity.AppId == request.Identity.AppId 
                     && e.Identity.JobId == request.Identity.JobId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(context?.CancellationToken ?? default);

        var response = new GetJobTraceResponse();

        foreach (var ev in events)
        {
            response.Events.Add(MapToProto(ev));
        }

        return response;
    }

    public override async Task<GetCorrelationTraceResponse> GetCorrelationTrace(GetCorrelationTraceRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Received GetCorrelationTrace request for Correlation ID: {CorrelationId}", request.CorrelationId);

        var events = await _dbContext.TraceEvents
            .Where(e => e.Identity.TenantId == request.Identity.TenantId 
                     && e.Identity.AppId == request.Identity.AppId 
                     && e.CorrelationId == request.CorrelationId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(context?.CancellationToken ?? default);

        var response = new GetCorrelationTraceResponse();

        foreach (var ev in events)
        {
            response.Events.Add(MapToProto(ev));
        }

        return response;
    }

    public override async Task<SaveJobStateResponse> SaveJobState(SaveJobStateRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Saving job state for TenantId: {TenantId} | AppId: {AppId} | JobId: {JobId} with Status: {Status}", request.Identity.TenantId, request.Identity.AppId, request.Identity.JobId, request.Status);
        
        if (!Enum.TryParse<Models.JobStatus>(request.Status, true, out var parsedStatus))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Invalid status: {request.Status}"));
        }

        var rowsAffected = await _dbContext.JobStates
            .Where(x => x.Identity.TenantId == request.Identity.TenantId && x.Identity.AppId == request.Identity.AppId && x.Identity.JobId == request.Identity.JobId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Status, parsedStatus)
                .SetProperty(b => b.OriginalRequestBytes, request.OriginalRequest != null ? request.OriginalRequest.ToByteArray() : Array.Empty<byte>()), 
                context.CancellationToken);

        if (rowsAffected == 0)
        {
            try
            {
                var newState = new Models.JobStateEntity
                {
                    Identity = new InfrastructureIdentity 
                    { 
                        TenantId = request.Identity.TenantId, 
                        AppId = request.Identity.AppId, 
                        JobId = request.Identity.JobId 
                    },
                    Status = parsedStatus,
                    OriginalRequestBytes = request.OriginalRequest?.ToByteArray() ?? Array.Empty<byte>()
                };
                _dbContext.JobStates.Add(newState);
                await _dbContext.SaveChangesAsync(context.CancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException pgEx && pgEx.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
            {
                _logger.LogWarning("Concurrency conflict on SaveJobState for {JobId}, retrying update...", request.Identity.JobId);
                _dbContext.ChangeTracker.Clear();
                await _dbContext.JobStates
                    .Where(x => x.Identity.TenantId == request.Identity.TenantId && x.Identity.AppId == request.Identity.AppId && x.Identity.JobId == request.Identity.JobId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(b => b.Status, parsedStatus)
                        .SetProperty(b => b.OriginalRequestBytes, request.OriginalRequest != null ? request.OriginalRequest.ToByteArray() : Array.Empty<byte>()), 
                        context.CancellationToken);
            }
        }

        return new SaveJobStateResponse { Success = true };
    }

    public override async Task<GetPendingJobsResponse> GetPendingJobs(GetPendingJobsRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Retrieving pending jobs");

        var pendingEntities = await _dbContext.JobStates
            .Where(x => x.Status == Models.JobStatus.PENDING)
            .ToListAsync(context.CancellationToken);

        var response = new GetPendingJobsResponse();

        foreach (var entity in pendingEntities)
        {
            try
            {
                var originalReq = ExecuteTaskRequest.Parser.ParseFrom(entity.OriginalRequestBytes);
                response.Jobs.Add(new JobStateRecord
                {
                    Identity = new Document.Models.V1.InfrastructureIdentity
                    {
                        TenantId = entity.Identity.TenantId,
                        AppId = entity.Identity.AppId,
                        JobId = entity.Identity.JobId
                    },
                    Status = entity.Status.ToString(),
                    OriginalRequest = originalReq
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize pending job {JobId}", entity.Identity.JobId);
                throw new RpcException(new Status(StatusCode.DataLoss, $"Corrupted state for job {entity.Identity.JobId}"));
            }
        }

        return response;
    }

    public override async Task<GetJobStateResponse> GetJobState(GetJobStateRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Retrieving job state for TenantId: {TenantId} | AppId: {AppId} | JobId: {JobId}", request.Identity.TenantId, request.Identity.AppId, request.Identity.JobId);

        var entity = await _dbContext.JobStates.FirstOrDefaultAsync(x => x.Identity.TenantId == request.Identity.TenantId && x.Identity.AppId == request.Identity.AppId && x.Identity.JobId == request.Identity.JobId, context.CancellationToken);

        if (entity == null)
        {
            return new GetJobStateResponse { Found = false };
        }

        try
        {
            var originalReq = ExecuteTaskRequest.Parser.ParseFrom(entity.OriginalRequestBytes);
            return new GetJobStateResponse
            {
                Found = true,
                Job = new JobStateRecord
                {
                    Identity = new Document.Models.V1.InfrastructureIdentity
                    {
                        TenantId = entity.Identity.TenantId,
                        AppId = entity.Identity.AppId,
                        JobId = entity.Identity.JobId
                    },
                    Status = entity.Status.ToString(),
                    OriginalRequest = originalReq
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize job state for {JobId}", entity.Identity.JobId);
            throw new RpcException(new Status(StatusCode.DataLoss, $"Corrupted state for job {entity.Identity.JobId}"));
        }
    }

    private TraceEvent MapToProto(Models.TraceEventRecord record)
    {
        #pragma warning disable CS0618
        return new TraceEvent
        {
            Id = record.EventId,
            JobId = record.Identity.JobId,
            ResourceId = record.ChunkId ?? string.Empty,
            CorrelationId = record.CorrelationId ?? string.Empty,
            EventType = record.Action ?? string.Empty,
            StepId = string.Empty,
            Message = record.Message ?? string.Empty,
            Metadata = record.Metadata ?? string.Empty,
            Timestamp = record.Timestamp.ToString("O"),
            DocumentType = record.DocumentType ?? string.Empty,
            TenantId = record.Identity.TenantId,
            AppId = record.Identity.AppId
        };
        #pragma warning restore CS0618
    }
}
