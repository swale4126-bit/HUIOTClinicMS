using System.ComponentModel.DataAnnotations.Schema;

namespace HUIOTClinicMS.Models
{
    public class LabResult
    {
        public int ID { get; set; }

        public int MedicalRecordID { get; set; }
        public MedicalRecord? MedicalRecord { get; set; }

        public int TypeID { get; set; }
        
        [ForeignKey("TypeID")]
        public TestType? TestType { get; set; }

        public string? Result { get; set; }

        public int ResultPerformedByID { get; set; }
        public Staff? ResultPerformedBy { get; set; }

        public string? Remark { get; set; }
        public string Status { get; set; } = "Completed";
    }
}
