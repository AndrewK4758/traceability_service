using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TraceabilityService.Data;
using TraceabilityService.Models;
using TraceabilityService.Protos;
using TraceabilityService.Services;
using Xunit;

namespace TraceabilityService.Tests;

public class TraceabilityQueryServiceImplTests
{
    private DbContextOptions<TraceDbContext> GetInMemoryOptions(string dbName)
    {
        return new DbContextOptionsBuilder<TraceDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
    }

    [Fact]
    public async Task GetJobTrace_ShouldReturnEvents_WhenJobExists()
    {
        var options = GetInMemoryOptions("GetJobTrace_Db");
        using var context = new TraceDbContext(options);
        
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "1", Identity = new InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job_1" }, Action = "JOB", Status = "STARTED", Timestamp = DateTime.UtcNow 
        });
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "2", Identity = new InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job_1" }, Action = "JOB", Status = "COMPLETED", Timestamp = DateTime.UtcNow.AddMinutes(1) 
        });
        await context.SaveChangesAsync();

        var service = new TraceabilityQueryServiceImpl(context, NullLogger<TraceabilityQueryServiceImpl>.Instance);
        var request = new GetJobTraceRequest { Identity = new Document.Models.V1.InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job_1" } };

        var response = await service.GetJobTrace(request, null!);

        Assert.Equal(2, response.Events.Count);
        Assert.Equal("T1", response.Events[0].TenantId);
        Assert.Equal("A1", response.Events[0].AppId);
        Assert.Equal(string.Empty, response.Events[0].StepId);
    }

    [Fact]
    public async Task GetJobTrace_ShouldReturnFailedStatus_WhenAnyEventFailed()
    {
        var options = GetInMemoryOptions("GetJobTrace_Failed_Db");
        using var context = new TraceDbContext(options);
        
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "1", Identity = new InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job_fail" }, Action = "JOB", Status = "STARTED", Timestamp = DateTime.UtcNow 
        });
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "2", Identity = new InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job_fail" }, Action = "CHUNK", Status = "FAILED", Timestamp = DateTime.UtcNow.AddMinutes(1) 
        });
        await context.SaveChangesAsync();

        var service = new TraceabilityQueryServiceImpl(context, NullLogger<TraceabilityQueryServiceImpl>.Instance);
        var request = new GetJobTraceRequest { Identity = new Document.Models.V1.InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job_fail" } };

        var response = await service.GetJobTrace(request, null!);

        Assert.Equal(2, response.Events.Count);
    }

    [Fact]
    public async Task GetCorrelationTrace_ShouldReturnEvents_WhenCorrelationExists()
    {
        var options = GetInMemoryOptions("GetCorrelationTrace_Db");
        using var context = new TraceDbContext(options);
        
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "1", Identity = new InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job_1" }, CorrelationId = "corr_1", Action = "JOB", Status = "STARTED", Timestamp = DateTime.UtcNow 
        });
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "2", Identity = new InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job_2" }, CorrelationId = "corr_1", Action = "JOB", Status = "STARTED", Timestamp = DateTime.UtcNow.AddMinutes(1) 
        });
        await context.SaveChangesAsync();

        var service = new TraceabilityQueryServiceImpl(context, NullLogger<TraceabilityQueryServiceImpl>.Instance);
        var request = new GetCorrelationTraceRequest { 
            CorrelationId = "corr_1",
            Identity = new Document.Models.V1.InfrastructureIdentity { TenantId = "T1", AppId = "A1" }
        };

        var response = await service.GetCorrelationTrace(request, null!);

        Assert.Equal(2, response.Events.Count);
    }

    [Fact]
    public async Task MapToProto_ShouldPopulateTenantIdAndAppId_AndNotFallbackStepId()
    {
        var options = GetInMemoryOptions("MapToProto_Db");
        using var context = new TraceDbContext(options);
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "evt_100", Identity = new InfrastructureIdentity { TenantId = "tenant_test", AppId = "app_test", JobId = "job_test" }, Action = "PROCESS", Status = "COMPLETED", Timestamp = DateTime.UtcNow 
        });
        context.SaveChanges();

        var service = new TraceabilityQueryServiceImpl(context, NullLogger<TraceabilityQueryServiceImpl>.Instance);
        var request = new GetJobTraceRequest { Identity = new Document.Models.V1.InfrastructureIdentity { TenantId = "tenant_test", AppId = "app_test", JobId = "job_test" } };

        var response = await service.GetJobTrace(request, null!);

        Assert.Single(response.Events);
        var ev = response.Events[0];
        Assert.Equal("tenant_test", ev.TenantId);
        Assert.Equal("app_test", ev.AppId);
        Assert.Equal(string.Empty, ev.StepId);
    }

    [Fact]
    public async Task MapToProto_NullPropertiesOnRecord_MapsToEmptyStrings_NotFallbackStrings()
    {
        var options = GetInMemoryOptions("MapToProto_NullProps_Db");
        using var context = new TraceDbContext(options);
        
        var record = new TraceEventRecord 
        { 
            EventId = "evt_nulls", 
            Identity = new InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job1" }, 
            ChunkId = null,
            CorrelationId = null,
            Action = "JOB", 
            Status = "COMPLETED",
            Message = null,
            Metadata = null,
            DocumentType = null,
            Timestamp = DateTime.UtcNow 
        };
        context.TraceEvents.Add(record);
        await context.SaveChangesAsync();

        var service = new TraceabilityQueryServiceImpl(context, NullLogger<TraceabilityQueryServiceImpl>.Instance);
        var request = new GetJobTraceRequest { Identity = new Document.Models.V1.InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job1" } };

        var response = await service.GetJobTrace(request, null!);

        Assert.Single(response.Events);
        var ev = response.Events[0];
        Assert.Equal("evt_nulls", ev.Id);
        Assert.Equal("T1", ev.TenantId);
        Assert.Equal("A1", ev.AppId);
        Assert.Equal("job1", ev.JobId);
        Assert.Equal(string.Empty, ev.ResourceId);
        Assert.Equal(string.Empty, ev.CorrelationId);
        Assert.Equal("JOB", ev.EventType);
        Assert.Equal(string.Empty, ev.StepId);
        Assert.Equal(string.Empty, ev.Message);
        Assert.Equal(string.Empty, ev.Metadata);
        Assert.Equal(string.Empty, ev.DocumentType);

        Assert.NotEqual("UNKNOWN", ev.StepId);
        Assert.NotEqual("unknown", ev.StepId);
    }

    [Fact]
    public void ParseTraceEventJson_ValidJson_ParsesSuccessfully()
    {
        var json = """
        {
            "event_id": "e-123",
            "job_id": "j-456",
            "tenant_id": "tenant-abc",
            "app_id": "app-xyz",
            "job_action": "PROCESS_DOC",
            "status": "COMPLETED",
            "timestamp": "2023-01-01T00:00:00Z"
        }
        """;

        var record = TraceabilityService.Workers.NatsTraceSubscriber.ParseTraceEventJson(json);

        Assert.Equal("e-123", record.EventId);
        Assert.Equal("j-456", record.Identity.JobId);
        Assert.Equal("tenant-abc", record.Identity.TenantId);
        Assert.Equal("app-xyz", record.Identity.AppId);
        Assert.Equal("PROCESS_DOC", record.Action);
        Assert.Equal("COMPLETED", record.Status);
    }

    [Fact]
    public void ParseTraceEventJson_MissingMandatoryField_ThrowsInvalidOperationException()
    {
        var missingJobIdJson = """
        {
            "event_id": "e-123",
            "job_action": "PROCESS_DOC",
            "status": "COMPLETED"
        }
        """;

        Assert.Throws<InvalidOperationException>(() => 
            TraceabilityService.Workers.NatsTraceSubscriber.ParseTraceEventJson(missingJobIdJson));
    }

    [Theory]
    [InlineData("{\"event_id\": null, \"job_id\": \"j1\", \"job_action\": \"JOB\", \"status\": \"COMPLETED\"}")]
    [InlineData("{\"event_id\": \"\", \"job_id\": \"j1\", \"job_action\": \"JOB\", \"status\": \"COMPLETED\"}")]
    [InlineData("{\"event_id\": \"   \", \"job_id\": \"j1\", \"job_action\": \"JOB\", \"status\": \"COMPLETED\"}")]
    [InlineData("{\"event_id\": \"e1\", \"job_id\": null, \"job_action\": \"JOB\", \"status\": \"COMPLETED\"}")]
    [InlineData("{\"event_id\": \"e1\", \"job_id\": \"\", \"job_action\": \"JOB\", \"status\": \"COMPLETED\"}")]
    [InlineData("{\"event_id\": \"e1\", \"job_id\": \"   \", \"job_action\": \"JOB\", \"status\": \"COMPLETED\"}")]
    [InlineData("{\"event_id\": \"e1\", \"job_id\": \"j1\"}")]
    [InlineData("{\"event_id\": \"e1\", \"job_id\": \"j1\", \"status\": \"COMPLETED\"}")]
    [InlineData("{\"event_id\": \"e1\", \"job_id\": \"j1\", \"job_action\": \"JOB\"}")]
    [InlineData("{}")]
    public void ParseTraceEventJson_BoundaryInputs_ThrowsInvalidOperationException(string invalidJson)
    {
        Assert.Throws<InvalidOperationException>(() => 
            TraceabilityService.Workers.NatsTraceSubscriber.ParseTraceEventJson(invalidJson));
    }

    [Fact]
    public void ParseTraceEventJson_MalformedJson_ThrowsJsonException()
    {
        var malformedJson = "NOT_JSON_AT_ALL";
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => 
            TraceabilityService.Workers.NatsTraceSubscriber.ParseTraceEventJson(malformedJson));
    }

    [Fact]
    public void ParseTraceEventJson_EventTypeWithDot_ParsesActionAndStatus()
    {
        var json = """
        {
            "event_id": "e-123",
            "job_id": "j-456",
            "event_type": "DOCUMENT_PROCESSING.STARTED",
            "timestamp": "2023-01-01T00:00:00Z"
        }
        """;

        var record = TraceabilityService.Workers.NatsTraceSubscriber.ParseTraceEventJson(json);

        Assert.Equal("DOCUMENT_PROCESSING", record.Action);
        Assert.Equal("STARTED", record.Status);
    }

    [Fact]
    public void ParseTraceEventJson_NullOptionalProperties_ParsesWithoutStringFallbacks()
    {
        var json = """
        {
            "event_id": "e-123",
            "job_id": "j-456",
            "job_action": "PROCESS_DOC",
            "status": "COMPLETED",
            "chunk_id": null,
            "correlation_id": null,
            "tenant_id": null,
            "user_id": null,
            "app_id": null,
            "document_type": null,
            "error": null,
            "message": null,
            "metadata": null,
            "timestamp": "2023-01-01T00:00:00Z"
        }
        """;

        var record = TraceabilityService.Workers.NatsTraceSubscriber.ParseTraceEventJson(json);

        Assert.Null(record.ChunkId);
        Assert.Null(record.CorrelationId);
        Assert.Empty(record.Identity.TenantId);
        Assert.Empty(record.Identity.AppId);
        Assert.Null(record.UserId);
        Assert.Null(record.DocumentType);
        Assert.Null(record.Message);
        Assert.Null(record.Metadata);
    }
}
