using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HUIOTClinicMS.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounterModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClinicalEncounterID",
                table: "Prescriptions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "MedicalRecords",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "CreatedBy",
                table: "MedicalRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EncounterID",
                table: "MedicalRecords",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "MedicalRecords",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpdatedBy",
                table: "MedicalRecords",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClinicalEncounterID",
                table: "LabRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClinicalEncounters",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PatientID = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClinicalEncounters", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ClinicalEncounters_Patients_PatientID",
                        column: x => x.PatientID,
                        principalTable: "Patients",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReferralRecords",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EncounterID = table.Column<int>(type: "int", nullable: false),
                    DestinationFacility = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReasonForTransfer = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClinicalSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransferredBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralRecords", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ReferralRecords_ClinicalEncounters_EncounterID",
                        column: x => x.EncounterID,
                        principalTable: "ClinicalEncounters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TriageAssessments",
                columns: table => new
                {
                    ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EncounterID = table.Column<int>(type: "int", nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    HeartRate = table.Column<int>(type: "int", nullable: true),
                    BloodPressure = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Weight = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    PriorityLevel = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageAssessments", x => x.ID);
                    table.ForeignKey(
                        name: "FK_TriageAssessments_ClinicalEncounters_EncounterID",
                        column: x => x.EncounterID,
                        principalTable: "ClinicalEncounters",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_ClinicalEncounterID",
                table: "Prescriptions",
                column: "ClinicalEncounterID");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalRecords_EncounterID",
                table: "MedicalRecords",
                column: "EncounterID");

            migrationBuilder.CreateIndex(
                name: "IX_LabRequests_ClinicalEncounterID",
                table: "LabRequests",
                column: "ClinicalEncounterID");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicalEncounters_PatientID",
                table: "ClinicalEncounters",
                column: "PatientID");

            migrationBuilder.CreateIndex(
                name: "IX_ReferralRecords_EncounterID",
                table: "ReferralRecords",
                column: "EncounterID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriageAssessments_EncounterID",
                table: "TriageAssessments",
                column: "EncounterID",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LabRequests_ClinicalEncounters_ClinicalEncounterID",
                table: "LabRequests",
                column: "ClinicalEncounterID",
                principalTable: "ClinicalEncounters",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalRecords_ClinicalEncounters_EncounterID",
                table: "MedicalRecords",
                column: "EncounterID",
                principalTable: "ClinicalEncounters",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_ClinicalEncounters_ClinicalEncounterID",
                table: "Prescriptions",
                column: "ClinicalEncounterID",
                principalTable: "ClinicalEncounters",
                principalColumn: "ID");

            // --- CUSTOM DATA MIGRATION: Preserve historical data ---
            migrationBuilder.Sql(@"
                CREATE TABLE #EncounterMap (MedicalRecordID int, EncounterID int);

                MERGE INTO ClinicalEncounters AS Target
                USING (SELECT ID, PatientID, DateTime FROM MedicalRecords) AS Source
                ON 1 = 0
                WHEN NOT MATCHED THEN
                    INSERT (PatientID, StartTime, Type, Status, CreatedAt)
                    VALUES (Source.PatientID, Source.DateTime, 1, 7, Source.DateTime)
                OUTPUT Source.ID, inserted.ID INTO #EncounterMap(MedicalRecordID, EncounterID);

                UPDATE m
                SET m.EncounterID = map.EncounterID
                FROM MedicalRecords m
                INNER JOIN #EncounterMap map ON m.ID = map.MedicalRecordID;

                UPDATE l
                SET l.ClinicalEncounterID = map.EncounterID
                FROM LabRequests l
                INNER JOIN MedicalRecords m ON l.MedicalRecordID = m.ID
                INNER JOIN #EncounterMap map ON m.ID = map.MedicalRecordID;
                
                UPDATE p
                SET p.ClinicalEncounterID = map.EncounterID
                FROM Prescriptions p
                INNER JOIN LabResults lr ON p.LabResultID = lr.ID
                INNER JOIN #EncounterMap map ON lr.MedicalRecordID = map.MedicalRecordID;

                DROP TABLE #EncounterMap;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabRequests_ClinicalEncounters_ClinicalEncounterID",
                table: "LabRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicalRecords_ClinicalEncounters_EncounterID",
                table: "MedicalRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_ClinicalEncounters_ClinicalEncounterID",
                table: "Prescriptions");

            migrationBuilder.DropTable(
                name: "ReferralRecords");

            migrationBuilder.DropTable(
                name: "TriageAssessments");

            migrationBuilder.DropTable(
                name: "ClinicalEncounters");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_ClinicalEncounterID",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_MedicalRecords_EncounterID",
                table: "MedicalRecords");

            migrationBuilder.DropIndex(
                name: "IX_LabRequests_ClinicalEncounterID",
                table: "LabRequests");

            migrationBuilder.DropColumn(
                name: "ClinicalEncounterID",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "EncounterID",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                table: "MedicalRecords");

            migrationBuilder.DropColumn(
                name: "ClinicalEncounterID",
                table: "LabRequests");
        }
    }
}
