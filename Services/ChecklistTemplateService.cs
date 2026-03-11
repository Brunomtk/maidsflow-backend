using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Core.DTO.ChecklistTemplate;
using Core.Exceptions;
using Core.Models;
using Infrastructure.Repositories;
using Services.Security;

namespace Services
{
    public interface IChecklistTemplateService
    {
        Task<List<ChecklistTemplateDTO>> GetVisibleTemplatesAsync();
        Task<ChecklistTemplateDTO?> GetByIdAsync(int id);
        Task SeedDefaultAirbnbTemplatesAsync();
        Task<ChecklistTemplateDTO> CreateAsync(CreateChecklistTemplateDTO dto);
        Task<ChecklistTemplateDTO?> UpdateAsync(UpdateChecklistTemplateDTO dto);
        Task<bool> DeleteAsync(int id);
    }

    public class ChecklistTemplateService : IChecklistTemplateService
    {
        private readonly IUnitOfWork _uow;
        private readonly ICurrentUser _currentUser;
        private readonly IScopeGuard _scope;

        public ChecklistTemplateService(IUnitOfWork uow, ICurrentUser currentUser, IScopeGuard scope)
        {
            _uow = uow;
            _currentUser = currentUser;
            _scope = scope;
        }

        public async Task<List<ChecklistTemplateDTO>> GetVisibleTemplatesAsync()
        {
            var companyId = await GetScopedCompanyIdForTemplatesAsync();
            await SeedDefaultAirbnbTemplatesAsync();
            var templates = await _uow.ChecklistTemplates.GetVisibleTemplatesAsync(companyId == 0 ? null : companyId);
            return templates.Select(Map).ToList();
        }

        public async Task<ChecklistTemplateDTO?> GetByIdAsync(int id)
        {
            var template = await _uow.ChecklistTemplates.GetByIdWithItemsAsync(id);
            if (template == null) return null;

            var companyId = await GetScopedCompanyIdForTemplatesAsync();
            if (template.CompanyId.HasValue && !_currentUser.IsAdmin && template.CompanyId != companyId)
                throw new ForbiddenException("You do not have permission to access this checklist model.");

            return Map(template);
        }

        public async Task SeedDefaultAirbnbTemplatesAsync()
        {
            var defaults = DefaultAirbnbTemplates();
            var existingVisible = await _uow.ChecklistTemplates.GetVisibleTemplatesAsync(null);
            var existingSystemTemplates = existingVisible.Where(x => x.IsSystemTemplate).ToList();

            foreach (var defaultTemplate in defaults)
            {
                var match = existingSystemTemplates.FirstOrDefault(x => IsSameSystemTemplate(x, defaultTemplate));

                if (match == null)
                {
                    await _uow.ChecklistTemplates.Add(defaultTemplate);
                    continue;
                }

                match.Name = defaultTemplate.Name;
                match.Description = defaultTemplate.Description;
                match.TemplateType = defaultTemplate.TemplateType;
                match.IsActive = true;
                match.IsSystemTemplate = true;
                match.CompanyId = null;

                foreach (var existingItem in match.Items.ToList())
                    _uow.ChecklistTemplateItems.Delete(existingItem);

                match.Items = defaultTemplate.Items
                    .OrderBy(i => i.SortOrder)
                    .Select(i => new ChecklistTemplateItem
                    {
                        ChecklistTemplateId = match.Id,
                        SpaceName = i.SpaceName,
                        Title = i.Title,
                        Description = i.Description,
                        IsRequired = i.IsRequired,
                        RequiresPhoto = i.RequiresPhoto,
                        SortOrder = i.SortOrder
                    }).ToList();

                foreach (var item in match.Items)
                    await _uow.ChecklistTemplateItems.Add(item);

                _uow.ChecklistTemplates.Update(match);
            }

            await _uow.SaveAsync();
        }

        public async Task<ChecklistTemplateDTO> CreateAsync(CreateChecklistTemplateDTO dto)
        {
            var companyId = await GetCompanyIdForWriteAsync();
            if (await _uow.ChecklistTemplates.ExistsByNameAsync(companyId, dto.Name))
                throw new ConflictException("A checklist model with this name already exists.");

            var entity = new ChecklistTemplate
            {
                CompanyId = companyId,
                Name = dto.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
                TemplateType = string.IsNullOrWhiteSpace(dto.TemplateType) ? "airbnb" : dto.TemplateType.Trim().ToLowerInvariant(),
                IsActive = dto.IsActive,
                IsSystemTemplate = false,
                Items = dto.Items.OrderBy(i => i.SortOrder).Select((item, index) => new ChecklistTemplateItem
                {
                    SpaceName = item.SpaceName.Trim(),
                    Title = item.Title.Trim(),
                    Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                    IsRequired = item.IsRequired,
                    RequiresPhoto = item.RequiresPhoto,
                    SortOrder = item.SortOrder == 0 ? index + 1 : item.SortOrder
                }).ToList()
            };

            await _uow.ChecklistTemplates.Add(entity);
            await _uow.SaveAsync();
            return Map(entity);
        }

        public async Task<ChecklistTemplateDTO?> UpdateAsync(UpdateChecklistTemplateDTO dto)
        {
            var entity = await _uow.ChecklistTemplates.GetByIdWithItemsAsync(dto.Id);
            if (entity == null) return null;
            if (entity.IsSystemTemplate)
                throw new ConflictException("System models cannot be edited. Create a custom copy instead.");

            var companyId = await GetCompanyIdForWriteAsync();
            if (entity.CompanyId != companyId && !_currentUser.IsAdmin)
                throw new ForbiddenException("You do not have permission to edit this checklist model.");

            if (await _uow.ChecklistTemplates.ExistsByNameAsync(companyId, dto.Name, dto.Id))
                throw new ConflictException("A checklist model with this name already exists.");

            entity.Name = dto.Name.Trim();
            entity.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
            entity.TemplateType = string.IsNullOrWhiteSpace(dto.TemplateType) ? "airbnb" : dto.TemplateType.Trim().ToLowerInvariant();
            entity.IsActive = dto.IsActive;

            foreach (var existing in entity.Items.ToList())
                _uow.ChecklistTemplateItems.Delete(existing);

            entity.Items = dto.Items.OrderBy(i => i.SortOrder).Select((item, index) => new ChecklistTemplateItem
            {
                ChecklistTemplateId = entity.Id,
                SpaceName = item.SpaceName.Trim(),
                Title = item.Title.Trim(),
                Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim(),
                IsRequired = item.IsRequired,
                RequiresPhoto = item.RequiresPhoto,
                SortOrder = item.SortOrder == 0 ? index + 1 : item.SortOrder
            }).ToList();

            foreach (var item in entity.Items)
                await _uow.ChecklistTemplateItems.Add(item);

            _uow.ChecklistTemplates.Update(entity);
            await _uow.SaveAsync();
            return Map(entity);
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var entity = await _uow.ChecklistTemplates.GetByIdWithItemsAsync(id);
            if (entity == null) return false;
            if (entity.IsSystemTemplate)
                throw new ConflictException("System models cannot be removed.");

            var companyId = await GetCompanyIdForWriteAsync();
            if (entity.CompanyId != companyId && !_currentUser.IsAdmin)
                throw new ForbiddenException("You do not have permission to remove this checklist model.");

            _uow.ChecklistTemplates.Delete(entity);
            return await _uow.SaveAsync() > 0;
        }

        private async Task<int> GetScopedCompanyIdForTemplatesAsync()
        {
            if (_currentUser.IsAdmin) return 0;
            var companyId = await _scope.GetScopedCompanyIdAsync();
            if (!companyId.HasValue) throw new ForbiddenException("Invalid company scope.");
            return companyId.Value;
        }

        private async Task<int> GetCompanyIdForWriteAsync()
        {
            var companyId = await _scope.GetScopedCompanyIdAsync();
            if (!companyId.HasValue) throw new ForbiddenException("Invalid company scope.");
            return companyId.Value;
        }

        private static ChecklistTemplateDTO Map(ChecklistTemplate x) => new()
        {
            Id = x.Id,
            CompanyId = x.CompanyId,
            Name = x.Name,
            Description = x.Description,
            TemplateType = x.TemplateType,
            IsSystemTemplate = x.IsSystemTemplate,
            IsActive = x.IsActive,
            ItemsCount = x.Items.Count,
            Items = x.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Id).Select(i => new ChecklistTemplateItemDTO
            {
                Id = i.Id,
                SpaceName = i.SpaceName,
                Title = i.Title,
                Description = i.Description,
                IsRequired = i.IsRequired,
                RequiresPhoto = i.RequiresPhoto,
                SortOrder = i.SortOrder
            }).ToList()
        };

        private static bool IsSameSystemTemplate(ChecklistTemplate existing, ChecklistTemplate target)
        {
            if (!existing.IsSystemTemplate) return false;

            var normalizedExistingName = Normalize(existing.Name);
            var normalizedTargetName = Normalize(target.Name);
            if (normalizedExistingName == normalizedTargetName) return true;

            return normalizedExistingName switch
            {
                "airbnb turnover padrao" => normalizedTargetName == "airbnb standard turnover",
                "airbnb check-out express" => normalizedTargetName == "airbnb express checkout",
                _ => false
            };
        }

        private static string Normalize(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant();

        private static List<ChecklistTemplate> DefaultAirbnbTemplates()
        {
            return new List<ChecklistTemplate>
            {
                new ChecklistTemplate
                {
                    CompanyId = null,
                    Name = "Airbnb Standard Turnover",
                    Description = "Complete turnover checklist for cleaning, restocking, and final readiness inspection between guests.",
                    TemplateType = "airbnb",
                    IsSystemTemplate = true,
                    IsActive = true,
                    Items = new List<ChecklistTemplateItem>
                    {
                        new() { SpaceName = "Entry", Title = "Check lock, handle, and access condition", IsRequired = true, SortOrder = 1 },
                        new() { SpaceName = "Entry", Title = "Clean the door, doormat, and light switches", SortOrder = 2 },
                        new() { SpaceName = "Living Room", Title = "Vacuum or sweep the floor and clean baseboards", IsRequired = true, SortOrder = 3 },
                        new() { SpaceName = "Living Room", Title = "Clean the sofa, cushions, and throw blanket", SortOrder = 4 },
                        new() { SpaceName = "Living Room", Title = "Wipe the table, TV, remotes, and exposed surfaces", SortOrder = 5 },
                        new() { SpaceName = "Bedroom", Title = "Replace all bed linens and make the bed neatly", IsRequired = true, RequiresPhoto = true, SortOrder = 6 },
                        new() { SpaceName = "Bedroom", Title = "Inspect the mattress, pillows, and headboard", SortOrder = 7 },
                        new() { SpaceName = "Bedroom", Title = "Organize the wardrobe, hangers, and nightstands", SortOrder = 8 },
                        new() { SpaceName = "Bathroom", Title = "Sanitize the toilet, sink, mirror, and shower area", IsRequired = true, RequiresPhoto = true, SortOrder = 9 },
                        new() { SpaceName = "Bathroom", Title = "Restock toilet paper, soap, and basic amenities", SortOrder = 10 },
                        new() { SpaceName = "Kitchen", Title = "Clean the countertop, sink, stove, and microwave", IsRequired = true, SortOrder = 11 },
                        new() { SpaceName = "Kitchen", Title = "Check utensils, dishes, and small appliances", SortOrder = 12 },
                        new() { SpaceName = "Kitchen", Title = "Remove trash and replace garbage bags", SortOrder = 13 },
                        new() { SpaceName = "Laundry Area", Title = "Check the washer area, sink, and cleaning products", SortOrder = 14 },
                        new() { SpaceName = "Final Review", Title = "Check smell, lighting, and room temperature", IsRequired = true, SortOrder = 15 },
                        new() { SpaceName = "Final Review", Title = "Take final ready-unit photos", IsRequired = true, RequiresPhoto = true, SortOrder = 16 }
                    }
                },
                new ChecklistTemplate
                {
                    CompanyId = null,
                    Name = "Airbnb Express Checkout",
                    Description = "Lean checklist for fast turnover on high-rotation short-stay properties.",
                    TemplateType = "airbnb",
                    IsSystemTemplate = true,
                    IsActive = true,
                    Items = new List<ChecklistTemplateItem>
                    {
                        new() { SpaceName = "General", Title = "Remove trash and forgotten guest items", IsRequired = true, SortOrder = 1 },
                        new() { SpaceName = "General", Title = "Ventilate the unit and check for odors", SortOrder = 2 },
                        new() { SpaceName = "Bedroom", Title = "Replace linens and organize pillows", IsRequired = true, RequiresPhoto = true, SortOrder = 3 },
                        new() { SpaceName = "Bathroom", Title = "Sanitize fixtures and dry visible surfaces", IsRequired = true, SortOrder = 4 },
                        new() { SpaceName = "Kitchen", Title = "Check the sink, dishes, and countertop", IsRequired = true, SortOrder = 5 },
                        new() { SpaceName = "Final Review", Title = "Take a final photo and release the unit", IsRequired = true, RequiresPhoto = true, SortOrder = 6 }
                    }
                }
            };
        }
    }
}
