using CgpRandevuTakip.Application.Abstractions;
using CgpRandevuTakip.Application.Common;
using CgpRandevuTakip.Domain.Entities;
using CgpRandevuTakip.Domain.Enums;
using CgpRandevuTakip.Domain.ValueObjects;

namespace CgpRandevuTakip.Application.Recipients;

public interface IRecipientService
{
    Task<IReadOnlyList<RecipientDto>> GetAllAsync();
    Task<IReadOnlyList<RecipientDto>> GetActiveAsync();
    Task<OperationResult> CreateAsync(RecipientInput input);
    Task<OperationResult> UpdateAsync(string id, RecipientInput input);
}

public sealed class RecipientService : IRecipientService
{
    private readonly IRecipientRepository _repository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditLogger _audit;

    public RecipientService(
        IRecipientRepository repository,
        ICurrentUser currentUser,
        IAuditLogger audit)
    {
        _repository = repository;
        _currentUser = currentUser;
        _audit = audit;
    }

    public async Task<IReadOnlyList<RecipientDto>> GetAllAsync()
    {
        if (!CanManage())
            return Array.Empty<RecipientDto>();

        var rows = await _repository.GetAllAsync();
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyList<RecipientDto>> GetActiveAsync()
    {
        if (!CanManage())
            return Array.Empty<RecipientDto>();

        var rows = await _repository.GetAllAsync();
        return rows.Where(x => x.IsActive).Select(ToDto).ToList();
    }

    public async Task<OperationResult> CreateAsync(RecipientInput input)
    {
        if (!CanManage())
            return OperationResult.Fail("Alıcı ekleme yetkisi yönetici veya sekreterdedir.");

        var validation = Validate(input, out var name, out var phone);
        if (validation is not null)
            return OperationResult.Fail(validation);

        var recipient = new Recipient
        {
            Name = name,
            PhoneE164 = phone,
            Tag = NormalizeTag(input.Tag),
            IsActive = input.IsActive
        };

        await _repository.AddAsync(recipient);
        await _audit.LogAsync(
            AuditAction.SettingsChanged,
            _currentUser.UserId,
            nameof(Recipient),
            recipient.Id);

        return OperationResult.Ok();
    }

    public async Task<OperationResult> UpdateAsync(string id, RecipientInput input)
    {
        if (!CanManage())
            return OperationResult.Fail("Alıcı düzenleme yetkisi yönetici veya sekreterdedir.");

        var recipient = await _repository.GetByIdAsync(id);
        if (recipient is null)
            return OperationResult.Fail("Alıcı bulunamadı.");

        var validation = Validate(input, out var name, out var phone);
        if (validation is not null)
            return OperationResult.Fail(validation);

        recipient.Name = name;
        recipient.PhoneE164 = phone;
        recipient.Tag = NormalizeTag(input.Tag);
        recipient.IsActive = input.IsActive;
        await _repository.SaveAsync(recipient);

        await _audit.LogAsync(
            AuditAction.SettingsChanged,
            _currentUser.UserId,
            nameof(Recipient),
            recipient.Id);

        return OperationResult.Ok();
    }

    private bool CanManage()
        => _currentUser.IsInRole(UserRole.Admin, UserRole.Secretary);

    private static string? Validate(
        RecipientInput input,
        out string name,
        out string phone)
    {
        name = input.Name?.Trim() ?? string.Empty;
        phone = string.Empty;

        if (name.Length < 2)
            return "Alıcı adı en az 2 karakter olmalıdır.";
        if (name.Length > 100)
            return "Alıcı adı en fazla 100 karakter olabilir.";

        if (!PhoneNumber.TryNormalize(input.Phone, out phone))
            return "Telefon numarası geçerli değil. Örnek: 0532 123 45 67.";

        return null;
    }

    private static string? NormalizeTag(string? tag)
        => string.IsNullOrWhiteSpace(tag) ? null : tag.Trim();

    private static RecipientDto ToDto(Recipient recipient)
        => new(
            recipient.Id,
            recipient.Name,
            recipient.PhoneE164,
            recipient.Tag,
            recipient.IsActive);
}
