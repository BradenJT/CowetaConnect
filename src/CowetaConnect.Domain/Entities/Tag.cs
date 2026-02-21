// src/CowetaConnect.Domain/Entities/Tag.cs
namespace CowetaConnect.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;

    public ICollection<BusinessTag> BusinessTags { get; set; } = [];
}
