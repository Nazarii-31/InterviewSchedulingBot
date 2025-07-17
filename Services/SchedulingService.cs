using InterviewSchedulingBot.Interfaces;
using InterviewSchedulingBot.Models;

namespace InterviewSchedulingBot.Services
{
    public class SchedulingService : ISchedulingService
    {
        private readonly IGraphCalendarService _calendarService;
        private readonly IConfiguration _configuration;

        public SchedulingService(IGraphCalendarService calendarService, IConfiguration configuration)
        {
            _calendarService = calendarService;
            _configuration = configuration;
        }

        public async Task<SchedulingResponse> FindAvailableTimeSlotsAsync(AvailabilityRequest request, string userId)
        {
            try
            {
                if (!request.IsValid())
                {
                    return SchedulingResponse.CreateFailure("Invalid availability request parameters", request);
                }

                // Get busy times for all attendees
                var attendeeBusyTimes = await _calendarService.GetFreeBusyInformationAsync(
                    request.AttendeeEmails, 
                    request.StartDate, 
                    request.EndDate, 
                    userId);

                // Find common availability
                var availableSlots = GetCommonAvailability(attendeeBusyTimes, request);

                if (availableSlots.Count == 0)
                {
                    return SchedulingResponse.CreateFailure(
                        "No common availability found for the specified attendees and time period", 
                        request);
                }

                return SchedulingResponse.CreateSuccess(availableSlots, request);
            }
            catch (Exception ex)
            {
                return SchedulingResponse.CreateFailure($"Error finding available time slots: {ex.Message}", request);
            }
        }

        public List<AvailableTimeSlot> GetCommonAvailability(Dictionary<string, List<AvailableTimeSlot>> attendeeBusyTimes, AvailabilityRequest request)
        {
            // Generate all possible working hour slots within the date range
            var workingHourSlots = GenerateWorkingHourSlots(request.StartDate, request.EndDate, request);

            // Find slots that don't conflict with any attendee's busy times
            var availableSlots = new List<AvailableTimeSlot>();

            foreach (var workingSlot in workingHourSlots)
            {
                if (workingSlot.CanAccommodate(request.DurationMinutes))
                {
                    // Check if this slot conflicts with any attendee's busy time
                    bool isAvailable = true;
                    
                    foreach (var attendeeEmail in request.AttendeeEmails)
                    {
                        if (attendeeBusyTimes.ContainsKey(attendeeEmail))
                        {
                            var busyTimes = attendeeBusyTimes[attendeeEmail];
                            
                            // Check if the required duration slot conflicts with any busy time
                            var requiredSlot = new AvailableTimeSlot(workingSlot.StartTime, 
                                workingSlot.StartTime.AddMinutes(request.DurationMinutes));
                            
                            if (busyTimes.Any(busyTime => busyTime.OverlapsWith(requiredSlot)))
                            {
                                isAvailable = false;
                                break;
                            }
                        }
                    }

                    if (isAvailable)
                    {
                        // Create a slot with the exact duration needed
                        var availableSlot = new AvailableTimeSlot(workingSlot.StartTime, 
                            workingSlot.StartTime.AddMinutes(request.DurationMinutes));
                        availableSlots.Add(availableSlot);
                    }
                }
            }

            // Remove overlapping slots and limit results
            var optimizedSlots = OptimizeTimeSlots(availableSlots, request.DurationMinutes);
            
            // Sort by start time and take top 20 results
            return optimizedSlots.OrderBy(slot => slot.StartTime).Take(20).ToList();
        }

        public List<AvailableTimeSlot> FilterByWorkingHours(List<AvailableTimeSlot> timeSlots, AvailabilityRequest request)
        {
            return timeSlots.Where(slot =>
            {
                var startTime = slot.StartTime.TimeOfDay;
                var endTime = slot.EndTime.TimeOfDay;
                
                return request.WorkingDays.Contains(slot.StartTime.DayOfWeek) &&
                       startTime >= request.WorkingHoursStart &&
                       endTime <= request.WorkingHoursEnd;
            }).ToList();
        }

        public List<AvailableTimeSlot> GenerateWorkingHourSlots(DateTime startDate, DateTime endDate, AvailabilityRequest request)
        {
            var workingSlots = new List<AvailableTimeSlot>();
            var currentDate = startDate.Date;

            while (currentDate <= endDate.Date)
            {
                // Check if current date is a working day
                if (request.WorkingDays.Contains(currentDate.DayOfWeek))
                {
                    var workingStart = currentDate.Add(request.WorkingHoursStart);
                    var workingEnd = currentDate.Add(request.WorkingHoursEnd);

                    // Adjust for the actual start/end dates
                    if (workingStart < startDate)
                        workingStart = startDate;
                    if (workingEnd > endDate)
                        workingEnd = endDate;

                    // Generate 30-minute slots within working hours
                    var slotStart = workingStart;
                    while (slotStart.AddMinutes(30) <= workingEnd)
                    {
                        var slotEnd = slotStart.AddMinutes(30);
                        workingSlots.Add(new AvailableTimeSlot(slotStart, slotEnd));
                        slotStart = slotStart.AddMinutes(30);
                    }
                }

                currentDate = currentDate.AddDays(1);
            }

            return workingSlots;
        }

        private List<AvailableTimeSlot> OptimizeTimeSlots(List<AvailableTimeSlot> availableSlots, int durationMinutes)
        {
            var optimized = new List<AvailableTimeSlot>();
            var sorted = availableSlots.OrderBy(slot => slot.StartTime).ToList();

            foreach (var slot in sorted)
            {
                // Skip if this slot overlaps with an already selected slot
                bool overlaps = optimized.Any(existing => slot.OverlapsWith(existing));
                
                if (!overlaps)
                {
                    optimized.Add(slot);
                }
            }

            return optimized;
        }
    }
}