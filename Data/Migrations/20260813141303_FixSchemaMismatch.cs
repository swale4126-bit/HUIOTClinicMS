using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUIOTClinicMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixSchemaMismatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropForeignKey(
            //     name: "FK_LabResults_TestTypes_TestTypeID",
            //     table: "LabResults");

            // migrationBuilder.DropIndex(
            //     name: "IX_LabResults_TestTypeID",
            //     table: "LabResults");

            // migrationBuilder.DropColumn(
            //     name: "TestTypeID",
            //     table: "LabResults");


            migrationBuilder.CreateIndex(
                name: "IX_LabResults_TypeID",
                table: "LabResults",
                column: "TypeID");

            migrationBuilder.AddForeignKey(
                name: "FK_LabResults_TestTypes_TypeID",
                table: "LabResults",
                column: "TypeID",
                principalTable: "TestTypes",
                principalColumn: "ID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabResults_TestTypes_TypeID",
                table: "LabResults");

            migrationBuilder.DropIndex(
                name: "IX_LabResults_TypeID",
                table: "LabResults");

            migrationBuilder.AddColumn<int>(
                name: "TestTypeID",
                table: "LabResults",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_TestTypeID",
                table: "LabResults",
                column: "TestTypeID");

            migrationBuilder.AddForeignKey(
                name: "FK_LabResults_TestTypes_TestTypeID",
                table: "LabResults",
                column: "TestTypeID",
                principalTable: "TestTypes",
                principalColumn: "ID");
        }
    }
}
