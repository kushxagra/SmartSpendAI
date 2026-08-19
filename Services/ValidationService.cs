// SmartSpendAI/Services/ValidationService.cs

using System.Globalization;
using SmartSpendAI.DTOs;

namespace SmartSpendAI.Services;

/// <summary>
/// Checks the AI's output before anything is written to the database.
/// This is the boundary described in the design document: AI output is
/// untrusted input until this class has approved it.
/// </summary>
public class ValidationService
{
    public class ValidationOutcome
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new();
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
    }

    public ValidationOutcome Validate(ExtractedInvoiceDto? dto)
    {
        var result = new ValidationOutcome();

        if (dto is null)
        {
            result.Errors.Add("No data was extracted from the document.");
            return result;
        }

        if (string.IsNullOrWhiteSpace(dto.VendorName))
            result.Errors.Add("Vendor name is missing.");

        if (string.IsNullOrWhiteSpace(dto.InvoiceNumber))
            result.Errors.Add("Invoice number is missing.");

        if (dto.Total is null)
            result.Errors.Add("Total amount is missing.");
        else if (dto.Total <= 0)
            result.Errors.Add("Total amount must be greater than zero.");

        if (dto.Subtotal is < 0) result.Errors.Add("Subtotal cannot be negative.");
        if (dto.Tax is < 0) result.Errors.Add("Tax amount cannot be negative.");

        result.InvoiceDate = ParseDate(dto.InvoiceDate, "Invoice date", result.Errors);
        result.DueDate = ParseDate(dto.DueDate, "Due date", result.Errors);

        for (var i = 0; i < dto.Items.Count; i++)
        {
            var item = dto.Items[i];
            var label = $"Line item {i + 1}";

            if (string.IsNullOrWhiteSpace(item.Description))
                result.Errors.Add($"{label}: description is missing.");
            if (item.Quantity is <= 0)
                result.Errors.Add($"{label}: quantity must be greater than zero.");
            if (item.UnitPrice is < 0)
                result.Errors.Add($"{label}: unit price cannot be negative.");
        }

        return result;
    }

    private static DateTime? ParseDate(string? value, string label, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                              DateTimeStyles.None, out var parsed))
            return parsed;

        errors.Add($"{label} '{value}' is not a valid date.");
        return null;
    }
}