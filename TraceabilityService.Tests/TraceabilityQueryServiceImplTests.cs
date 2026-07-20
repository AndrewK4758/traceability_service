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
            EventId = "1", TenantId = "T1", AppId = "A1", JobId = "job_1", Action = "JOB", Status = "STARTED", Timestamp = DateTime.UtcNow 
        });
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "2", TenantId = "T1", AppId = "A1", JobId = "job_1", Action = "JOB", Status = "COMPLETED", Timestamp = DateTime.UtcNow.AddMinutes(1) 
        });
        await context.SaveChangesAsync();

        var service = new TraceabilityQueryServiceImpl(context, NullLogger<TraceabilityQueryServiceImpl>.Instance);
        var request = new GetJobTraceRequest { Identity = new Document.Models.V1.InfrastructureIdentity { TenantId = "T1", AppId = "A1", JobId = "job_1" } };

        var response = await service.GetJobTrace(request, null!);


        Assert.Equal(2, response.Events.Count);
    }

    [Fact]
    public async Task GetJobTrace_ShouldReturnFailedStatus_WhenAnyEventFailed()
    {
        var options = GetInMemoryOptions("GetJobTrace_Failed_Db");
        using var context = new TraceDbContext(options);
        
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "1", TenantId = "T1", AppId = "A1", JobId = "job_fail", Action = "JOB", Status = "STARTED", Timestamp = DateTime.UtcNow 
        });
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "2", TenantId = "T1", AppId = "A1", JobId = "job_fail", Action = "CHUNK", Status = "FAILED", Timestamp = DateTime.UtcNow.AddMinutes(1) 
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
            EventId = "1", TenantId = "T1", AppId = "A1", JobId = "job_1", CorrelationId = "corr_1", Action = "JOB", Status = "STARTED", Timestamp = DateTime.UtcNow 
        });
        context.TraceEvents.Add(new TraceEventRecord 
        { 
            EventId = "2", TenantId = "T1", AppId = "A1", JobId = "job_2", CorrelationId = "corr_1", Action = "JOB", Status = "STARTED", Timestamp = DateTime.UtcNow.AddMinutes(1) 
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
}
