using InterviewBot.Domain.Entities;

namespace InterviewBot.Domain.Interfaces
{
    public interface IInterviewRepository
    {
        Task<Interview?> GetByIdAsync(Guid id);
        Task<IEnumerable<Interview>> GetUpcomingInterviewsAsync(DateTime from, DateTime to);
        Task<IEnumerable<Interview>> GetByParticipantAsync(Guid participantId);
        Task AddAsync(Interview interview);
        Task UpdateAsync(Interview interview);
        Task DeleteAsync(Guid id);
    }
    
    public interface IParticipantRepository
    {
        Task<Participant?> GetByIdAsync(Guid id);
        Task<Participant?> GetByEmailAsync(string email);
        Task<IEnumerable<Participant>> GetByIdsAsync(IEnumerable<Guid> ids);
        Task AddAsync(Participant participant);
        Task UpdateAsync(Participant participant);
    }
    
    public interface IAvailabilityRepository
    {
        Task StoreAvailabilityAsync(string participantId, List<TimeSlot> availability, DateTime startDate, DateTime endDate);
        Task<List<TimeSlot>> GetAvailabilityAsync(string participantId, DateTime startDate, DateTime endDate);
        Task<DateTime?> GetLastUpdateTimeAsync(string participantId);
    }
    
    public interface IUnitOfWork : IDisposable
    {
        IInterviewRepository Interviews { get; }
        IParticipantRepository Participants { get; }
        IAvailabilityRepository Availability { get; }
        
        Task<bool> SaveChangesAsync();
    }
}