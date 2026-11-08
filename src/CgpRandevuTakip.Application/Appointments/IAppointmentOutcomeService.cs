using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Appointments;

public interface IAppointmentOutcomeService
{
    Task<AppointmentOutcomeDto?> GetAsync(string appointmentId);

    Task<OperationResult> SaveAsync(string appointmentId, AppointmentOutcomeInput input);
}
