using Microsoft.EntityFrameworkCore;
using InterviewBot.Domain.Entities;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Persistence.Repositories
{
    public class ParticipantRepository : IParticipantRepository
    {
        private readonly InterviewBotDbContext _dbContext;
        
        public ParticipantRepository(InterviewBotDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        
        public async Task<Participant?> GetByIdAsync(Guid id)
        {
            return await _dbContext.Participants
                .Include(p => p.InterviewParticipants)
                .Include(p => p.AvailabilityRecords)
                .FirstOrDefaultAsync(p => p.Id == id);
        }
        
        public async Task<Participant?> GetByEmailAsync(string email)
        {
            return await _dbContext.Participants
                .Include(p => p.InterviewParticipants)
                .Include(p => p.AvailabilityRecords)
                .FirstOrDefaultAsync(p => p.Email == email);
        }
        
        public async Task<IEnumerable<Participant>> GetByIdsAsync(IEnumerable<Guid> ids)
        {
            return await _dbContext.Participants
                .Include(p => p.InterviewParticipants)
                .Include(p => p.AvailabilityRecords)
                .Where(p => ids.Contains(p.Id))
                .ToListAsync();
        }
        
        public async Task AddAsync(Participant participant)
        {
            await _dbContext.Participants.AddAsync(participant);
        }
        
        public async Task UpdateAsync(Participant participant)
        {
            _dbContext.Participants.Update(participant);
            await Task.CompletedTask;
        }
    }
}