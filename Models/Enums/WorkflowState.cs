namespace HUIOTClinicMS.Models.Enums
{
    public enum WorkflowState
    {
        Registered = 1,
        InTriage = 2,
        WaitingConsultation = 3,
        InConsultation = 4,
        WaitingLab = 5,
        WaitingPharmacy = 6,
        Discharged = 7,
        Transferred = 8,
        InEmergency = 9
    }
}
