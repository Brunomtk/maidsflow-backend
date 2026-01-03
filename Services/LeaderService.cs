using Core.Models;
using Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Exceptions;
using Services.Security;

namespace Services
{
    public class LeaderService : ILeaderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUser _currentUser;

        public LeaderService(IUnitOfWork unitOfWork, ICurrentUser currentUser)
        {
            _unitOfWork = unitOfWork;
            _currentUser = currentUser;
        }

        private void EnsureAdmin()
        {
            if (!_currentUser.IsAdmin)
                throw new ForbiddenException("Somente admin pode acessar Leaders.");
        }

        public async Task<IEnumerable<Leader>> GetAllAsync()
        {
            EnsureAdmin();
            return await _unitOfWork.Leaders.GetAllAsync();
        }

        public async Task<Leader?> GetByIdAsync(int id)
        {
            EnsureAdmin();
            return await _unitOfWork.Leaders.GetByIdAsync(id);
        }

        public async Task<Leader> CreateAsync(Leader leader)
        {
            EnsureAdmin();
            leader.CreatedDate = DateTime.UtcNow;
            leader.UpdatedDate = DateTime.UtcNow;
            await _unitOfWork.Leaders.Add(leader);
            await _unitOfWork.SaveAsync();
            return leader;
        }

        public async Task<Leader?> UpdateAsync(int id, Leader updatedLeader)
        {
            EnsureAdmin();
            var leader = await _unitOfWork.Leaders.GetByIdAsync(id);
            if (leader == null) return null;

            leader.Name = updatedLeader.Name;
            leader.Email = updatedLeader.Email;
            leader.Phone = updatedLeader.Phone;
            leader.UserId = updatedLeader.UserId;
            leader.Region = updatedLeader.Region;
            leader.Status = updatedLeader.Status;
            leader.UpdatedDate = DateTime.UtcNow;

            _unitOfWork.Leaders.Update(leader);
            await _unitOfWork.SaveAsync();
            return leader;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            EnsureAdmin();
            var leader = await _unitOfWork.Leaders.GetByIdAsync(id);
            if (leader == null) return false;

            _unitOfWork.Leaders.Delete(leader);
            await _unitOfWork.SaveAsync();
            return true;
        }
    }

    public interface ILeaderService
    {
        Task<IEnumerable<Leader>> GetAllAsync();
        Task<Leader?> GetByIdAsync(int id);
        Task<Leader> CreateAsync(Leader leader);
        Task<Leader?> UpdateAsync(int id, Leader updatedLeader);
        Task<bool> DeleteAsync(int id);
    }
}
