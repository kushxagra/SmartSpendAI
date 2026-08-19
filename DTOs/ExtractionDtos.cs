// SmartSpendAI/DTOs/ExtractionDtos.cs

namespace SmartSpendAI.DTOs;

/// <summary>
/// The contract we ask the AI to return. Dates are strings because models
/// return varied formats — we parse them ourselves during validation.
/// Everything is nullable: a missing field is a validation failure, not a crash.
/// </summary>
public class ExtractedInvoiceDto
{
    public string? VendorName { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceDate { get; set; }
    public string? DueDate { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? Tax { get; set; }
    public decimal? Total { get; set; }
    public List<ExtractedItemDto> Items { get; set; } = new();
}

public class ExtractedItemDto
{
    public string? Description { get; set; }
    public decimal? Quantity { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? Total { get; set; }
}

/// <summary>What the extract endpoint returns to the caller.</summary>
public class ExtractionResponseDto
{
    public int InvoiceId { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string> ValidationErrors { get; set; } = new();
    public ExtractedInvoiceDto? Extracted { get; set; }
}