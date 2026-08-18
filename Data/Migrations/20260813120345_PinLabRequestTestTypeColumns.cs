using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUIOTClinicMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class PinLabRequestTestTypeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabRequestTestTypes_LabRequests_LabRequestsLabRequestID",
                table: "LabRequestTestTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_LabRequestTestTypes_TestTypes_TestTypesID",
                table: "LabRequestTestTypes");

            migrationBuilder.RenameColumn(
                name: "TestTypesID",
                table: "LabRequestTestTypes",
                newName: "TestTypeID");

            migrationBuilder.RenameColumn(
                name: "LabRequestsLabRequestID",
                table: "LabRequestTestTypes",
                newName: "LabRequestID");

            migrationBuilder.RenameIndex(
                name: "IX_LabRequestTestTypes_TestTypesID",
                table: "LabRequestTestTypes",
                newName: "IX_LabRequestTestTypes_TestTypeID");

            migrationBuilder.AddForeignKey(
                name: "FK_LabRequestTestTypes_LabRequests_LabRequestID",
                table: "LabRequestTestTypes",
                column: "LabRequestID",
                principalTable: "LabRequests",
                principalColumn: "LabRequestID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LabRequestTestTypes_TestTypes_TestTypeID",
                table: "LabRequestTestTypes",
                column: "TestTypeID",
                principalTable: "TestTypes",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabRequestTestTypes_LabRequests_LabRequestID",
                table: "LabRequestTestTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_LabRequestTestTypes_TestTypes_TestTypeID",
                table: "LabRequestTestTypes");

            migrationBuilder.RenameColumn(
                name: "TestTypeID",
                table: "LabRequestTestTypes",
                newName: "TestTypesID");

            migrationBuilder.RenameColumn(
                name: "LabRequestID",
                table: "LabRequestTestTypes",
                newName: "LabRequestsLabRequestID");

            migrationBuilder.RenameIndex(
                name: "IX_LabRequestTestTypes_TestTypeID",
                table: "LabRequestTestTypes",
                newName: "IX_LabRequestTestTypes_TestTypesID");

            migrationBuilder.AddForeignKey(
                name: "FK_LabRequestTestTypes_LabRequests_LabRequestsLabRequestID",
                table: "LabRequestTestTypes",
                column: "LabRequestsLabRequestID",
                principalTable: "LabRequests",
                principalColumn: "LabRequestID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LabRequestTestTypes_TestTypes_TestTypesID",
                table: "LabRequestTestTypes",
                column: "TestTypesID",
                principalTable: "TestTypes",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
