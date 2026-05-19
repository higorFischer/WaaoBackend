namespace Waao.Domain.Models.Entities;

public abstract class Entity
{
	public Guid Id { get; set; }
	public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	public DateTime? UpdatedAt { get; set; }
	public bool IsDeleted { get; set; }
	public DateTime? DeletedAt { get; set; }
}
