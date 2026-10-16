using System;
using System.ComponentModel.DataAnnotations;

namespace TraceabilityService.Models;

public class JobStateEntity
{
    [Key]
    public int Id { get; set; }

    public required InfrastructureIdentity Identity { get; set; }

    // Serialized ExecuteTaskRequest protobuf payload used to resume pending jobs.
    public byte[] OriginalRequestBytes { get; set; } = Array.Empty<byte>();

    public JobStatus Status { get; set; } = JobStatus.PENDING;
}
