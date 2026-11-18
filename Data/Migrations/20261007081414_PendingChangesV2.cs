using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUIOTClinicMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class PendingChangesV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_LabResults_LabResultID",
                table: "Prescriptions");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_LabResults_LabResultID",
                table: "Prescriptions",
                column: "LabResultID",
                principalTable: "LabResults",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_LabResults_LabResultID",
                table: "Prescriptions");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_LabResults_LabResultID",
                table: "Prescriptions",
                column: "LabResultID",
                principalTable: "LabResults",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
