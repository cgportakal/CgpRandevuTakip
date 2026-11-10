using CgpRandevuTakip.Domain.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Domain.Entities;

public class Appointment : EntityBase
{
    public string PersonId { get; set; } = string.Empty;
    public string HostUserId { get; set; } = string.Empty;
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Randevu başlangıcı, UTC. Gösterim sistem saat dilimiyle yapılır.</summary>
    public DateTime StartsAtUtc { get; set; }

    public int DurationMinutes { get; set; } = 30;
    public AppointmentMethod Method { get; set; } = AppointmentMethod.FaceToFace;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;

    public string? Description { get; set; }
    public string? StudentFirstName { get; set; }
    public string? StudentLastName { get; set; }

    public bool IsClosed { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? ClosedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public int RowVersion { get; set; } = 1;

    public Person? Person { get; set; }
    public User? HostUser { get; set; }
    public Category? Category { get; set; }
    public AppointmentOutcome? Outcome { get; set; }
    public ICollection<ReminderDispatch> Reminders { get; set; } = new List<ReminderDispatch>();

    public DateTime EndsAtUtc => StartsAtUtc.AddMinutes(DurationMinutes);

    public string StudentFullName
        => $"{StudentFirstName} {StudentLastName}".Trim();

    public bool IsOverdue(DateTime nowUtc)
        => Status == AppointmentStatus.Scheduled && StartsAtUtc < nowUtc;

    public bool IsActiveForReminders
        => !IsDeleted
           && !IsClosed
           && Status is AppointmentStatus.Scheduled or AppointmentStatus.Arrived;

    public void MarkArrived(DateTime nowUtc)
    {
        if (Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Postponed))
            throw new InvalidOperationException("Bu durumdan 'Geldi' durumuna geçilemez.");

        Status = AppointmentStatus.Arrived;
        UpdatedAtUtc = nowUtc;
        RowVersion++;
    }

    public void MarkNoShow(DateTime nowUtc)
    {
        if (Status is not (AppointmentStatus.Scheduled or AppointmentStatus.Arrived))
            throw new InvalidOperationException("Bu durumdan 'Gelmedi' durumuna geçilemez.");

        Status = AppointmentStatus.NoShow;
        UpdatedAtUtc = nowUtc;
        RowVersion++;
    }

    public void Postpone(DateTime nowUtc)
    {
        if (Status is AppointmentStatus.Cancelled or AppointmentStatus.Completed)
            throw new InvalidOperationException("Kapanmış görüşme ertelenemez.");

        Status = AppointmentStatus.Postponed;
        UpdatedAtUtc = nowUtc;
        RowVersion++;
    }

    public void Cancel(DateTime nowUtc, string? closedByUserId = null)
    {
        if (Status == AppointmentStatus.Completed)
            throw new InvalidOperationException("Tamamlanmış görüşme iptal edilemez.");

        Status = AppointmentStatus.Cancelled;
        IsClosed = true;
        ClosedAtUtc = nowUtc;
        ClosedByUserId = closedByUserId;
        UpdatedAtUtc = nowUtc;
        RowVersion++;
    }

    public void Complete(DateTime nowUtc, string? closedByUserId = null)
    {
        if (Status != AppointmentStatus.Arrived)
            throw new InvalidOperationException("Yalnızca 'Geldi' durumundaki görüşme tamamlanabilir.");

        Status = AppointmentStatus.Completed;
        IsClosed = true;
        ClosedAtUtc = nowUtc;
        ClosedByUserId = closedByUserId;
        UpdatedAtUtc = nowUtc;
        RowVersion++;
    }

    /// <summary>Yanlışlıkla kapatılan görüşmeyi yeniden planlanmış duruma döndürür.</summary>
    public void Reopen(DateTime nowUtc)
    {
        if (Status == AppointmentStatus.Completed)
            throw new InvalidOperationException("Tamamlanmış görüşme yeniden açılamaz.");

        if (Status == AppointmentStatus.Scheduled && !IsClosed)
            return;

        Status = AppointmentStatus.Scheduled;
        IsClosed = false;
        ClosedAtUtc = null;
        ClosedByUserId = null;
        UpdatedAtUtc = nowUtc;
        RowVersion++;
    }

    /// <summary>
    /// Görüşme sonucunu (katılım + kapanış) durum makinesine uygular.
    /// Kapanmış bir görüşmenin katılımı değiştirilecekse önce yeniden açılır.
    /// </summary>
    public void ApplyOutcome(
        Attendance attendance,
        bool close,
        DateTime nowUtc,
        string? userId)
    {
        if (!Enum.IsDefined(attendance))
            throw new ArgumentOutOfRangeException(nameof(attendance));

        if (Status == AppointmentStatus.Completed && attendance != Attendance.Arrived)
            throw new InvalidOperationException(
                "Tamamlanmış görüşmenin katılım sonucu değiştirilemez.");

        switch (attendance)
        {
            case Attendance.Arrived:
                if (Status == AppointmentStatus.Completed)
                {
                    if (!close)
                        throw new InvalidOperationException(
                            "Tamamlanmış görüşme kapanışsız bırakılamaz.");
                    return;
                }

                if (Status is AppointmentStatus.NoShow
                    or AppointmentStatus.Postponed
                    or AppointmentStatus.Cancelled)
                {
                    Reopen(nowUtc);
                }

                if (Status == AppointmentStatus.Scheduled)
                    MarkArrived(nowUtc);

                if (close)
                {
                    if (Status == AppointmentStatus.Arrived)
                        Complete(nowUtc, userId);
                }
                else
                {
                    SetClosed(false, nowUtc, null);
                }

                break;

            case Attendance.NoShow:
                if (Status is AppointmentStatus.Postponed or AppointmentStatus.Cancelled)
                    Reopen(nowUtc);

                if (Status == AppointmentStatus.Arrived)
                    MarkNoShow(nowUtc);
                else if (Status == AppointmentStatus.Scheduled)
                    MarkNoShow(nowUtc);

                SetClosed(close, nowUtc, userId);
                break;

            case Attendance.Postponed:
                if (Status == AppointmentStatus.Cancelled)
                    Reopen(nowUtc);

                Postpone(nowUtc);
                SetClosed(false, nowUtc, null);
                break;

            case Attendance.Cancelled:
                Cancel(nowUtc, userId);
                break;
        }
    }

    private void SetClosed(bool closed, DateTime nowUtc, string? userId)
    {
        if (closed)
        {
            if (!IsClosed)
            {
                IsClosed = true;
                ClosedAtUtc = nowUtc;
                ClosedByUserId = userId;
                UpdatedAtUtc = nowUtc;
                RowVersion++;
            }

            return;
        }

        if (!IsClosed)
            return;

        IsClosed = false;
        ClosedAtUtc = null;
        ClosedByUserId = null;
        UpdatedAtUtc = nowUtc;
        RowVersion++;
    }

    public void SoftDelete(DateTime nowUtc)
    {
        IsDeleted = true;
        DeletedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
        RowVersion++;
    }
}
