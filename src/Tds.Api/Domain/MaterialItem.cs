namespace Tds.Api.Domain;

/// <summary>DATA_MODEL §5. Kind + grade + thickness with a minimum stock; stock itself is per lot (StockMovement).</summary>
public class MaterialItem : Entity
{
    public const int NameMax = 120;
    public const int GradeMax = 40;

    public required string Name { get; set; }
    public string? Grade { get; set; }
    public decimal? ThicknessMm { get; set; }
    public Unit Unit { get; set; }
    public decimal? MinimumStock { get; set; }
    public DateOnly? NextStocktakeDate { get; set; }
    public bool IsActive { get; set; } = true;
}
