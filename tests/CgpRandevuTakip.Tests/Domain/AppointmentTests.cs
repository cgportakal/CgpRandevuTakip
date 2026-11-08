using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using Xunit;

namespace CgpRandevuTakip.Tests.Domain;

public class AppointmentTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 9, 0, 0, DateTimeKind.Utc);

    private static Appointment CreateScheduled()
        => new()
        {
            PersonId = "p1",
            HostUserId = "h1",
            CategoryId = "c1",
            StartsAtUtc = Now.AddHours(1),
            Status = AppointmentStatus.Scheduled
        };

    [Fact]
    public void Complete_FromArrived_SetsCompletedAndClosed()
    {
        var appointment = CreateScheduled();
        appointment.MarkArrived(Now);

        appointment.Complete(Now.AddMinutes(30));

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.True(appointment.IsClosed);
        Assert.NotNull(appointment.ClosedAtUtc);
    }

    [Fact]
    public void Complete_FromScheduled_Throws()
    {
        var appointment = CreateScheduled();

        Assert.Throws<InvalidOperationException>(() => appointment.Complete(Now));
    }

    [Fact]
    public void Cancel_SetsCancelledAndClosed()
    {
        var appointment = CreateScheduled();

        appointment.Cancel(Now);

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.True(appointment.IsClosed);
    }

    [Fact]
    public void IsOverdue_OnlyForScheduledPastAppointments()
    {
        var appointment = CreateScheduled();
        Assert.False(appointment.IsOverdue(Now));

        appointment.StartsAtUtc = Now.AddMinutes(-1);
        Assert.True(appointment.IsOverdue(Now));

        appointment.Status = AppointmentStatus.Arrived;
        Assert.False(appointment.IsOverdue(Now));
    }

    [Fact]
    public void SoftDelete_MarksDeleted()
    {
        var appointment = CreateScheduled();

        appointment.SoftDelete(Now);

        Assert.True(appointment.IsDeleted);
        Assert.NotNull(appointment.DeletedAtUtc);
    }

    [Fact]
    public void ApplyOutcome_ArrivedAndClosed_Completes()
    {
        var appointment = CreateScheduled();

        appointment.ApplyOutcome(Attendance.Arrived, true, Now, "u1");

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.True(appointment.IsClosed);
        Assert.Equal("u1", appointment.ClosedByUserId);
    }

    [Fact]
    public void ApplyOutcome_ArrivedWithoutClose_StaysOpen()
    {
        var appointment = CreateScheduled();

        appointment.ApplyOutcome(Attendance.Arrived, false, Now, "u1");

        Assert.Equal(AppointmentStatus.Arrived, appointment.Status);
        Assert.False(appointment.IsClosed);
        Assert.Null(appointment.ClosedByUserId);
    }

    [Fact]
    public void ApplyOutcome_NoShowClosed_MarksNoShowAndClosed()
    {
        var appointment = CreateScheduled();

        appointment.ApplyOutcome(Attendance.NoShow, true, Now, "u1");

        Assert.Equal(AppointmentStatus.NoShow, appointment.Status);
        Assert.True(appointment.IsClosed);
        Assert.Equal("u1", appointment.ClosedByUserId);
    }

    [Fact]
    public void ApplyOutcome_Postponed_KeepsOpenAndReopensCancelled()
    {
        var appointment = CreateScheduled();
        appointment.Cancel(Now, "u1");

        appointment.ApplyOutcome(Attendance.Postponed, false, Now, "u2");

        Assert.Equal(AppointmentStatus.Postponed, appointment.Status);
        Assert.False(appointment.IsClosed);
        Assert.Null(appointment.ClosedByUserId);
    }

    [Fact]
    public void ApplyOutcome_Cancelled_Closes()
    {
        var appointment = CreateScheduled();

        appointment.ApplyOutcome(Attendance.Cancelled, true, Now, "u1");

        Assert.Equal(AppointmentStatus.Cancelled, appointment.Status);
        Assert.True(appointment.IsClosed);
    }

    [Fact]
    public void ApplyOutcome_ArrivedOnCancelled_ReopensThenCompletes()
    {
        var appointment = CreateScheduled();
        appointment.Cancel(Now, "u1");

        appointment.ApplyOutcome(Attendance.Arrived, true, Now.AddMinutes(5), "u2");

        Assert.Equal(AppointmentStatus.Completed, appointment.Status);
        Assert.True(appointment.IsClosed);
        Assert.Equal("u2", appointment.ClosedByUserId);
    }

    [Fact]
    public void ApplyOutcome_CompletedCannotBecomeNoShow()
    {
        var appointment = CreateScheduled();
        appointment.ApplyOutcome(Attendance.Arrived, true, Now, "u1");

        Assert.Throws<InvalidOperationException>(
            () => appointment.ApplyOutcome(Attendance.NoShow, true, Now, "u2"));
    }

    [Fact]
    public void Reopen_FromCompleted_Throws()
    {
        var appointment = CreateScheduled();
        appointment.ApplyOutcome(Attendance.Arrived, true, Now, "u1");

        Assert.Throws<InvalidOperationException>(() => appointment.Reopen(Now));
    }
}
