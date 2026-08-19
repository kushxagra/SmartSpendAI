// SmartSpendAI/DTOs/InvoiceDtos.cs

namespace SmartSpendAI.DTOs;

// ---------- Requests ----------

public class CreateInvoiceRequest
{
    public int VendorId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public int? UploadedByUserId { get; set; }
    public List<CreateInvoiceItemRequest> Items { get; set; } = new();
}

public class CreateInvoiceItemRequest
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

// ---------- Responses ----------

public class InvoiceListItemDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? RiskScore { get; set; }
    public string? RiskLevel { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InvoiceDetailDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal? SubTotal { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public DateTime CreatedAt { get; set; }

    public decimal? RiskScore { get; set; }
    public string? RiskLevel { get; set; }
    public string? RiskReason { get; set; }

    public List<InvoiceItemDto> Items { get; set; } = new();
}

public class InvoiceItemDto
{
    public int InvoiceItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class UploadResultDto
{
    public int InvoiceId { get; set; }
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

// ---------- Errors ----------

public class ApiError
{
    public string Message { get; set; } = string.Empty;

    public ApiError() { }
    public ApiError(string message) => Message = message;
}