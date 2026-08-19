using Microsoft.EntityFrameworkCore;
using SmartSpendAI.Data;
using SmartSpendAI.DTOs;
using SmartSpendAI.Models;

namespace SmartSpendAI.Services;

/// <summary>
/// Orchestrates the pipeline:
/// stored PDF -> text -> AI -> validate -> persist -> risk assessment.
/// Nothing reaches the Invoice table until ValidationService has approved it,
/// and the risk score is calculated by deterministic rules, never by the AI.
/// </summary>
public class InvoiceProcessingService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly PdfTextExtractor _pdf;
    private readonly AIService _ai;
    private readonly ValidationService _validator;
    private readonly RiskService _risk;

    public InvoiceProcessingService(
        AppDbContext db,
        IWebHostEnvironment env,
        PdfTextExtractor pdf,
        AIService ai,
        ValidationService validator,
        RiskService risk)
    {
        _db = db;
        _env = env;
        _pdf = pdf;
        _ai = ai;
        _validator = validator;
        _risk = risk;
    }

    public async Task<ExtractionResponseDto> ProcessAsync(int invoiceId)
    {
        var result = new ExtractionResponseDto { InvoiceId = invoiceId };

        var invoice = await _db.Invoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

        if (invoice is null)
        {
            result.Message = $"Invoice {invoiceId} was not found.";
            return result;
        }

        if (string.IsNullOrWhiteSpace(invoice.FilePath))
        {
            result.Message = "This invoice has no uploaded file to read.";
            return result;
        }

        var fullPath = Path.Combine(_env.ContentRootPath, invoice.FilePath);
        if (!File.Exists(fullPath))
        {
            result.Message = "The uploaded file is missing from disk.";
            return result;
        }

        // ---- 1. PDF -> text ----
        string text;
        try
        {
            text = _pdf.Extract(fullPath);
        }
        catch (Exception ex)
        {
            result.Message = $"The PDF could not be read: {ex.Message}";
            return result;
        }

        if (text.Length < 40)
        {
            result.Message = "Almost no text was found in the PDF. "
                           + "It may be a scanned image, which this version does not support.";
            return result;
        }

        // ---- 2. text -> AI ----
        var (ok, error, raw, data, model) = await _ai.ExtractAsync(text);

        // ---- 3. validate ----
        var validation = _validator.Validate(data);

        // The raw response is stored either way (NFR-10 auditability)
        var analysis = new AIAnalysis
        {
            InvoiceId = invoice.InvoiceId,
            RawResponse = string.IsNullOrWhiteSpace(raw) ? null : raw,
            ModelVersion = model,
            ExtractedVendor = data?.VendorName,
            ExtractedInvoiceNumber = data?.InvoiceNumber,
            ExtractedAmount = data?.Total,
            ExtractedTax = data?.Tax,
            AIReason = "Extraction failed."
        };
        _db.AIAnalyses.Add(analysis);

        if (!ok)
        {
            analysis.AIReason = Truncate(error ?? "Extraction failed.", 1000);
            invoice.Status = InvoiceStatuses.Flagged;
            await _db.SaveChangesAsync();

            result.Message = error ?? "Extraction failed.";
            return result;
        }

        if (!validation.IsValid)
        {
            analysis.AIReason = Truncate(string.Join(" ", validation.Errors), 1000);
            invoice.Status = InvoiceStatuses.Flagged;
            await _db.SaveChangesAsync();

            result.Message = "Extraction completed but the data failed validation. "
                           + "The invoice was not updated.";
            result.ValidationErrors = validation.Errors;
            result.Extracted = data;
            return result;
        }

        // ---- 4. persist ----
        invoice.VendorId = await ResolveVendorAsync(data!.VendorName!);
        invoice.InvoiceNumber = data.InvoiceNumber!.Trim();
        invoice.InvoiceDate = validation.InvoiceDate;
        invoice.DueDate = validation.DueDate;
        invoice.SubTotal = data.Subtotal;
        invoice.TaxAmount = data.Tax;
        invoice.TotalAmount = data.Total;

        // Replace any previous line items so re-running is safe
        if (invoice.Items.Count > 0) _db.InvoiceItems.RemoveRange(invoice.Items);

        foreach (var item in data.Items)
        {
            _db.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.InvoiceId,
                Description = string.IsNullOrWhiteSpace(item.Description)
                    ? "(no description)"
                    : item.Description.Trim(),
                Quantity = item.Quantity ?? 1,
                UnitPrice = item.UnitPrice ?? 0,
                TotalPrice = item.Total ?? (item.Quantity ?? 1) * (item.UnitPrice ?? 0)
            });
        }

        await _db.SaveChangesAsync();

        // ---- 5. risk assessment (runs on the saved data) ----
        var assessment = await _risk.AssessAsync(invoice.InvoiceId);
        if (assessment is not null)
        {
            analysis.RiskScore = assessment.Score;
            analysis.RiskLevel = assessment.Level;
            analysis.AIReason = Truncate(assessment.ReasonText, 1000);

            invoice.Status = assessment.Score > 30
                ? InvoiceStatuses.Flagged
                : InvoiceStatuses.Verified;

            await _db.SaveChangesAsync();
        }

        result.Success = true;
        result.Message = assessment is null
            ? "Invoice extracted and saved."
            : $"Invoice extracted and saved. Risk score {assessment.Score} ({assessment.Level}).";
        result.Extracted = data;
        return result;
    }

    /// <summary>Re-run only the risk checks against an already-extracted invoice.</summary>
    public async Task<RiskAssessment?> ReassessAsync(int invoiceId)
    {
        var assessment = await _risk.AssessAsync(invoiceId);
        if (assessment is null) return null;

        var invoice = await _db.Invoices.FirstAsync(i => i.InvoiceId == invoiceId);

        var analysis = await _db.AIAnalyses
            .Where(a => a.InvoiceId == invoiceId)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        if (analysis is null)
        {
            analysis = new AIAnalysis { InvoiceId = invoiceId };
            _db.AIAnalyses.Add(analysis);
        }

        analysis.RiskScore = assessment.Score;
        analysis.RiskLevel = assessment.Level;
        analysis.AIReason = Truncate(assessment.ReasonText, 1000);

        invoice.Status = assessment.Score > 30
            ? InvoiceStatuses.Flagged
            : InvoiceStatuses.Verified;

        await _db.SaveChangesAsync();
        return assessment;
    }

    /// <summary>Match the extracted vendor by name, or create a new vendor record.</summary>
    private async Task<int> ResolveVendorAsync(string vendorName)
    {
        var name = vendorName.Trim();

        var existing = await _db.Vendors
            .FirstOrDefaultAsync(v => v.VendorName.ToLower() == name.ToLower());

        if (existing is not null) return existing.VendorId;

        var vendor = new Vendor { VendorName = name };
        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();
        return vendor.VendorId;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}