using Microsoft.EntityFrameworkCore;
using InterviewBot.Domain.Entities;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Persistence.Repositories
{
    public class InterviewRepository : IInterviewRepository
    {
        private readonly InterviewBotDbContext _dbContext;
        
        public InterviewRepository(InterviewBotDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        
        public async Task<Interview?> GetByIdAsync(Guid id)
        {
            return await _dbContext.Interviews
                .Include(i => i.InterviewParticipants)
                    .ThenInclude(ip => ip.Participant)
                .FirstOrDefaultAsync(i => i.Id == id);
        }
        
        public async Task<IEnumerable<Interview>> GetUpcomingInterviewsAsync(DateTime from, DateTime to)
        {
            return await _dbContext.Interviews
                .Include(i => i.InterviewParticipants)
                    .ThenInclude(ip => ip.Participant)
                .Where(i => i.StartTime >= from && i.StartTime <= to)
                .Where(i => i.Status != InterviewStatus.Cancelled)
                .OrderBy(i => i.StartTime)
                .ToListAsync();
        }
        
        public async Task<IEnumerable<Interview>> GetByParticipantAsync(Guid participantId)
        {
            return await _dbContext.Interviews
                .Include(i => i.InterviewParticipants)
                    .ThenInclude(ip => ip.Participant)
                .Where(i => i.InterviewParticipants.Any(ip => ip.ParticipantId == participantId))
                .OrderBy(i => i.StartTime)
                .ToListAsync();
        }
        
        public async Task AddAsync(Interview interview)
        {
            await _dbContext.Interviews.AddAsync(interview);
        }
        
        public async Task UpdateAsync(Interview interview)
        {
            _dbContext.Interviews.Update(interview);
            await Task.CompletedTask;
        }
        
        public async Task DeleteAsync(Guid id)
        {
            var interview = await _dbContext.Interviews.FindAsync(id);
            if (interview != null)
            {
                _dbContext.Interviews.Remove(interview);
            }
        }
    }
}