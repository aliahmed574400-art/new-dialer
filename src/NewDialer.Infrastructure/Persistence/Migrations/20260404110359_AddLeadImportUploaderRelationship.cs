using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NewDialer.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadImportUploaderRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lead_import_batches_users_UploadedByUserId",
                table: "lead_import_batches");

            migrationBuilder.AddForeignKey(
                name: "FK_lead_import_batches_users_UploadedByUserId",
                table: "lead_import_batches",
                column: "UploadedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_lead_import_batches_users_UploadedByUserId",
                table: "lead_import_batches");

            migrationBuilder.AddForeignKey(
                name: "FK_lead_import_batches_users_UploadedByUserId",
                table: "lead_import_batches",
                column: "UploadedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
