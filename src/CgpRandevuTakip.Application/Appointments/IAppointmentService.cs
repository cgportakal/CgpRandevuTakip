using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Enums;

namespace CgpRandevuTakip.Application.Appointments;

public interface IAppointmentService
{
    Task<IReadOnlyList<AppointmentListItemDto>> GetDayAsync(DateTime localDate);
    Task<IReadOnlyList<AppointmentListItemDto>> SearchAsync(
        string? searchText,
        string? categoryId,
        AppointmentStatus? status,
        DateTime? fromLocal,
        DateTime? toLocal);
    Task<AppointmentDetailDto?> GetDetailAsync(string id);
    Task<AppointmentListItemDto?> GetNextAppointmentAsync();
    Task<AppointmentSaveResult> CreateAsync(AppointmentInput input);
    Task<AppointmentSaveResult> UpdateAsync(string id, AppointmentInput input);
    Task<OperationResult> DeleteAsync(string id);
    Task<OperationResult> RestoreAsync(string id);
}
