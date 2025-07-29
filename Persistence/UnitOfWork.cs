using Microsoft.EntityFrameworkCore;
using InterviewBot.Domain.Interfaces;
using InterviewBot.Persistence.Repositories;

namespace InterviewBot.Persistence
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly InterviewBotDbContext _dbContext;
        private readonly ILogger<UnitOfWork> _logger;
        
        private IInterviewRepository? _interviewRepository;
        private IParticipantRepository? _participantRepository;
        private IAvailabilityRepository? _availabilityRepository;
        
        public UnitOfWork(
            InterviewBotDbContext dbContext,
            ILogger<UnitOfWork> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }
        
        public IInterviewRepository Interviews => _interviewRepository ??= new InterviewRepository(_dbContext);
        public IParticipantRepository Participants => _participantRepository ??= new ParticipantRepository(_dbContext);
        public IAvailabilityRepository Availability => _availabilityRepository ??= new AvailabilityRepository(_dbContext);
        
        public async Task<bool> SaveChangesAsync()
        {
            try
            {
                var changes = await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Saved {Changes} changes to database", changes);
                return true;
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving changes to database");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving changes to database");
                return false;
            }
        }
        
        public void Dispose()
        {
            _dbContext.Dispose();
        }
    }
}