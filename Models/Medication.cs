namespace HUIOTClinicMS.Models
{
    public class Medication
    {
        public int MedicationID { get; set; }

        public int PrescriptionID { get; set; }
        public Prescription? Prescription { get; set; }

        public string MedicationName { get; set; } = string.Empty;
        public string? MedicationType { get; set; }
        public string? PrescribedDose { get; set; }

        public decimal? Amount { get; set; }
        public int? DispensedQuantity { get; set; }

        public int PharmacistID { get; set; }
        public Staff? Pharmacist { get; set; }

        public string? Remark { get; set; }

        public DateTime DispensedDate { get; set; } = DateTime.Now;
    }
}