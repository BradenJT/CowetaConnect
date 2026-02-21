// src/CowetaConnect.Domain/Entities/BusinessTag.cs
namespace CowetaConnect.Domain.Entities;

public class BusinessTag
{
    public Guid BusinessId { get; set; }
    public Guid TagId { get; set; }

    public Business Business { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}
