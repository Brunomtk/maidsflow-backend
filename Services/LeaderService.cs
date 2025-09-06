using Core.Models;
using Infrastructure.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Services
{
    public class LeaderService : ILeaderService
    {
        private readonly IUnitOfWork _unitOfWork;

        public LeaderService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<Leader>> GetAllAsync()
        {
            return await _unitOfWork.Leaders.GetAllAsync();
        }

        public async Task<Leader?> GetByIdAsync(int id)
        {
            return await _unitOfWork.Leaders.GetByIdAsync(id);
        }

        public async Task<Leader> CreateAsync(Leader leader)
        {
            leader.CreatedDate = DateTime.UtcNow;
            leader.UpdatedDate = DateTime.UtcNow;  // Ajuste aqui
            await _unitOfWork.Leaders.Add(leader);
            await _unitOfWork.SaveAsync();
            return leader;
        }

        public async Task<Leader?> UpdateAsync(int id, Leader updatedLeader)
        {
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
