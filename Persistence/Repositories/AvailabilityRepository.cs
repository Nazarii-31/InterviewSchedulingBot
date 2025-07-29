using Microsoft.EntityFrameworkCore;
using InterviewBot.Domain.Entities;
using InterviewBot.Domain.Interfaces;

namespace InterviewBot.Persistence.Repositories
{
    public class AvailabilityRepository : IAvailabilityRepository
    {
        private readonly InterviewBotDbContext _dbContext;
        
        public AvailabilityRepository(InterviewBotDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        
        public async Task StoreAvailabilityAsync(string participantId, List<TimeSlot> availability, DateTime startDate, DateTime endDate)
        {
            if (!Guid.TryParse(participantId, out var participantGuid))
            {
                throw new ArgumentException("Invalid participant ID format", nameof(participantId));
            }
            
            // Remove existing availability for the date range
            var existingRecords = await _dbContext.AvailabilityRecords
                .Include(ar => ar.TimeSlots)
                .Where(ar => ar.ParticipantId == participantGuid)
                .Where(ar => ar.Date >= startDate.Date && ar.Date <= endDate.Date)
                .ToListAsync();
                
            _dbContext.AvailabilityRecords.RemoveRange(existingRecords);
            
            // Group availability by date
            var availabilityByDate = availability
                .GroupBy(ts => ts.StartTime.Date)
                .ToList();
                
            foreach (var dateGroup in availabilityByDate)
            {
                var availabilityRecord = new AvailabilityRecord
                {
                    ParticipantId = participantGuid,
                    Date = dateGroup.Key,
                    LastUpdated = DateTime.UtcNow
                };
                
                await _dbContext.AvailabilityRecords.AddAsync(availabilityRecord);
                
                foreach (var timeSlot in dateGroup)
                {
                    timeSlot.AvailabilityRecordId = availabilityRecord.Id;
                    await _dbContext.TimeSlots.AddAsync(timeSlot);
                }
            }
        }
        
        public async Task<List<TimeSlot>> GetAvailabilityAsync(string participantId, DateTime startDate, DateTime endDate)
        {
            if (!Guid.TryParse(participantId, out var participantGuid))
            {
                return new List<TimeSlot>();
            }
            
            var timeSlots = await _dbContext.TimeSlots
                .Include(ts => ts.AvailabilityRecord)
                .Where(ts => ts.AvailabilityRecord.ParticipantId == participantGuid)
                .Where(ts => ts.StartTime >= startDate && ts.EndTime <= endDate)
                .OrderBy(ts => ts.StartTime)
                .ToListAsync();
                
            return timeSlots;
        }
        
        public async Task<DateTime?> GetLastUpdateTimeAsync(string participantId)
        {
            if (!Guid.TryParse(participantId, out var participantGuid))
            {
                return null;
            }
            
            var lastUpdate = await _dbContext.AvailabilityRecords
                .Where(ar => ar.ParticipantId == participantGuid)
                .MaxAsync(ar => (DateTime?)ar.LastUpdated);
                
            return lastUpdate;
        }
    }
}