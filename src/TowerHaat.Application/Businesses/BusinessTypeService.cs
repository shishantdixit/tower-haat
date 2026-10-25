using Microsoft.EntityFrameworkCore;
using TowerHaat.Application.Abstractions;
using TowerHaat.Application.Common;
using TowerHaat.Domain.Businesses;

namespace TowerHaat.Application.Businesses;

/// <summary>Business types and categories are data the platform admin manages (A-03), never code.</summary>
public sealed class BusinessTypeService(IAppDbContext db)
{
    public async Task<List<BusinessTypeResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var types = await db.BusinessTypes
            .Where(t => includeInactive || t.IsActive)
            .ToListAsync(cancellationToken);
        var categories = await db.Categories
            .Where(c => includeInactive || c.IsActive)
            .ToListAsync(cancellationToken);

        return types
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name)
            .Select(t => new BusinessTypeResponse(
                t.Id,
                t.Name,
                t.OrderingEnabled,
                t.SortOrder,
                t.IsActive,
                categories
                    .Where(c => c.BusinessTypeId == t.Id)
                    .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                    .Select(c => new CategoryResponse(c.Id, c.Name, c.SortOrder, c.IsActive))
                    .ToList()))
            .ToList();
    }

    public async Task<BusinessTypeResponse> CreateTypeAsync(SaveBusinessTypeRequest request, CancellationToken cancellationToken)
    {
        var name = Validate.Required(request.Name, "Type name", 50);
        await EnsureUniqueTypeAsync(name, exceptId: null, cancellationToken);

        var type = new BusinessType(name, request.OrderingEnabled, request.SortOrder);
        if (!request.IsActive)
        {
            type.Update(name, request.OrderingEnabled, request.SortOrder, isActive: false);
        }

        db.BusinessTypes.Add(type);
        await db.SaveChangesAsync(cancellationToken);
        return new BusinessTypeResponse(type.Id, type.Name, type.OrderingEnabled, type.SortOrder, type.IsActive, []);
    }

    public async Task UpdateTypeAsync(Guid typeId, SaveBusinessTypeRequest request, CancellationToken cancellationToken)
    {
        var type = await db.BusinessTypes.SingleOrDefaultAsync(t => t.Id == typeId, cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "Business type not found.");
        var name = Validate.Required(request.Name, "Type name", 50);
        await EnsureUniqueTypeAsync(name, typeId, cancellationToken);

        type.Update(name, request.OrderingEnabled, request.SortOrder, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CategoryResponse> CreateCategoryAsync(Guid typeId, SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        if (!await db.BusinessTypes.AnyAsync(t => t.Id == typeId, cancellationToken))
        {
            throw new AppException(ErrorKind.NotFound, "Business type not found.");
        }

        var name = Validate.Required(request.Name, "Category name", 50);
        await EnsureUniqueCategoryAsync(typeId, name, exceptId: null, cancellationToken);

        var category = new Category(typeId, name, request.SortOrder);
        if (!request.IsActive)
        {
            category.Update(name, request.SortOrder, isActive: false);
        }

        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return new CategoryResponse(category.Id, category.Name, category.SortOrder, category.IsActive);
    }

    public async Task UpdateCategoryAsync(Guid categoryId, SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            ?? throw new AppException(ErrorKind.NotFound, "Category not found.");
        var name = Validate.Required(request.Name, "Category name", 50);
        await EnsureUniqueCategoryAsync(category.BusinessTypeId, name, categoryId, cancellationToken);

        category.Update(name, request.SortOrder, request.IsActive);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureUniqueTypeAsync(string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = name.ToUpper();
        if (await db.BusinessTypes.AnyAsync(t => t.Name.ToUpper() == key && t.Id != exceptId, cancellationToken))
        {
            throw new AppException(ErrorKind.Conflict, $"A business type named {name} already exists.");
        }
    }

    private async Task EnsureUniqueCategoryAsync(Guid typeId, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = name.ToUpper();
        if (await db.Categories.AnyAsync(
                c => c.BusinessTypeId == typeId && c.Name.ToUpper() == key && c.Id != exceptId, cancellationToken))
        {
            throw new AppException(ErrorKind.Conflict, $"This type already has a category named {name}.");
        }
    }
}
