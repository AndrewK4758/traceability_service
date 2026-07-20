using System;
using System.ComponentModel.DataAnnotations;

namespace TraceabilityService.Models;

public class JobStateEntity
{
    public string TenantId { get; set; } = string.Empty;
    public string AppId { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;

    // We store the serialized protobuf byte array directly
    public byte[] OriginalRequestBytes { get; set; } = Array.Empty<byte>();

    public string Status { get; set; } = "PENDING";
}
