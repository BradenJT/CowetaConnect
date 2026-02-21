// src/CowetaConnect.Domain/Entities/Business.cs
using NetTopologySuite.Geometries;

namespace CowetaConnect.Domain.Entities;

public class Business
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? AddressLine1 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = "OK";
    public string? Zip { get; set; }
    public double? Lat { get; set; }
    public double? Lng { get; set; }
    public Point? Location { get; set; }                // geography(Point,4326) — PostGIS
    public string? ServiceAreaGeoJson { get; set; }     // jsonb
    public bool IsActive { get; set; } = true;
    public bool IsVerified { get; set; }
    public int? FeaturedRank { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Category Category { get; set; } = null!;
    public ICollection<BusinessTag> BusinessTags { get; set; } = [];
    public ICollection<BusinessHour> BusinessHours { get; set; } = [];
    public ICollection<BusinessPhoto> BusinessPhotos { get; set; } = [];
}
