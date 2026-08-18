using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUIOTClinicMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLabRequestTestTypeLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LabRequestTestType",
                columns: table => new
                {
                    LabRequestsLabRequestID = table.Column<int>(type: "int", nullable: false),
                    TestTypesID = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabRequestTestType", x => new { x.LabRequestsLabRequestID, x.TestTypesID });
                    table.ForeignKey(
                        name: "FK_LabRequestTestType_LabRequests_LabRequestsLabRequestID",
                        column: x => x.LabRequestsLabRequestID,
                        principalTable: "LabRequests",
                        principalColumn: "LabRequestID",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabRequestTestType_TestTypes_TestTypesID",
                        column: x => x.TestTypesID,
                        principalTable: "TestTypes",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabRequestTestType_TestTypesID",
                table: "LabRequestTestType",
                column: "TestTypesID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LabRequestTestType");
        }
    }
}
