// SmartSpendAI/Services/InvoiceService.cs

using Microsoft.EntityFrameworkCore;
using SmartSpendAI.Data;
using SmartSpendAI.DTOs;
using SmartSpendAI.Models;

namespace SmartSpendAI.Services;

public class InvoiceService
{
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;

    // FR-09: only PDFs, and a size ceiling
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;   // 10 MB
    private const string AllowedExtension = ".pdf";
    private const string UnknownVendorName = "Unknown Vendor";

    public InvoiceService(AppDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // ---------- READ ----------

    public async Task<List<InvoiceListItemDto>> GetAllAsync(string? status, string? riskLevel)
    {
        var query =
            from i in _db.Invoices.AsNoTracking()
            join v in _db.Vendors.AsNoTracking() on i.VendorId equals v.VendorId
            join a in _db.AIAnalyses.AsNoTracking() on i.InvoiceId equals a.InvoiceId into ax
            from a in ax.DefaultIfEmpty()
            select new InvoiceListItemDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceNumber = i.InvoiceNumber,
                VendorName = v.VendorName,
                TotalAmount = i.TotalAmount,
                Status = i.Status,
                RiskScore = a == null ? (decimal?)null : a.RiskScore,
                RiskLevel = a == null ? null : a.RiskLevel,
                CreatedAt = i.CreatedAt
            };

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(x => x.Status == status);

        if (!string.IsNullOrWhiteSpace(riskLevel))
            query = query.Where(x => x.RiskLevel == riskLevel);

        return await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
    }

    public async Task<InvoiceDetailDto?> GetByIdAsync(int id)
    {
        var invoice = await _db.Invoices
            .AsNoTracking()
            .Include(i => i.Vendor)
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.InvoiceId == id);

        if (invoice == null) return null;

        var analysis = await _db.AIAnalyses
            .AsNoTracking()
            .Where(a => a.InvoiceId == id)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        return new InvoiceDetailDto
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceNumber = invoice.InvoiceNumber,
            VendorId = invoice.VendorId,
            VendorName = invoice.Vendor?.VendorName ?? string.Empty,
            InvoiceDate = invoice.InvoiceDate,
            DueDate = invoice.DueDate,
            SubTotal = invoice.SubTotal,
            TaxAmount = invoice.TaxAmount,
            TotalAmount = invoice.TotalAmount,
            Status = invoice.Status,
            FilePath = invoice.FilePath,
            CreatedAt = invoice.CreatedAt,
            RiskScore = analysis?.RiskScore,
            RiskLevel = analysis?.RiskLevel,
            RiskReason = analysis?.AIReason,
            Items = invoice.Items.Select(it => new InvoiceItemDto
            {
                InvoiceItemId = it.InvoiceItemId,
                Description = it.Description,
                Quantity = it.Quantity,
                UnitPrice = it.UnitPrice,
                TotalPrice = it.TotalPrice
            }).ToList()
        };
    }

    // ---------- CREATE ----------

    public async Task<(bool Ok, string? Error, int InvoiceId)> CreateAsync(CreateInvoiceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.InvoiceNumber))
            return (false, "Invoice number is required.", 0);

        if (req.TotalAmount is not null && req.TotalAmount <= 0)
            return (false, "Total amount must be greater than zero.", 0);

        var vendorExists = await _db.Vendors.AnyAsync(v => v.VendorId == req.VendorId);
        if (!vendorExists)
            return (false, $"Vendor {req.VendorId} was not found.", 0);

        if (req.UploadedByUserId is not null)
        {
            var userExists = await _db.Users.AnyAsync(u => u.UserId == req.UploadedByUserId);
            if (!userExists)
                return (false, $"User {req.UploadedByUserId} was not found.", 0);
        }

        var invoice = new Invoice
        {
            VendorId = req.VendorId,
            InvoiceNumber = req.InvoiceNumber.Trim(),
            InvoiceDate = req.InvoiceDate,
            DueDate = req.DueDate,
            SubTotal = req.SubTotal,
            TaxAmount = req.TaxAmount,
            TotalAmount = req.TotalAmount,
            UploadedByUserId = req.UploadedByUserId,
            Status = InvoiceStatuses.Pending,
            Items = req.Items.Select(i => new InvoiceItem
            {
                Description = i.Description,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                TotalPrice = i.TotalPrice
            }).ToList()
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        return (true, null, invoice.InvoiceId);
    }

    // ---------- UPLOAD ----------

    public async Task<(bool Ok, string? Error, UploadResultDto? Result)> UploadAsync(
        IFormFile? file, int? vendorId, int? uploadedByUserId)
    {
        if (file is null || file.Length == 0)
            return (false, "No file was uploaded.", null);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != AllowedExtension)
            return (false, "Only PDF files are accepted.", null);

        if (file.Length > MaxFileSizeBytes)
            return (false, $"File exceeds the maximum size of {MaxFileSizeBytes / (1024 * 1024)} MB.", null);

        // Resolve the vendor. FR-07: an unrecognised vendor is recorded, not rejected.
        // Resolve the vendor. FR-07: an unrecognised vendor is recorded, not rejected.
        // Swagger sends 0 for blank optional numbers, so treat 0 as "not supplied".
        int resolvedVendorId;
        if (vendorId is > 0 && await _db.Vendors.AnyAsync(v => v.VendorId == vendorId))
            resolvedVendorId = vendorId.Value;
        else
            resolvedVendorId = await GetOrCreateUnknownVendorAsync();

        int? resolvedUserId = null;
        if (uploadedByUserId is > 0 && await _db.Users.AnyAsync(u => u.UserId == uploadedByUserId))
            resolvedUserId = uploadedByUserId;


        // Save the file under uploads/invoices with a collision-proof name
        var folder = Path.Combine(_env.ContentRootPath, "uploads", "invoices");
        Directory.CreateDirectory(folder);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(folder, storedFileName);

        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        var relativePath = Path.Combine("uploads", "invoices", storedFileName);

        // Placeholder invoice number — Day 3 replaces this with the extracted value
        var invoice = new Invoice
        {
            VendorId = resolvedVendorId,
            InvoiceNumber = $"PENDING-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Status = InvoiceStatuses.Pending,
            FilePath = relativePath,
            UploadedByUserId = resolvedUserId        
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync();

        return (true, null, new UploadResultDto
        {
            InvoiceId = invoice.InvoiceId,
            StoredFileName = storedFileName,
            FilePath = relativePath,
            Status = invoice.Status,
            Message = "File uploaded. Extraction runs in the next stage."
        });
    }

    // ---------- DELETE ----------

    public async Task<(bool Ok, string? Error)> DeleteAsync(int id)
    {
        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id);
        if (invoice is null)
            return (false, $"Invoice {id} was not found.");

        // Remove the analysis first — it references the invoice with NO ACTION
        var analyses = await _db.AIAnalyses.Where(a => a.InvoiceId == id).ToListAsync();
        if (analyses.Count > 0) _db.AIAnalyses.RemoveRange(analyses);

        // Line items cascade automatically
        _db.Invoices.Remove(invoice);
        await _db.SaveChangesAsync();

        // Delete the stored file too, but never fail the request over it
        if (!string.IsNullOrWhiteSpace(invoice.FilePath))
        {
            try
            {
                var fullPath = Path.Combine(_env.ContentRootPath, invoice.FilePath);
                if (File.Exists(fullPath)) File.Delete(fullPath);
            }
            catch { /* file already gone or locked — the record is what matters */ }
        }

        return (true, null);
    }

    // ---------- helpers ----------

    private async Task<int> GetOrCreateUnknownVendorAsync()
    {
        var vendor = await _db.Vendors.FirstOrDefaultAsync(v => v.VendorName == UnknownVendorName);
        if (vendor is not null) return vendor.VendorId;

        vendor = new Vendor { VendorName = UnknownVendorName };
        _db.Vendors.Add(vendor);
        await _db.SaveChangesAsync();
        return vendor.VendorId;
    }
}