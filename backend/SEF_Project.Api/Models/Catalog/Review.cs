namespace SEF_Project.Api.Models.Catalog;

public class Review : GuidEntity
{
    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public bool IsPublished { get; set; } = true;
}
