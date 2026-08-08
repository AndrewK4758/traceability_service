using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace TraceabilityService.Models;

[Owned]
public record InfrastructureIdentity
{
    [Required]
    [MaxLength(128)]
    public string TenantId { get; init; } = string.Empty;
    
    [Required]
    [MaxLength(128)]
    public string AppId { get; init; } = string.Empty;
    
    [Required]
    [MaxLength(128)]
    public string JobId { get; init; } = string.Empty;
}
