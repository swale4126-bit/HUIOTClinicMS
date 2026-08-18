namespace HUIOTClinicMS.Models
{
    public class EmergencyContact
    {
        public int ID { get; set; }

        public int PatientID { get; set; }
        public Patient? Patient { get; set; }

        public string ContactName { get; set; } = string.Empty;
        public string ContactPhoneNumber { get; set; } = string.Empty;
        public string? Relationship { get; set; }
    }
}
