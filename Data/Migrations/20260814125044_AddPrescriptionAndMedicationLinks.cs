using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUIOTClinicMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescriptionAndMedicationLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LabResultID",
                table: "Prescriptions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Prescriptions",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DispensedDate",
                table: "Medications",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<int>(
                name: "PrescriptionID",
                table: "Medications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_LabResultID",
                table: "Prescriptions",
                column: "LabResultID");

            migrationBuilder.CreateIndex(
                name: "IX_Medications_PrescriptionID",
                table: "Medications",
                column: "PrescriptionID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Medications_Prescriptions_PrescriptionID",
                table: "Medications",
                column: "PrescriptionID",
                principalTable: "Prescriptions",
                principalColumn: "PrescriptionID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_LabResults_LabResultID",
                table: "Prescriptions",
                column: "LabResultID",
                principalTable: "LabResults",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Medications_Prescriptions_PrescriptionID",
                table: "Medications");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_LabResults_LabResultID",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_LabResultID",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_Medications_PrescriptionID",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "LabResultID",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "DispensedDate",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "PrescriptionID",
                table: "Medications");
        }
    }
}
