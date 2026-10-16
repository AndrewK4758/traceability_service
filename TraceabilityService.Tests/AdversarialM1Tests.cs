using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TraceabilityService.Data;
using TraceabilityService.Models;
using TraceabilityService.Protos;
using TraceabilityService.Services;
using TraceabilityService.Workers;
using Xunit;

namespace TraceabilityService.Tests;

public class AdversarialM1Tests
{
    private DbContextOptions<TraceDbContext> GetInMemoryOptions(string dbName)
    {
        return new DbContextOptionsBuilder<TraceDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
    }

    [Theory]
    [InlineData("""{"job_id": "j1", "job_action": "ACT", "status": "OK"}""", "event_id")]
    [InlineData("""{"event_id": "", "job_id": "j1", "job_action": "ACT", "status": "OK"}""", "event_id")]
    [InlineData("""{"event_id": "   ", "job_id": "j1", "job_action": "ACT", "status": "OK"}""", "event_id")]
    [InlineData("""{"event_id": "e1", "job_action": "ACT", "status": "OK"}""", "job_id")]
    [InlineData("""{"event_id": "e1", "job_id": "", "job_action": "ACT", "status": "OK"}""", "job_id")]
    [InlineData("""{"event_id": "e1", "job_id": "j1", "status": "OK"}""", "action")]
    [InlineData("""{"event_id": "e1", "job_id": "j1", "event_type": "NO_DOT", "status": "OK"}""", "action")]
    [InlineData("""{"event_id": "e1", "job_id": "j1", "job_action": "ACT"}""", "status")]
    public void ParseTraceEventJson_IncompleteOrMalformed_ThrowsInvalidOperationException(string jsonPayload, string expectedMissingDetail)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            NatsTraceSubscriber.ParseTraceEventJson(jsonPayload));

        Assert.Contains(expectedMissingDetail, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseTraceEventJson_DotSeparatedEventType_ParsesActionAndStatus()
    {
        var json = """
        {
            "event_id": "e-999",
            "job_id": "j-888",
            "event_type": "document.processed",
            "timestamp": "2023-01-01T00:00:00Z"
        }
        """;

        var record = NatsTraceSubscriber.ParseTraceEventJson(json);

        Assert.Equal("DOCUMENT", record.Action);
        Assert.Equal("PROCESSED", record.Status);
    }

    [Fact]
    public void ParseTraceEventJson_ErrorPrecedesMessage_AndExtractedDataFallback()
    {
        var json = """
        {
            "event_id": "e-100",
            "job_id": "j-200",
            "job_action": "TRANSFORM",
            "status": "FAILED",
            "error": "Critical system failure",
            "message": "Secondary message",
            "extracted_data": {"key": "value"},
            "timestamp": "2023-01-01T00:00:00Z"
        }
        """;

        var record = NatsTraceSubscriber.ParseTraceEventJson(json);

        Assert.Equal("Critical system failure", record.Message);
        Assert.NotNull(record.Metadata);
        Assert.Contains("key", record.Metadata);
    }

    [Fact]
    public async Task MapToProto_StrictTenantAndAppIdMapping_StepIdMustBeEmpty()
    {
        var options = GetInMemoryOptions("Adversarial_MapToProto_Db");
        using var context = new TraceDbContext(options);
        context.TraceEvents.Add(new TraceEventRecord
        {
            EventId = "evt_adv_1",
            Identity = new InfrastructureIdentity
            {
                TenantId = "TENANT_ALPHA",
                AppId = "APP_BETA",
                JobId = "JOB_GAMMA"
            },
            Action = "INGEST",
            Status = "COMPLETED",
            Timestamp = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new TraceabilityQueryServiceImpl(context, NullLogger<TraceabilityQueryServiceImpl>.Instance);
        var request = new GetJobTraceRequest
        {
            Identity = new Document.Models.V1.InfrastructureIdentity
            {
                TenantId = "TENANT_ALPHA",
                AppId = "APP_BETA",
                JobId = "JOB_GAMMA"
            }
        };

        var response = await service.GetJobTrace(request, null!);

        Assert.Single(response.Events);
        var ev = response.Events[0];

        Assert.Equal("TENANT_ALPHA", ev.TenantId);
        Assert.Equal("APP_BETA", ev.AppId);
        Assert.Equal(string.Empty, ev.StepId);
        Assert.NotEqual(ev.AppId, ev.StepId);
    }

    [Fact]
    public async Task MapToProto_EmptyTenantAndAppId_MapsToEmptyStrings()
    {
        var options = GetInMemoryOptions("Adversarial_EmptyIdentity_Db");
        using var context = new TraceDbContext(options);
        context.TraceEvents.Add(new TraceEventRecord
        {
            EventId = "evt_empty_1",
            Identity = new InfrastructureIdentity
            {
                TenantId = "",
                AppId = "",
                JobId = "JOB_EMPTY"
            },
            Action = "PROCESS",
            Status = "COMPLETED",
            Timestamp = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var service = new TraceabilityQueryServiceImpl(context, NullLogger<TraceabilityQueryServiceImpl>.Instance);
        var request = new GetJobTraceRequest
        {
            Identity = new Document.Models.V1.InfrastructureIdentity
            {
                TenantId = "",
                AppId = "",
                JobId = "JOB_EMPTY"
            }
        };

        var response = await service.GetJobTrace(request, null!);

        Assert.Single(response.Events);
        var ev = response.Events[0];

        Assert.Equal(string.Empty, ev.TenantId);
        Assert.Equal(string.Empty, ev.AppId);
        Assert.Equal(string.Empty, ev.StepId);
    }
}
