using Microsoft.AspNetCore.Mvc;
using SmartSpendAI.DTOs;
using SmartSpendAI.Services;

namespace SmartSpendAI.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoiceController : ControllerBase
{
    private readonly InvoiceService _service;
    private readonly InvoiceProcessingService _processing;

    public InvoiceController(InvoiceService service, InvoiceProcessingService processing)
    {
        _service = service;
        _processing = processing;
    }

    /// <summary>List invoices, optionally filtered by status or risk level.</summary>
    [HttpGet]
    public async Task<ActionResult<List<InvoiceListItemDto>>> GetAll(
        [FromQuery] string? status, [FromQuery] string? riskLevel)
    {
        var invoices = await _service.GetAllAsync(status, riskLevel);
        return Ok(invoices);
    }

    /// <summary>Get one invoice with its line items and risk analysis.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceDetailDto>> GetById(int id)
    {
        var invoice = await _service.GetByIdAsync(id);
        if (invoice is null)
            return NotFound(new ApiError($"Invoice {id} was not found."));

        return Ok(invoice);
    }

    /// <summary>Create an invoice from supplied data.</summary>
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CreateInvoiceRequest request)
    {
        var (ok, error, invoiceId) = await _service.CreateAsync(request);
        if (!ok)
            return BadRequest(new ApiError(error!));

        return CreatedAtAction(nameof(GetById), new { id = invoiceId }, new { invoiceId });
    }

    /// <summary>Upload an invoice PDF. Stores the file and creates a pending invoice record.</summary>
    [HttpPost("upload")]
    [RequestSizeLimit(InvoiceService.MaxFileSizeBytes)]
    public async Task<ActionResult<UploadResultDto>> Upload(
        IFormFile file,
        [FromForm] int? vendorId,
        [FromForm] int? uploadedByUserId)
    {
        var (ok, error, result) = await _service.UploadAsync(file, vendorId, uploadedByUserId);
        if (!ok)
            return BadRequest(new ApiError(error!));

        return Ok(result);
    }

    /// <summary>Extract the invoice data with AI, validate it, save it and score its risk.</summary>
    [HttpPost("{id:int}/extract")]
    public async Task<ActionResult<ExtractionResponseDto>> Extract(int id)
    {
        var result = await _processing.ProcessAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>Re-run only the risk checks against an invoice that is already extracted.</summary>
    [HttpPost("{id:int}/reassess")]
    public async Task<ActionResult<RiskAssessment>> Reassess(int id)
    {
        var assessment = await _processing.ReassessAsync(id);
        if (assessment is null)
            return NotFound(new ApiError($"Invoice {id} was not found."));

        return Ok(assessment);
    }

    /// <summary>Delete an invoice, its line items, its analysis and its stored file.</summary>
    [HttpDelete("{id:int}")]
    public async Task<ActionResult> Delete(int id)
    {
        var (ok, error) = await _service.DeleteAsync(id);
        if (!ok)
            return NotFound(new ApiError(error!));

        return NoContent();
    }
}