using CgpRandevuTakip.Application.Common;

namespace CgpRandevuTakip.Application.Categories;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync();
    Task<IReadOnlyList<CategoryDto>> GetActiveAsync();
    Task<OperationResult> CreateAsync(CategoryInput input);
    Task<OperationResult> UpdateAsync(string id, CategoryInput input);
    Task<OperationResult> SetActiveAsync(string id, bool isActive);
    Task<OperationResult> DeleteAsync(string id);
    Task<OperationResult> MoveAsync(string id, int direction);
}
