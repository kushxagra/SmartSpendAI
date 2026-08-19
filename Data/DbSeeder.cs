// SmartSpendAI/Data/DbSeeder.cs

using SmartSpendAI.Models;

namespace SmartSpendAI.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        if (db.Roles.Any()) return;   // only seed an empty database

        var roles = new[]
        {
            new Role { RoleName = "Admin" },
            new Role { RoleName = "Manager" },
            new Role { RoleName = "Finance" },
            new Role { RoleName = "Employee" }
        };
        db.Roles.AddRange(roles);

        var depts = new[]
        {
            new Department { DepartmentName = "Finance" },
            new Department { DepartmentName = "Engineering" },
            new Department { DepartmentName = "Operations" }
        };
        db.Departments.AddRange(depts);

        db.SaveChanges();   // generates the IDs used below

        // TODO: replace TEMP hashes with real hashing on Day 7
        db.Users.AddRange(
            new User
            {
                FullName = "Admin User",
                Email = "admin@smartspend.com",
                PasswordHash = "TEMP",
                RoleId = roles[0].RoleId,
                DepartmentId = depts[0].DepartmentId
            },
            new User
            {
                FullName = "Finance User",
                Email = "finance@smartspend.com",
                PasswordHash = "TEMP",
                RoleId = roles[2].RoleId,
                DepartmentId = depts[0].DepartmentId
            },
            new User
            {
                FullName = "Employee User",
                Email = "employee@smartspend.com",
                PasswordHash = "TEMP",
                RoleId = roles[3].RoleId,
                DepartmentId = depts[1].DepartmentId
            }
        );

        db.Vendors.AddRange(
            new Vendor
            {
                VendorName = "ABC Technologies",
                GSTNumber = "29ABCDE1234F1Z5",
                Email = "billing@abctech.com",
                Phone = "9876543210"
            },
            new Vendor
            {
                VendorName = "Global Supplies Ltd",
                GSTNumber = "27GLOBL5678K1Z2",
                Email = "accounts@globalsupplies.com",
                Phone = "9812345678"
            }
        );

        db.ExpensePolicies.AddRange(
            new ExpensePolicy { PolicyName = "Travel Limit", Category = "Travel", MaximumAmount = 25000m },
            new ExpensePolicy { PolicyName = "Meal Limit", Category = "Meals", MaximumAmount = 2000m },
            new ExpensePolicy { PolicyName = "Equipment Limit", Category = "Equipment", MaximumAmount = 100000m }
        );

        db.SaveChanges();
    }
}
