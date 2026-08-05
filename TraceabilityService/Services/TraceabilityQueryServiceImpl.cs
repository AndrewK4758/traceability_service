
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using TraceabilityService.Data;
using TraceabilityService.Protos;
using Google.Protobuf;
using Document.Orchestrator.V1;
using Mployee.Dotnet.Sdk.Payloads;
using Mployee.Dotnet.Sdk.Payloads.Workflow;

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

        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;

        var events = await _dbContext.TraceEvents
            .Where(e => e.TenantId == request.Identity.TenantId 
                     && e.AppId == request.Identity.AppId 
                     && e.JobId == request.Identity.JobId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

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

        var cancellationToken = context?.CancellationToken ?? CancellationToken.None;

        var events = await _dbContext.TraceEvents
            .Where(e => e.TenantId == request.Identity.TenantId 
                     && e.AppId == request.Identity.AppId 
                     && e.CorrelationId == request.CorrelationId)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(cancellationToken);

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
        var cancellationToken = context.CancellationToken;

        int retries = 3;
        while (retries > 0)
        {
            try
            {
                var existing = await _dbContext.JobStates.FirstOrDefaultAsync(x => x.TenantId == request.Identity.TenantId && x.AppId == request.Identity.AppId && x.JobId == request.Identity.JobId, cancellationToken);
                if (existing == null)
                {
                    existing = new Models.JobStateEntity
                    {
                        TenantId = request.Identity.TenantId,
                        AppId = request.Identity.AppId,
                        JobId = request.Identity.JobId,
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
            catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("UNIQUE constraint failed") == true)
            {
                _logger.LogWarning("Concurrency conflict on SaveJobState for {JobId}, retrying...", request.Identity.JobId);
                // Detach everything to reset the DbContext state before retrying
                _dbContext.ChangeTracker.Clear();
                retries--;
                if (retries == 0) throw;
            }
        }
        
        return new SaveJobStateResponse { Success = false };
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
                var originalReq = ExecuteTaskRequest.Parser.ParseFrom(entity.OriginalRequestBytes);
                response.Jobs.Add(new JobStateRecord
                {
                    Identity = new Document.Models.V1.InfrastructureIdentity
                    {
                        TenantId = entity.TenantId,
                        AppId = entity.AppId,
                        JobId = entity.JobId
                    },
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
        _logger.LogInformation("Retrieving job state for TenantId: {TenantId} | AppId: {AppId} | JobId: {JobId}", request.Identity.TenantId, request.Identity.AppId, request.Identity.JobId);

        var cancellationToken = context.CancellationToken;
        var entity = await _dbContext.JobStates.FirstOrDefaultAsync(x => x.TenantId == request.Identity.TenantId && x.AppId == request.Identity.AppId && x.JobId == request.Identity.JobId, cancellationToken);

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
                        TenantId = entity.TenantId,
                        AppId = entity.AppId,
                        JobId = entity.JobId
                    },
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

    private TraceEvent MapToProto(Models.TraceEventRecord record)
    {
        #pragma warning disable CS0618
        return new TraceEvent
        {
            Id = record.EventId,
            JobId = record.JobId ?? string.Empty,
            ResourceId = record.ChunkId ?? string.Empty,
            CorrelationId = record.CorrelationId ?? string.Empty,
            EventType = record.Action ?? string.Empty,
            StepId = string.Empty,
            Message = record.Message ?? string.Empty,
            Metadata = record.Metadata ?? string.Empty,
            Timestamp = record.Timestamp.ToString("O"),
            DocumentType = record.DocumentType ?? string.Empty,
            TenantId = record.TenantId ?? string.Empty,
            AppId = record.AppId ?? string.Empty
        };
        #pragma warning restore CS0618
    }
}
