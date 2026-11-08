namespace CgpRandevuTakip.Domain.Enums;

public enum UserRole
{
    Admin = 0,
    Secretary = 1,
    Host = 2
}

public enum AppointmentMethod
{
    Phone = 0,
    FaceToFace = 1,
    WhatsApp = 2,
    Online = 3
}

public enum AppointmentStatus
{
    Scheduled = 0,
    Arrived = 1,
    NoShow = 2,
    Postponed = 3,
    Cancelled = 4,
    Completed = 5
}

public enum Attendance
{
    Arrived = 0,
    NoShow = 1,
    Postponed = 2,
    Cancelled = 3
}

public enum ReminderState
{
    Pending = 0,
    Sent = 1,
    DismissedOnly = 2,
    InformedHost = 3,
    Snoozed = 4,
    Cancelled = 5,
    Missed = 6,
    Unanswered = 7
}

public enum ReminderAction
{
    None = 0,
    DismissOnly = 1,
    InformedHost = 2,
    UnreachableSnooze = 3,
    VisitorCancelled = 4
}

public enum ReminderChannel
{
    Windows = 0,
    Push = 1
}

public enum ShareMethod
{
    DailyList = 0,
    AppointmentDetail = 1
}

public enum ShareResult
{
    Prepared = 0,
    Opened = 1,
    Failed = 2,
    Cancelled = 3
}

public enum BackupProvider
{
    Local = 0,
    GoogleDrive = 1
}

public enum AuditAction
{
    LoginSucceeded = 0,
    LoginFailed = 1,
    Logout = 2,
    AppointmentCreated = 3,
    AppointmentUpdated = 4,
    AppointmentDeleted = 5,
    AppointmentRestored = 6,
    OutcomeSaved = 7,
    ReminderActionTaken = 8,
    CategoryCreated = 9,
    CategoryUpdated = 10,
    CategoryDeactivated = 11,
    SettingsChanged = 12,
    BackupCreated = 13,
    BackupRestored = 14,
    SharePrepared = 15,
    FirstRunCompleted = 16,
    CategoryDeleted = 17,
    ThemeChanged = 18,
    PasswordChanged = 19,
    UserCreated = 20,
    UserUpdated = 21,
    UserDeactivated = 22,
    SessionLocked = 23,
    SessionUnlocked = 24,
    AppointmentPurged = 25
}
