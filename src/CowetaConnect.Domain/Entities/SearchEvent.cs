// src/CowetaConnect.Domain/Entities/SearchEvent.cs
namespace CowetaConnect.Domain.Entities;

public class SearchEvent
{
    public Guid Id { get; set; }
    public string? SessionHash { get; set; }        // SHA-256 of session token, no reversibility
    public string? Keyword { get; set; }
    public Guid? CategoryFilter { get; set; }
    public string? CityFilter { get; set; }
    public string? UserCity { get; set; }           // Geolocated origin city
    public string? UserZip { get; set; }            // Geolocated origin ZIP
    public int ResultCount { get; set; }
    public Guid[] ResultBusinessIds { get; set; } = [];   // uuid[]
    public Guid? ClickedBusinessId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
