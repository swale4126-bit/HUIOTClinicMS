using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUIOTClinicMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDynamicRbacAndTaskAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Staffs",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "AssignedToStaffID",
                table: "Prescriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedToStaffID",
                table: "LabRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AssignedToStaffID",
                table: "ClinicalEncounters",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TaskAssignmentHistories",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TaskType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TaskID = table.Column<int>(type: "int", nullable: false),
                    AssignedFromStaffID = table.Column<int>(type: "int", nullable: true),
                    AssignedToStaffID = table.Column<int>(type: "int", nullable: false),
                    AssignedByID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAssignmentHistories", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TaskAssignmentHistories_AspNetUsers_AssignedByID",
                        column: x => x.AssignedByID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskAssignmentHistories_Staffs_AssignedFromStaffID",
                        column: x => x.AssignedFromStaffID,
                        principalTable: "Staffs",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskAssignmentHistories_Staffs_AssignedToStaffID",
                        column: x => x.AssignedToStaffID,
                        principalTable: "Staffs",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_AssignedToStaffID",
                table: "Prescriptions",
                column: "AssignedToStaffID");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_AssignedToStaffID",
                table: "LabRequests",
                column: "AssignedToStaffID");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEncounters_AssignedToStaffID",
                table: "ClinicalEncounters",
                column: "AssignedToStaffID");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentHistories_AssignedByID",
                table: "TaskAssignmentHistories",
                column: "AssignedByID");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentHistories_AssignedFromStaffID",
                table: "TaskAssignmentHistories",
                column: "AssignedFromStaffID");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAssignmentHistories_AssignedToStaffID",
                table: "TaskAssignmentHistories",
                column: "AssignedToStaffID");

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicalEncounters_Staffs_AssignedToStaffID",
                table: "ClinicalEncounters",
                column: "AssignedToStaffID",
                principalTable: "Staffs",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabRequests_Staffs_AssignedToStaffID",
                table: "LabRequests",
                column: "AssignedToStaffID",
                principalTable: "Staffs",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_Staffs_AssignedToStaffID",
                table: "Prescriptions",
                column: "AssignedToStaffID",
                principalTable: "Staffs",
                principalColumn: "ID",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClinicalEncounters_Staffs_AssignedToStaffID",
                table: "ClinicalEncounters");

            migrationBuilder.DropForeignKey(
                name: "FK_LabRequests_Staffs_AssignedToStaffID",
                table: "LabRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_Staffs_AssignedToStaffID",
                table: "Prescriptions");

            migrationBuilder.DropTable(
                name: "TaskAssignmentHistories");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_AssignedToStaffID",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_LabRequests_AssignedToStaffID",
                table: "LabRequests");

            migrationBuilder.DropIndex(
                name: "IX_ClinicalEncounters_AssignedToStaffID",
                table: "ClinicalEncounters");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Staffs");

            migrationBuilder.DropColumn(
                name: "AssignedToStaffID",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "AssignedToStaffID",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "AssignedToStaffID",
                table: "ClinicalEncounters");
        }
    }
}
