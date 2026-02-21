using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CowetaConnect.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedDefaultCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "categories",
                columns: ["id", "name", "slug", "icon", "parent_id"],
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), "Food & Beverage",  "food-beverage",   "🍽️", null },
                    { new Guid("11111111-0000-0000-0000-000000000002"), "Retail",           "retail",          "🛍️", null },
                    { new Guid("11111111-0000-0000-0000-000000000003"), "Services",         "services",        "🔧", null },
                    { new Guid("11111111-0000-0000-0000-000000000004"), "Agriculture",      "agriculture",     "🌾", null },
                    { new Guid("11111111-0000-0000-0000-000000000005"), "Artisan & Crafts", "artisan-crafts",  "🎨", null },
                    { new Guid("11111111-0000-0000-0000-000000000006"), "Health & Wellness","health-wellness", "💊", null },
                    { new Guid("11111111-0000-0000-0000-000000000007"), "Entertainment",    "entertainment",   "🎭", null },
                    { new Guid("11111111-0000-0000-0000-000000000008"), "Automotive",       "automotive",      "🚗", null },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "categories",
                keyColumn: "id",
                keyValues:
                [
                    new Guid("11111111-0000-0000-0000-000000000001"),
                    new Guid("11111111-0000-0000-0000-000000000002"),
                    new Guid("11111111-0000-0000-0000-000000000003"),
                    new Guid("11111111-0000-0000-0000-000000000004"),
                    new Guid("11111111-0000-0000-0000-000000000005"),
                    new Guid("11111111-0000-0000-0000-000000000006"),
                    new Guid("11111111-0000-0000-0000-000000000007"),
                    new Guid("11111111-0000-0000-0000-000000000008"),
                ]);
        }
    }
}
