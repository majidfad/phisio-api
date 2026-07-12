using Microsoft.AspNetCore.Identity;
using Phisio.Domain.Common;
using Phisio.Domain.Entities;
using Phisio.Domain.Enums;

namespace Phisio.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>, IAuditableEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsEnabled { get; set; } = true;

    public ICollection<UserExercise> UserExercises { get; set; } = new List<UserExercise>();

    public DoctorProfile? DoctorProfile { get; set; }
}
