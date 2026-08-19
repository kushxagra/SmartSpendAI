// SmartSpendAI/Services/RiskService.cs

using Microsoft.EntityFrameworkCore;
using SmartSpendAI.Data;
using SmartSpendAI.Models;

namespace SmartSpendAI.Services;

public class RiskCheckResult
{
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int Points { get; set; }
}

public class RiskAssessment
{
    public int Score { get; set; }
    public string Level { get; set; } = RiskLevels.Low;
    public List<RiskCheckResult> Triggered { get; set; } = new();

    public string ReasonText =>
        Triggered.Count == 0
            ? "No risk factors detected."
            : string.Join(" | ", Triggered.Select(t => $"{t.Description} (+{t.Points})"));
}

/// <summary>
/// The three validation checks and the scoring engine (BR-01 to BR-05).
///
/// None of this uses AI. These are deterministic business rules, so the same
/// invoice always produces the same score and every point can be explained.
/// </summary>
public class RiskService
{
    public const int DuplicatePoints = 40;
    public const int AmountAnomalyPoints = 20;
    public const int TaxMismatchPoints = 20;
    public const int MissingInformationPoints = 10;
    public const int UnknownVendorPoints = 10;

    private const decimal TaxTolerance = 0.01m;

    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public RiskService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<RiskAssessment?> AssessAsync(int invoiceId)
    {
        var invoice = await _db.Invoices
            .Include(i => i.Vendor)
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);

        if (invoice is null) return null;

        var assessment = new RiskAssessment();

        await CheckDuplicateAsync(invoice, assessment);
        await CheckAmountAnomalyAsync(invoice, assessment);
        CheckTax(invoice, assessment);
        CheckMissingInformation(invoice, assessment);
        CheckUnknownVendor(invoice, assessment);

        assessment.Score = Math.Min(100, assessment.Triggered.Sum(t => t.Points));
        assessment.Level = ToLevel(assessment.Score);

        return assessment;
    }

    // ---------- BR-01: duplicate ----------

    private async Task CheckDuplicateAsync(Invoice invoice, RiskAssessment assessment)
    {
        var isDuplicate = await _db.Invoices.AnyAsync(i =>
            i.InvoiceId != invoice.InvoiceId &&
            i.VendorId == invoice.VendorId &&
            i.InvoiceNumber == invoice.InvoiceNumber &&
            i.TotalAmount == invoice.TotalAmount);

        if (isDuplicate)
            Add(assessment, "DUPLICATE",
                $"Possible duplicate: invoice {invoice.InvoiceNumber} for this vendor and amount already exists",
                DuplicatePoints);
    }

    // ---------- BR-02: amount anomaly ----------

    private async Task CheckAmountAnomalyAsync(Invoice invoice, RiskAssessment assessment)
    {
        if (invoice.TotalAmount is null) return;

        var multiplier = _config.GetValue<decimal?>("Risk:AnomalyMultiplier") ?? 2.0m;
        var minimumHistory = _config.GetValue<int?>("Risk:MinimumHistoryCount") ?? 2;

        var history = await _db.Invoices
            .Where(i => i.VendorId == invoice.VendorId
                     && i.InvoiceId != invoice.InvoiceId
                     && i.TotalAmount != null)
            .Select(i => i.TotalAmount!.Value)
            .ToListAsync();

        // Without enough history an "average" means nothing, so we do not guess.
        if (history.Count < minimumHistory) return;

        var average = history.Average();
        var threshold = average * multiplier;

        if (invoice.TotalAmount > threshold)
            Add(assessment, "AMOUNT_ANOMALY",
                $"Unusually high amount: {invoice.TotalAmount:N2} against a vendor average of {average:N2}",
                AmountAnomalyPoints);
    }

    // ---------- BR-03: tax ----------

    private static void CheckTax(Invoice invoice, RiskAssessment assessment)
    {
        if (invoice.SubTotal is null || invoice.TaxAmount is null || invoice.TotalAmount is null)
            return;

        var expected = invoice.SubTotal.Value + invoice.TaxAmount.Value;
        var difference = Math.Abs(expected - invoice.TotalAmount.Value);

        if (difference > TaxTolerance)
            Add(assessment, "TAX_MISMATCH",
                $"Amount mismatch: subtotal plus tax is {expected:N2} but the stated total is {invoice.TotalAmount:N2}",
                TaxMismatchPoints);
    }

    // ---------- missing information ----------

    private static void CheckMissingInformation(Invoice invoice, RiskAssessment assessment)
    {
        var missing = new List<string>();

        if (invoice.InvoiceDate is null) missing.Add("invoice date");
        if (invoice.DueDate is null) missing.Add("due date");
        if (invoice.SubTotal is null) missing.Add("subtotal");
        if (invoice.TaxAmount is null) missing.Add("tax amount");
        if (invoice.Items.Count == 0) missing.Add("line items");

        if (missing.Count > 0)
            Add(assessment, "MISSING_INFORMATION",
                $"Missing information: {string.Join(", ", missing)}",
                MissingInformationPoints);
    }

    // ---------- unknown vendor ----------

    private static void CheckUnknownVendor(Invoice invoice, RiskAssessment assessment)
    {
        // A vendor with no GST number was created automatically from an upload
        // rather than being a maintained master record (FR-07).
        if (invoice.Vendor is null || string.IsNullOrWhiteSpace(invoice.Vendor.GSTNumber))
            Add(assessment, "UNKNOWN_VENDOR",
                $"Unknown vendor: '{invoice.Vendor?.VendorName ?? "unresolved"}' is not an established vendor record",
                UnknownVendorPoints);
    }

    // ---------- BR-05: bands ----------

    public static string ToLevel(int score) => score switch
    {
        <= 30 => RiskLevels.Low,
        <= 60 => RiskLevels.Medium,
        _ => RiskLevels.High
    };

    private static void Add(RiskAssessment assessment, string code, string description, int points) =>
        assessment.Triggered.Add(new RiskCheckResult
        {
            Code = code,
            Description = description,
            Points = points
        });
}