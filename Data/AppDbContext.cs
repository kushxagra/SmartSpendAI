
// SmartSpendAI/Data/AppDbContext.cs
// Complete configuration. Nothing left as "apply the same pattern".

using Microsoft.EntityFrameworkCore;
using SmartSpendAI.Models;

namespace SmartSpendAI.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<ExpensePolicy> ExpensePolicies => Set<ExpensePolicy>();
    public DbSet<ExpenseClaim> ExpenseClaims => Set<ExpenseClaim>();
    public DbSet<ExpenseItem> ExpenseItems => Set<ExpenseItem>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Approval> Approvals => Set<Approval>();
    public DbSet<ApprovalHistory> ApprovalHistory => Set<ApprovalHistory>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AIAnalysis> AIAnalyses => Set<AIAnalysis>();
    public DbSet<PolicyViolation> PolicyViolations => Set<PolicyViolation>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<ExpensePolicy>().HasKey(x => x.PolicyId);
        b.Entity<ExpenseClaim>().HasKey(x => x.ExpenseId);
        b.Entity<ApprovalHistory>().HasKey(x => x.HistoryId);
        b.Entity<PolicyViolation>().HasKey(x => x.ViolationId);

        const string Now = "SYSUTCDATETIME()";

        // ---------------- Roles ----------------
        b.Entity<Role>(e =>
        {
            e.Property(x => x.RoleName).IsRequired().HasMaxLength(50);
            e.HasIndex(x => x.RoleName).IsUnique();
        });

        // ---------------- Departments ----------------
        b.Entity<Department>(e =>
        {
            e.Property(x => x.DepartmentName).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.DepartmentName).IsUnique();
        });

        // ---------------- Users ----------------
        b.Entity<User>(e =>
        {
            e.Property(x => x.FullName).IsRequired().HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(150);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);

            e.HasIndex(x => x.Email).IsUnique();

            e.HasOne(x => x.Role).WithMany(r => r.Users)
             .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Department).WithMany(d => d.Users)
             .HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Vendors ----------------
        b.Entity<Vendor>(e =>
        {
            e.Property(x => x.VendorName).IsRequired().HasMaxLength(150);
            e.Property(x => x.GSTNumber).HasMaxLength(20);
            e.Property(x => x.Email).HasMaxLength(150);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Address).HasMaxLength(300);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);
        });

        // ---------------- ExpensePolicies ----------------
        b.Entity<ExpensePolicy>(e =>
        {
            e.Property(x => x.PolicyName).IsRequired().HasMaxLength(150);
            e.Property(x => x.Category).IsRequired().HasMaxLength(100);
            e.Property(x => x.MaximumAmount).HasPrecision(18, 2);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);
        });

        // ---------------- ExpenseClaims ----------------
        b.Entity<ExpenseClaim>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.ExpenseDate).HasColumnType("date");
            e.Property(x => x.Status).IsRequired().HasMaxLength(30);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);

            e.HasOne(x => x.Employee).WithMany()
             .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- ExpenseItems ----------------
        b.Entity<ExpenseItem>(e =>
        {
            e.Property(x => x.ItemDescription).IsRequired().HasMaxLength(200);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.Amount).HasPrecision(18, 2);

            e.HasOne(x => x.ExpenseClaim).WithMany(c => c.Items)
             .HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- Receipts ----------------
        b.Entity<Receipt>(e =>
        {
            e.Property(x => x.FileName).IsRequired().HasMaxLength(255);
            e.Property(x => x.FilePath).IsRequired().HasMaxLength(500);
            e.Property(x => x.FileType).HasMaxLength(50);
            e.Property(x => x.UploadedAt).HasDefaultValueSql(Now);

            e.HasOne(x => x.ExpenseClaim).WithMany(c => c.Receipts)
             .HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- Invoices ----------------
        b.Entity<Invoice>(e =>
        {
            e.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(100);
            e.Property(x => x.InvoiceDate).HasColumnType("date");
            e.Property(x => x.DueDate).HasColumnType("date");
            e.Property(x => x.SubTotal).HasPrecision(18, 2);
            e.Property(x => x.TaxAmount).HasPrecision(18, 2);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.Status).IsRequired().HasMaxLength(30);
            e.Property(x => x.FilePath).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);

            // Duplicate check (BR-01) — deliberately NOT unique
            e.HasIndex(x => new { x.VendorId, x.InvoiceNumber });

            e.HasOne(x => x.Vendor).WithMany(v => v.Invoices)
             .HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.UploadedBy).WithMany()
             .HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- InvoiceItems ----------------
        b.Entity<InvoiceItem>(e =>
        {
            e.Property(x => x.Description).IsRequired().HasMaxLength(300);
            e.Property(x => x.Quantity).HasPrecision(18, 3);
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.TotalPrice).HasPrecision(18, 2);

            e.HasOne(x => x.Invoice).WithMany(i => i.Items)
             .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---------------- Approvals ----------------
        b.Entity<Approval>(e =>
        {
            e.Property(x => x.Status).IsRequired().HasMaxLength(30);
            e.Property(x => x.Comments).HasMaxLength(500);

            e.HasOne(x => x.ExpenseClaim).WithMany()
             .HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Approver).WithMany()
             .HasForeignKey(x => x.ApproverId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- ApprovalHistory ----------------
        b.Entity<ApprovalHistory>(e =>
        {
            e.ToTable("ApprovalHistory");
            e.Property(x => x.OldStatus).HasMaxLength(30);
            e.Property(x => x.NewStatus).HasMaxLength(30);
            e.Property(x => x.Comments).HasMaxLength(500);
            e.Property(x => x.ActionAt).HasDefaultValueSql(Now);

            e.HasOne(x => x.Approval).WithMany(a => a.History)
             .HasForeignKey(x => x.ApprovalId).OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.ActionByUser).WithMany()
             .HasForeignKey(x => x.ActionBy).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Payments ----------------
        b.Entity<Payment>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.Property(x => x.PaymentStatus).IsRequired().HasMaxLength(30);
            e.Property(x => x.PaymentDate).HasColumnType("date");
            e.Property(x => x.TransactionReference).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);

            e.HasOne(x => x.ExpenseClaim).WithMany()
             .HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Invoice).WithMany()
             .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- AIAnalyses ----------------
        b.Entity<AIAnalysis>(e =>
        {
            e.Property(x => x.ExtractedVendor).HasMaxLength(200);
            e.Property(x => x.ExtractedInvoiceNumber).HasMaxLength(100);
            e.Property(x => x.ExtractedAmount).HasPrecision(18, 2);
            e.Property(x => x.ExtractedTax).HasPrecision(18, 2);
            e.Property(x => x.Category).HasMaxLength(100);
            e.Property(x => x.RiskScore).HasPrecision(5, 2);
            e.Property(x => x.RiskLevel).HasMaxLength(20);
            e.Property(x => x.AIReason).HasMaxLength(1000);
            e.Property(x => x.ModelVersion).HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);
            // RawResponse: no MaxLength → nvarchar(max)

            e.HasIndex(x => x.InvoiceId);

            e.HasOne(x => x.Invoice).WithMany()
             .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.ExpenseClaim).WithMany()
             .HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- PolicyViolations ----------------
        b.Entity<PolicyViolation>(e =>
        {
            e.Property(x => x.ViolationType).IsRequired().HasMaxLength(100);
            e.Property(x => x.ExpectedAmount).HasPrecision(18, 2);
            e.Property(x => x.ActualAmount).HasPrecision(18, 2);
            e.Property(x => x.DifferenceAmount).HasPrecision(18, 2);
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);

            e.HasOne(x => x.ExpenseClaim).WithMany()
             .HasForeignKey(x => x.ExpenseId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Invoice).WithMany()
             .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Policy).WithMany()
             .HasForeignKey(x => x.PolicyId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- AuditLogs ----------------
        b.Entity<AuditLog>(e =>
        {
            e.Property(x => x.Action).IsRequired().HasMaxLength(100);
            e.Property(x => x.EntityName).HasMaxLength(100);
            e.Property(x => x.IPAddress).HasMaxLength(50);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);
            // OldValue / NewValue: no MaxLength → nvarchar(max)

            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Notifications ----------------
        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Title).IsRequired().HasMaxLength(200);
            e.Property(x => x.Message).IsRequired().HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasDefaultValueSql(Now);

            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
