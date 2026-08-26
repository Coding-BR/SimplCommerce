using IdealCreative.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdealCreative.Api.Data;

public static class DatabaseBootstrap
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
        // EnsureCreated does not alter an existing development database. These idempotent
        // statements keep newly added commerce tables available after an API upgrade.
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS "Carts" ("UserId" text PRIMARY KEY, "ItemsJson" text NOT NULL DEFAULT '[]', "CouponCode" text NULL, "DiscountCents" bigint NOT NULL DEFAULT 0, "ShippingZipCode" text NULL, "UpdatedAt" timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS "Coupons" ("Code" text PRIMARY KEY, "DiscountType" text NOT NULL, "Value" numeric NOT NULL, "MinPurchaseCents" bigint NOT NULL DEFAULT 0, "StartDate" timestamptz NULL, "EndDate" timestamptz NULL, "MaxUsesGlobal" integer NULL, "MaxUsesPerUser" integer NULL, "CurrentUsesGlobal" integer NOT NULL DEFAULT 0, "IsActive" boolean NOT NULL DEFAULT true, "CreatedAt" timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS "Orders" ("Id" uuid PRIMARY KEY, "UserId" text NOT NULL, "ItemsJson" text NOT NULL DEFAULT '[]', "SubtotalCents" bigint NOT NULL, "DiscountCents" bigint NOT NULL DEFAULT 0, "TotalCents" bigint NOT NULL, "Status" text NOT NULL, "PaymentMethod" text NULL, "ShippingAddress" text NULL, "CustomerName" text NULL, "CustomerEmail" text NULL, "CustomerPhone" text NULL, "ZipCode" text NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS "Categories" ("Id" uuid PRIMARY KEY, "Title" text NOT NULL, "ImageUrl" text NULL, "Priority" integer NOT NULL DEFAULT 0, "CreatedAt" timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS "Tags" ("Id" uuid PRIMARY KEY, "Title" text NOT NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS "Reviews" ("Id" uuid PRIMARY KEY, "ProductId" uuid NOT NULL, "UserId" text NOT NULL, "Rating" integer NOT NULL, "Comment" text NOT NULL, "IsApproved" boolean NOT NULL DEFAULT true, "CreatedAt" timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS "AppSettings" ("Key" text PRIMARY KEY, "ValueJson" text NOT NULL DEFAULT '{{}}', "UpdatedAt" timestamptz NOT NULL DEFAULT now());
            CREATE TABLE IF NOT EXISTS "PaymentTransactions" ("Id" uuid PRIMARY KEY, "OrderId" uuid NOT NULL, "Provider" text NOT NULL, "ProviderPaymentId" text NOT NULL, "Status" text NOT NULL, "AmountCents" bigint NOT NULL, "RawPayload" text NULL, "CreatedAt" timestamptz NOT NULL DEFAULT now(), "UpdatedAt" timestamptz NULL);
            CREATE TABLE IF NOT EXISTS "PrivacyRequests" ("Id" uuid PRIMARY KEY, "UserId" text NOT NULL, "Type" text NOT NULL, "Status" text NOT NULL, "RequestedAt" timestamptz NOT NULL DEFAULT now(), "ProcessedAt" timestamptz NULL, "BlockingReason" text NULL, "LegalBasis" text NOT NULL DEFAULT 'LGPD-Art16', "RetentionUntil" timestamptz NULL, "Notes" text NULL);
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PaymentTransactions_Provider_ProviderPaymentId" ON "PaymentTransactions" ("Provider", "ProviderPaymentId");
            CREATE INDEX IF NOT EXISTS "IX_Orders_UserId_CreatedAt" ON "Orders" ("UserId", "CreatedAt" DESC);
            CREATE INDEX IF NOT EXISTS "IX_Orders_Status_CreatedAt" ON "Orders" ("Status", "CreatedAt" DESC);
            CREATE INDEX IF NOT EXISTS "IX_Reviews_ProductId_CreatedAt" ON "Reviews" ("ProductId", "CreatedAt" DESC);
            CREATE INDEX IF NOT EXISTS "IX_PrivacyRequests_UserId_Status" ON "PrivacyRequests" ("UserId", "Status");
            CREATE INDEX IF NOT EXISTS "IX_Products_Published_Category" ON "Products" ("IsPublished", "CategoryId");
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "BirthDate" timestamp with time zone NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "Street" text NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "Number" text NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "Neighborhood" text NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "City" text NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "State" text NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "ZipCode" text NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "Country" text NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "CustomerDocument" text NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "UpdatedAt" timestamp with time zone NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "AccountState" text NOT NULL DEFAULT 'Active';
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "DeletionRequestedAt" timestamptz NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "DeactivatedAt" timestamptz NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "AnonymizedAt" timestamptz NULL;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "TokenVersion" integer NOT NULL DEFAULT 0;
            ALTER TABLE "AspNetUsers" ADD COLUMN IF NOT EXISTS "RetentionUntil" timestamptz NULL;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "PaymentProvider" text NULL;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "PaymentIntentId" text NULL;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "TransactionId" text NULL;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "CouponCode" text NULL;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "PaidAt" timestamptz NULL;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "PaymentFailureReason" text NULL;
            ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShippingCents" bigint NOT NULL DEFAULT 0;
            ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "DigitalFilePath" text NULL;
            ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "HideDigitalFromCustomer" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "CategoryId" uuid NULL;
            ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "TagsJson" text NOT NULL DEFAULT '[]';
            ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "Views" integer NOT NULL DEFAULT 0;
            ALTER TABLE "Products" ADD COLUMN IF NOT EXISTS "SalesCount" integer NOT NULL DEFAULT 0;
        """);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var roleName in new[] { "Admin", "Customer" })
        {
            if (await roles.RoleExistsAsync(roleName)) continue;
            var roleResult = await roles.CreateAsync(new IdentityRole(roleName));
            if (!roleResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        var email = configuration["Admin:Email"];
        var password = configuration["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            return;

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = "Administrador" };
            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        }
        else
        {
            var token = await users.GeneratePasswordResetTokenAsync(admin);
            await users.ResetPasswordAsync(admin, token, password);
        }

        if (!await users.IsInRoleAsync(admin, "Admin"))
        {
            var roleResult = await users.AddToRoleAsync(admin, "Admin");
            if (!roleResult.Succeeded)
                throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }
    }
}
