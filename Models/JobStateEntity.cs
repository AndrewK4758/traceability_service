using System;
using System.ComponentModel.DataAnnotations;

namespace TraceabilityService.Models;

public class JobStateEntity
{
    [Key]
    public string JobId { get; set; } = string.Empty;

    // We store the serialized protobuf byte array directly
    public byte[] OriginalRequestBytes { get; set; } = Array.Empty<byte>();

    public string Status { get; set; } = "PENDING";
}
