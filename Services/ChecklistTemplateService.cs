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
            var templates = await _uow.ChecklistTemplates.GetVisibleTemplatesAsync(companyId);
            return templates.Select(Map).ToList();
        }

        public async Task<ChecklistTemplateDTO?> GetByIdAsync(int id)
        {
            var template = await _uow.ChecklistTemplates.GetByIdWithItemsAsync(id);
            if (template == null) return null;
            var companyId = await GetScopedCompanyIdForTemplatesAsync();
            if (template.CompanyId.HasValue && !_currentUser.IsAdmin && template.CompanyId != companyId)
                throw new ForbiddenException("Você não tem permissão para acessar este modelo de checklist.");
            return Map(template);
        }

        public async Task SeedDefaultAirbnbTemplatesAsync()
        {
            if ((await _uow.ChecklistTemplates.GetVisibleTemplatesAsync(null)).Any(x => x.IsSystemTemplate))
                return;

            var defaults = DefaultAirbnbTemplates();
            foreach (var template in defaults)
                await _uow.ChecklistTemplates.Add(template);

            await _uow.SaveAsync();
        }

        public async Task<ChecklistTemplateDTO> CreateAsync(CreateChecklistTemplateDTO dto)
        {
            var companyId = await GetCompanyIdForWriteAsync();
            if (await _uow.ChecklistTemplates.ExistsByNameAsync(companyId, dto.Name))
                throw new ConflictException("Já existe um modelo de checklist com esse nome.");

            var entity = new ChecklistTemplate
            {
                CompanyId = companyId,
                Name = dto.Name.Trim(),
                Description = dto.Description,
                TemplateType = string.IsNullOrWhiteSpace(dto.TemplateType) ? "airbnb" : dto.TemplateType.Trim().ToLowerInvariant(),
                IsActive = dto.IsActive,
                IsSystemTemplate = false,
                Items = dto.Items.OrderBy(i => i.SortOrder).Select((item, index) => new ChecklistTemplateItem
                {
                    SpaceName = item.SpaceName.Trim(),
                    Title = item.Title.Trim(),
                    Description = item.Description,
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
                throw new ConflictException("Modelos padrão do sistema não podem ser editados. Crie uma cópia customizada.");

            var companyId = await GetCompanyIdForWriteAsync();
            if (entity.CompanyId != companyId && !_currentUser.IsAdmin)
                throw new ForbiddenException("Você não tem permissão para editar este modelo.");

            if (await _uow.ChecklistTemplates.ExistsByNameAsync(companyId, dto.Name, dto.Id))
                throw new ConflictException("Já existe um modelo de checklist com esse nome.");

            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description;
            entity.TemplateType = string.IsNullOrWhiteSpace(dto.TemplateType) ? "airbnb" : dto.TemplateType.Trim().ToLowerInvariant();
            entity.IsActive = dto.IsActive;

            foreach (var existing in entity.Items.ToList())
                _uow.ChecklistTemplateItems.Delete(existing);

            entity.Items = dto.Items.OrderBy(i => i.SortOrder).Select((item, index) => new ChecklistTemplateItem
            {
                ChecklistTemplateId = entity.Id,
                SpaceName = item.SpaceName.Trim(),
                Title = item.Title.Trim(),
                Description = item.Description,
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
                throw new ConflictException("Modelos padrão do sistema não podem ser removidos.");

            var companyId = await GetCompanyIdForWriteAsync();
            if (entity.CompanyId != companyId && !_currentUser.IsAdmin)
                throw new ForbiddenException("Você não tem permissão para remover este modelo.");

            _uow.ChecklistTemplates.Delete(entity);
            return await _uow.SaveAsync() > 0;
        }

        private async Task<int> GetScopedCompanyIdForTemplatesAsync()
        {
            if (_currentUser.IsAdmin) return 0;
            var companyId = await _scope.GetScopedCompanyIdAsync();
            if (!companyId.HasValue) throw new ForbiddenException("Escopo de company inválido.");
            return companyId.Value;
        }

        private async Task<int> GetCompanyIdForWriteAsync()
        {
            var companyId = await _scope.GetScopedCompanyIdAsync();
            if (!companyId.HasValue) throw new ForbiddenException("Escopo de company inválido.");
            return companyId.Value;
        }

        private static ChecklistTemplateDTO Map(ChecklistTemplate x) => new()
        {
            Id = x.Id,
            CompanyId = x.CompanyId ?? 0,
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

        private static List<ChecklistTemplate> DefaultAirbnbTemplates()
        {
            return new List<ChecklistTemplate>
            {
                new ChecklistTemplate
                {
                    CompanyId = null,
                    Name = "Airbnb Turnover Padrão",
                    Description = "Checklist completo para limpeza entre hóspedes, reposição e inspeção final.",
                    TemplateType = "airbnb",
                    IsSystemTemplate = true,
                    IsActive = true,
                    Items = new List<ChecklistTemplateItem>
                    {
                        new() { SpaceName = "Entrada", Title = "Verificar fechadura, maçaneta e acesso", SortOrder = 1 },
                        new() { SpaceName = "Entrada", Title = "Limpar porta, tapete e interruptores", SortOrder = 2 },
                        new() { SpaceName = "Sala", Title = "Aspirar/varrer piso e limpar rodapés", SortOrder = 3 },
                        new() { SpaceName = "Sala", Title = "Limpar sofá, almofadas e manta", SortOrder = 4 },
                        new() { SpaceName = "Sala", Title = "Limpar mesa, TV, controles e superfícies", SortOrder = 5 },
                        new() { SpaceName = "Quarto", Title = "Trocar roupa de cama completa", RequiresPhoto = true, SortOrder = 6 },
                        new() { SpaceName = "Quarto", Title = "Inspecionar colchão, travesseiros e cabeceira", SortOrder = 7 },
                        new() { SpaceName = "Quarto", Title = "Organizar armário, cabides e criados", SortOrder = 8 },
                        new() { SpaceName = "Banheiro", Title = "Higienizar vaso, pia, espelho e box", RequiresPhoto = true, SortOrder = 9 },
                        new() { SpaceName = "Banheiro", Title = "Repor papel, sabonete e amenities", SortOrder = 10 },
                        new() { SpaceName = "Cozinha", Title = "Limpar bancada, pia, fogão e micro-ondas", SortOrder = 11 },
                        new() { SpaceName = "Cozinha", Title = "Conferir utensílios, louças e eletros", SortOrder = 12 },
                        new() { SpaceName = "Cozinha", Title = "Descartar lixo e trocar sacos", SortOrder = 13 },
                        new() { SpaceName = "Lavanderia", Title = "Conferir máquina, tanque e produtos", SortOrder = 14 },
                        new() { SpaceName = "Finalização", Title = "Checar cheiro, iluminação e temperatura", SortOrder = 15 },
                        new() { SpaceName = "Finalização", Title = "Registrar fotos finais da unidade pronta", RequiresPhoto = true, SortOrder = 16 }
                    }
                },
                new ChecklistTemplate
                {
                    CompanyId = null,
                    Name = "Airbnb Check-out Express",
                    Description = "Modelo enxuto para conferência rápida em unidades com alta rotatividade.",
                    TemplateType = "airbnb",
                    IsSystemTemplate = true,
                    IsActive = true,
                    Items = new List<ChecklistTemplateItem>
                    {
                        new() { SpaceName = "Geral", Title = "Remover lixo e itens esquecidos", SortOrder = 1 },
                        new() { SpaceName = "Geral", Title = "Ventilar ambiente e checar odores", SortOrder = 2 },
                        new() { SpaceName = "Quarto", Title = "Trocar cama e organizar travesseiros", RequiresPhoto = true, SortOrder = 3 },
                        new() { SpaceName = "Banheiro", Title = "Higienizar louças e secar superfícies", SortOrder = 4 },
                        new() { SpaceName = "Cozinha", Title = "Conferir pia, louças e bancada", SortOrder = 5 },
                        new() { SpaceName = "Finalização", Title = "Tirar foto final e liberar unidade", RequiresPhoto = true, SortOrder = 6 }
                    }
                }
            };
        }
    }
}
