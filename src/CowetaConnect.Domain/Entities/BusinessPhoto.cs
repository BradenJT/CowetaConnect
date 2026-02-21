// src/CowetaConnect.Domain/Entities/BusinessPhoto.cs
namespace CowetaConnect.Domain.Entities;

public class BusinessPhoto
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public string BlobUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }

    public Business Business { get; set; } = null!;
}
