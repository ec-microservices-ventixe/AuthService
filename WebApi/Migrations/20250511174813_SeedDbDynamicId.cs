using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace WebApi.Migrations
{
    /// <inheritdoc />
    public partial class SeedDbDynamicId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "9077fe49-496e-4b4f-9d4e-b063b7aa3f07");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "c7251a72-1012-44bc-9f70-9a1de5957302");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "4ea54f3c-62e8-4698-a095-91c1f1474c91", null, "Admin", "ADMIN" },
                    { "c8b3b32c-27e1-4014-a7a2-7f8d4cba4cd8", null, "User", "USER" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "4ea54f3c-62e8-4698-a095-91c1f1474c91");

            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "c8b3b32c-27e1-4014-a7a2-7f8d4cba4cd8");

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "9077fe49-496e-4b4f-9d4e-b063b7aa3f07", null, "Admin", "ADMIN" },
                    { "c7251a72-1012-44bc-9f70-9a1de5957302", null, "User", "USER" }
                });
        }
    }
}
