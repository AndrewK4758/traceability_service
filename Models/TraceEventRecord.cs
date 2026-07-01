
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TraceabilityService.Models;

[Table("TraceEvents")]
public class TraceEventRecord
{
    [Key]
    public required string EventId { get; set; }
    
    [Required]
    [MaxLength(128)]
    public required string JobId { get; set; }
    
    [MaxLength(128)]
    public string? ChunkId { get; set; }
    
    [MaxLength(128)]
    public string? CorrelationId { get; set; }
    
    [MaxLength(128)]
    public string? TenantId { get; set; }
    
    [MaxLength(128)]
    public string? UserId { get; set; }
    
    [MaxLength(128)]
    public string? AppId { get; set; }
    
    [MaxLength(64)]
    public required string Action { get; set; }
    
    [MaxLength(32)]
    public required string Status { get; set; }
    
    [MaxLength(1024)]
    public string? Message { get; set; }
    
    public string? Metadata { get; set; }
    
    [MaxLength(256)]
    public string? DocumentType { get; set; }
    
    public int? TotalChunks { get; set; }
    
    public DateTime Timestamp { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
