namespace ClinicDesk.Api.Domain.Enums;

public enum UserRole
{
    Patient = 0,
    Doctor = 1,
    Admin = 2
}

public enum SlotStatus
{
    Available = 0,
    Booked = 1,
    Blocked = 2
}

public enum AppointmentStatus
{
    Confirmed = 0,
    Completed = 1,
    Cancelled = 2,
    LateCancelled = 3,
    NoShow = 4
}

public enum WaitlistStatus
{
    Waiting = 0,
    Promoted = 1,
    Cancelled = 2
}
