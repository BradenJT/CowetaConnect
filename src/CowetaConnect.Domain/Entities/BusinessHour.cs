// src/CowetaConnect.Domain/Entities/BusinessHour.cs
namespace CowetaConnect.Domain.Entities;

public class BusinessHour
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public int DayOfWeek { get; set; }      // 0 = Sunday … 6 = Saturday
    public TimeOnly? OpenTime { get; set; }
    public TimeOnly? CloseTime { get; set; }
    public bool IsClosed { get; set; }

    public Business Business { get; set; } = null!;
}
