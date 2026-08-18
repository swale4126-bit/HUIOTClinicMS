using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUIOTClinicMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixLabRequestTestTypeJoinTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabRequestTestType_LabRequests_LabRequestsLabRequestID",
                table: "LabRequestTestType");

            migrationBuilder.DropForeignKey(
                name: "FK_LabRequestTestType_TestTypes_TestTypesID",
                table: "LabRequestTestType");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LabRequestTestType",
                table: "LabRequestTestType");

            migrationBuilder.RenameTable(
                name: "LabRequestTestType",
                newName: "LabRequestTestTypes");

            migrationBuilder.RenameIndex(
                name: "IX_LabRequestTestType_TestTypesID",
                table: "LabRequestTestTypes",
                newName: "IX_LabRequestTestTypes_TestTypesID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LabRequestTestTypes",
                table: "LabRequestTestTypes",
                columns: new[] { "LabRequestsLabRequestID", "TestTypesID" });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabRequestTestTypes_LabRequests_LabRequestsLabRequestID",
                table: "LabRequestTestTypes");

            migrationBuilder.DropForeignKey(
                name: "FK_LabRequestTestTypes_TestTypes_TestTypesID",
                table: "LabRequestTestTypes");

            migrationBuilder.DropPrimaryKey(
                name: "PK_LabRequestTestTypes",
                table: "LabRequestTestTypes");

            migrationBuilder.RenameTable(
                name: "LabRequestTestTypes",
                newName: "LabRequestTestType");

            migrationBuilder.RenameIndex(
                name: "IX_LabRequestTestTypes_TestTypesID",
                table: "LabRequestTestType",
                newName: "IX_LabRequestTestType_TestTypesID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_LabRequestTestType",
                table: "LabRequestTestType",
                columns: new[] { "LabRequestsLabRequestID", "TestTypesID" });

            migrationBuilder.AddForeignKey(
                name: "FK_LabRequestTestType_LabRequests_LabRequestsLabRequestID",
                table: "LabRequestTestType",
                column: "LabRequestsLabRequestID",
                principalTable: "LabRequests",
                principalColumn: "LabRequestID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_LabRequestTestType_TestTypes_TestTypesID",
                table: "LabRequestTestType",
                column: "TestTypesID",
                principalTable: "TestTypes",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
