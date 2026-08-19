// SmartSpendAI/Models/Entities.cs
// All entities and status constants in one file.
// Matches the Database Design Document v1.0 naming conventions.

namespace SmartSpendAI.Models;

// ============================================================
//  Status constants
// ============================================================

public static class ExpenseStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string UnderReview = "UnderReview";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Paid = "Paid";
}

public static class InvoiceStatuses
{
    public const string Pending = "Pending";
    public const string Verified = "Verified";
    public const string Flagged = "Flagged";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Paid = "Paid";
}

public static class ApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}

public static class RiskLevels
{
    public const string Low = "LOW";
    public const string Medium = "MEDIUM";
    public const string High = "HIGH";
}

// ============================================================
//  Wave 1 — Master data
// ============================================================

public class Role
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}

public class Department
{
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;

    public ICollection<User> Users { get; set; } = new List<User>();
}

public class User
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    public int RoleId { get; set; }
    public Role? Role { get; set; }

    public int DepartmentId { get; set; }
    public Department? Department { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

public class Vendor
{
    public int VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string? GSTNumber { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}

public class ExpensePolicy
{
    public int PolicyId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal MaximumAmount { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}

// ============================================================
//  Wave 2 — Transactions
// ============================================================

public class ExpenseClaim
{
    public int ExpenseId { get; set; }

    public int EmployeeId { get; set; }
    public User? Employee { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string Status { get; set; } = ExpenseStatuses.Draft;
    public DateTime CreatedAt { get; set; }

    public ICollection<ExpenseItem> Items { get; set; } = new List<ExpenseItem>();
    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}

public class ExpenseItem
{
    public int ExpenseItemId { get; set; }

    public int ExpenseId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public string ItemDescription { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Amount { get; set; }
}

public class Receipt
{
    public int ReceiptId { get; set; }

    public int ExpenseId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public string FileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? FileType { get; set; }
    public DateTime UploadedAt { get; set; }
}

public class Invoice
{
    public int InvoiceId { get; set; }

    public int VendorId { get; set; }
    public Vendor? Vendor { get; set; }

    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime? InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }

    public decimal? SubTotal { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? TotalAmount { get; set; }

    public string Status { get; set; } = InvoiceStatuses.Pending;
    public string? FilePath { get; set; }

    // Added beyond the design document — required by FR-11 and FR-23
    public int? UploadedByUserId { get; set; }
    public User? UploadedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<InvoiceItem> Items { get; set; } = new List<InvoiceItem>();
}

public class InvoiceItem
{
    public int InvoiceItemId { get; set; }

    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

// ============================================================
//  Wave 3 — Workflow
// ============================================================

public class Approval
{
    public int ApprovalId { get; set; }

    public int ExpenseId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public int ApproverId { get; set; }
    public User? Approver { get; set; }

    public int ApprovalLevel { get; set; }
    public string Status { get; set; } = ApprovalStatuses.Pending;
    public string? Comments { get; set; }
    public DateTime? ApprovedAt { get; set; }

    public ICollection<ApprovalHistory> History { get; set; } = new List<ApprovalHistory>();
}

public class ApprovalHistory
{
    public int HistoryId { get; set; }

    public int ApprovalId { get; set; }
    public Approval? Approval { get; set; }

    public string? OldStatus { get; set; }
    public string? NewStatus { get; set; }

    public int ActionBy { get; set; }
    public User? ActionByUser { get; set; }

    public string? Comments { get; set; }
    public DateTime ActionAt { get; set; }
}

public class Payment
{
    public int PaymentId { get; set; }

    public int? ExpenseId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public decimal Amount { get; set; }
    public string PaymentStatus { get; set; } = PaymentStatuses.Pending;
    public DateTime? PaymentDate { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ============================================================
//  Wave 4 — Analysis and audit
// ============================================================

public class AIAnalysis
{
    public int AIAnalysisId { get; set; }

    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public int? ExpenseId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public string? ExtractedVendor { get; set; }
    public string? ExtractedInvoiceNumber { get; set; }
    public decimal? ExtractedAmount { get; set; }
    public decimal? ExtractedTax { get; set; }

    public string? Category { get; set; }
    public decimal? RiskScore { get; set; }
    public string? RiskLevel { get; set; }
    public string? AIReason { get; set; }
    public string? ModelVersion { get; set; }

    // Added beyond the design document — raw AI response for auditability (NFR-10)
    public string? RawResponse { get; set; }

    public DateTime CreatedAt { get; set; }
}

public class PolicyViolation
{
    public int ViolationId { get; set; }

    public int? ExpenseId { get; set; }
    public ExpenseClaim? ExpenseClaim { get; set; }

    public int? InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }

    public int? PolicyId { get; set; }
    public ExpensePolicy? Policy { get; set; }

    public string ViolationType { get; set; } = string.Empty;
    public decimal? ExpectedAmount { get; set; }
    public decimal? ActualAmount { get; set; }
    public decimal? DifferenceAmount { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLog
{
    public int AuditLogId { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public int? EntityId { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? IPAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Notification
{
    public int NotificationId { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
