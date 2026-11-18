namespace HUIOTClinicMS.Models.Enums
{
    public enum LabRequestStatus
    {
        Requested = 1,
        SampleCollected = 2,
        Processing = 3,
        Completed = 4,
        Cancelled = 5,
        Pending = 6
    }

    public static class LabRequestStatuses
    {
        public const string Pending = "Pending";
        public const string InProgress = "In Progress";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }
}
