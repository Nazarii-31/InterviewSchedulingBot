using InterviewSchedulingBot.Models;
using InterviewSchedulingBot.Interfaces;
using System.Collections.Concurrent;

namespace InterviewSchedulingBot.Services
{
    /// <summary>
    /// In-memory implementation of scheduling history repository
    /// For production use, this should be replaced with a persistent database
    /// </summary>
    public class InMemorySchedulingHistoryRepository : ISchedulingHistoryRepository
    {
        private readonly ConcurrentDictionary<string, List<SchedulingHistoryEntry>> _schedulingHistory = new();
        private readonly ConcurrentDictionary<string, UserPreferences> _userPreferences = new();
        private readonly ConcurrentDictionary<string, List<SchedulingPattern>> _schedulingPatterns = new();
        private readonly ILogger<InMemorySchedulingHistoryRepository> _logger;

        public InMemorySchedulingHistoryRepository(ILogger<InMemorySchedulingHistoryRepository> logger)
        {
            _logger = logger;
            InitializeSampleData();
        }

        public Task StoreSchedulingHistoryAsync(SchedulingHistoryEntry entry)
        {
            try
            {
                _schedulingHistory.AddOrUpdate(
                    entry.UserId,
                    new List<SchedulingHistoryEntry> { entry },
                    (key, existing) =>
                    {
                        existing.Add(entry);
                        // Keep only last 1000 entries per user to manage memory
                        if (existing.Count > 1000)
                        {
                            existing = existing.OrderByDescending(e => e.CreatedAt).Take(1000).ToList();
                        }
                        return existing;
                    });

                _logger.LogInformation("Stored scheduling history for user {UserId}", entry.UserId);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing scheduling history for user {UserId}", entry.UserId);
                throw;
            }
        }

        public Task<List<SchedulingHistoryEntry>> GetSchedulingHistoryAsync(string userId, int lookbackDays = 90)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-lookbackDays);
                
                if (_schedulingHistory.TryGetValue(userId, out var history))
                {
                    var filteredHistory = history
                        .Where(e => e.CreatedAt >= cutoffDate)
                        .OrderByDescending(e => e.CreatedAt)
                        .ToList();
                    
                    _logger.LogInformation("Retrieved {Count} scheduling history entries for user {UserId}", 
                        filteredHistory.Count, userId);
                    
                    return Task.FromResult(filteredHistory);
                }

                _logger.LogInformation("No scheduling history found for user {UserId}", userId);
                return Task.FromResult(new List<SchedulingHistoryEntry>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scheduling history for user {UserId}", userId);
                throw;
            }
        }

        public Task<Dictionary<string, List<SchedulingHistoryEntry>>> GetSchedulingHistoryAsync(List<string> userIds, int lookbackDays = 90)
        {
            try
            {
                var result = new Dictionary<string, List<SchedulingHistoryEntry>>();
                var cutoffDate = DateTime.UtcNow.AddDays(-lookbackDays);

                foreach (var userId in userIds)
                {
                    if (_schedulingHistory.TryGetValue(userId, out var history))
                    {
                        var filteredHistory = history
                            .Where(e => e.CreatedAt >= cutoffDate)
                            .OrderByDescending(e => e.CreatedAt)
                            .ToList();
                        
                        result[userId] = filteredHistory;
                    }
                    else
                    {
                        result[userId] = new List<SchedulingHistoryEntry>();
                    }
                }

                _logger.LogInformation("Retrieved scheduling history for {UserCount} users", userIds.Count);
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scheduling history for multiple users");
                throw;
            }
        }

        public Task StoreUserPreferencesAsync(UserPreferences preferences)
        {
            try
            {
                preferences.LastUpdated = DateTime.UtcNow;
                _userPreferences.AddOrUpdate(preferences.UserId, preferences, (key, existing) => preferences);
                
                _logger.LogInformation("Stored user preferences for user {UserId}", preferences.UserId);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing user preferences for user {UserId}", preferences.UserId);
                throw;
            }
        }

        public Task<UserPreferences?> GetUserPreferencesAsync(string userId)
        {
            try
            {
                if (_userPreferences.TryGetValue(userId, out var preferences))
                {
                    _logger.LogInformation("Retrieved user preferences for user {UserId}", userId);
                    return Task.FromResult<UserPreferences?>(preferences);
                }

                _logger.LogInformation("No user preferences found for user {UserId}", userId);
                return Task.FromResult<UserPreferences?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving user preferences for user {UserId}", userId);
                throw;
            }
        }

        public Task StoreSchedulingPatternsAsync(List<SchedulingPattern> patterns)
        {
            try
            {
                foreach (var pattern in patterns)
                {
                    _schedulingPatterns.AddOrUpdate(
                        pattern.UserId,
                        new List<SchedulingPattern> { pattern },
                        (key, existing) =>
                        {
                            existing.RemoveAll(p => p.PatternId == pattern.PatternId);
                            existing.Add(pattern);
                            return existing;
                        });
                }

                _logger.LogInformation("Stored {Count} scheduling patterns", patterns.Count);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error storing scheduling patterns");
                throw;
            }
        }

        public Task<List<SchedulingPattern>> GetSchedulingPatternsAsync(string userId)
        {
            try
            {
                if (_schedulingPatterns.TryGetValue(userId, out var patterns))
                {
                    _logger.LogInformation("Retrieved {Count} scheduling patterns for user {UserId}", 
                        patterns.Count, userId);
                    return Task.FromResult(patterns);
                }

                _logger.LogInformation("No scheduling patterns found for user {UserId}", userId);
                return Task.FromResult(new List<SchedulingPattern>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving scheduling patterns for user {UserId}", userId);
                throw;
            }
        }

        /// <summary>
        /// Initializes sample data for demonstration purposes
        /// </summary>
        private void InitializeSampleData()
        {
            try
            {
                var sampleUsers = new[] { "user1", "user2", "demo-user", "test-user" };
                var random = new Random();

                foreach (var userId in sampleUsers)
                {
                    // Generate sample scheduling history
                    var historyEntries = new List<SchedulingHistoryEntry>();
                    for (int i = 0; i < 20; i++)
                    {
                        var scheduledTime = DateTime.UtcNow.AddDays(-random.Next(1, 90));
                        historyEntries.Add(new SchedulingHistoryEntry
                        {
                            UserId = userId,
                            AttendeeEmails = new List<string> { $"attendee{i}@company.com", $"attendee{i + 1}@company.com" },
                            ScheduledTime = scheduledTime,
                            DurationMinutes = random.Next(30, 120),
                            DayOfWeek = scheduledTime.DayOfWeek,
                            TimeOfDay = scheduledTime.TimeOfDay,
                            UserSatisfactionScore = random.NextDouble() * 0.5 + 0.5, // 0.5-1.0 range
                            WasRescheduled = random.NextDouble() < 0.2, // 20% chance of rescheduling
                            MeetingCompleted = random.NextDouble() < 0.9, // 90% completion rate
                            TimeZone = "UTC"
                        });
                    }
                    _schedulingHistory[userId] = historyEntries;

                    // Generate sample user preferences
                    var preferences = new UserPreferences
                    {
                        UserId = userId,
                        PreferredDays = new List<DayOfWeek> { DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday },
                        PreferredTimes = new List<TimeSpan> { new TimeSpan(10, 0, 0), new TimeSpan(14, 0, 0) },
                        PreferredDurationMinutes = 60,
                        MorningPreference = random.NextDouble() * 0.5 + 0.3,
                        AfternoonPreference = random.NextDouble() * 0.5 + 0.4,
                        EveningPreference = random.NextDouble() * 0.3,
                        OptimalStartTime = new TimeSpan(9 + random.Next(0, 3), 0, 0),
                        OptimalEndTime = new TimeSpan(15 + random.Next(0, 3), 0, 0),
                        TotalScheduledMeetings = historyEntries.Count,
                        AverageReschedulingRate = historyEntries.Count(e => e.WasRescheduled) / (double)historyEntries.Count
                    };

                    // Generate day preference scores
                    foreach (DayOfWeek day in Enum.GetValues<DayOfWeek>())
                    {
                        var dayScore = day >= DayOfWeek.Monday && day <= DayOfWeek.Friday ? 
                            random.NextDouble() * 0.5 + 0.5 : 
                            random.NextDouble() * 0.3;
                        preferences.DayPreferenceScores[day] = dayScore;
                    }

                    _userPreferences[userId] = preferences;

                    // Generate sample scheduling patterns
                    var patterns = new List<SchedulingPattern>();
                    for (int i = 0; i < 5; i++)
                    {
                        patterns.Add(new SchedulingPattern
                        {
                            UserId = userId,
                            DayOfWeek = (DayOfWeek)((i % 5) + 1), // Monday to Friday
                            StartTime = new TimeSpan(9 + i, 0, 0),
                            EndTime = new TimeSpan(10 + i, 0, 0),
                            FrequencyCount = random.Next(1, 10),
                            SuccessRate = random.NextDouble() * 0.5 + 0.5,
                            AverageUserSatisfaction = random.NextDouble() * 0.5 + 0.5,
                            AverageDurationMinutes = 60,
                            ReschedulingRate = random.NextDouble() * 0.3,
                            LastOccurrence = DateTime.UtcNow.AddDays(-random.Next(1, 30)),
                            PatternType = "Regular"
                        });
                    }
                    _schedulingPatterns[userId] = patterns;
                }

                _logger.LogInformation("Initialized sample data for {UserCount} users", sampleUsers.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing sample data");
            }
        }
    }
}