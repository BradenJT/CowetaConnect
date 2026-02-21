// src/CowetaConnect.Domain/Entities/Category.cs
namespace CowetaConnect.Domain.Entities;

public class Category
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public Guid? ParentId { get; set; }

    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = [];
    public ICollection<Business> Businesses { get; set; } = [];
}
