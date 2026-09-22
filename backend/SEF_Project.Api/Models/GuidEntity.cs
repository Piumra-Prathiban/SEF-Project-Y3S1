namespace SEF_Project.Api.Models;

public abstract class GuidEntity : IAuditableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
