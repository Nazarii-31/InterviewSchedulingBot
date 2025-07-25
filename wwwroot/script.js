// Interview Scheduling Bot - JavaScript functionality

// Tab functionality
function openTab(evt, tabName) {
    var i, tabcontent, tablinks;
    
    // Hide all tab content
    tabcontent = document.getElementsByClassName("tab-content");
    for (i = 0; i < tabcontent.length; i++) {
        tabcontent[i].classList.remove("active");
    }
    
    // Remove active class from all tab buttons
    tablinks = document.getElementsByClassName("tab-button");
    for (i = 0; i < tablinks.length; i++) {
        tablinks[i].classList.remove("active");
    }
    
    // Show the current tab and mark button as active
    document.getElementById(tabName).classList.add("active");
    evt.currentTarget.classList.add("active");
}

// Initialize default dates
function initializeDates() {
    const now = new Date();
    const tomorrow = new Date(now);
    tomorrow.setDate(tomorrow.getDate() + 1);
    tomorrow.setHours(9, 0, 0, 0);
    
    const weekLater = new Date(tomorrow);
    weekLater.setDate(weekLater.getDate() + 7);
    weekLater.setHours(17, 0, 0, 0);
    
    const earliestInput = document.getElementById('earliest-date');
    const latestInput = document.getElementById('latest-date');
    const valEarliestInput = document.getElementById('val-earliest');
    const valLatestInput = document.getElementById('val-latest');
    const proposedTimeInput = document.getElementById('proposed-time');
    
    if (earliestInput) earliestInput.value = formatDateTimeLocal(tomorrow);
    if (latestInput) latestInput.value = formatDateTimeLocal(weekLater);
    if (valEarliestInput) valEarliestInput.value = formatDateTimeLocal(tomorrow);
    if (valLatestInput) valLatestInput.value = formatDateTimeLocal(weekLater);
    if (proposedTimeInput) proposedTimeInput.value = formatDateTimeLocal(tomorrow);
}

// Format date for datetime-local input
function formatDateTimeLocal(date) {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    
    return `${year}-${month}-${day}T${hours}:${minutes}`;
}

// Show/hide loading spinner
function showLoading() {
    document.getElementById('loading').style.display = 'flex';
}

function hideLoading() {
    document.getElementById('loading').style.display = 'none';
}

// API call helper
async function makeApiCall(endpoint, data) {
    try {
        showLoading();
        const response = await fetch(`/api/scheduling/${endpoint}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify(data)
        });
        
        const result = await response.json();
        
        if (!response.ok) {
            throw new Error(result.error || `HTTP error! status: ${response.status}`);
        }
        
        return result;
    } catch (error) {
        console.error('API call failed:', error);
        throw error;
    } finally {
        hideLoading();
    }
}

// Parse email list from textarea
function parseEmailList(emailText) {
    return emailText.split('\n')
        .map(email => email.trim())
        .filter(email => email.length > 0);
}

// Initialize the application
document.addEventListener('DOMContentLoaded', function() {
    initializeDates();
    initializeMockData();
    
    // Show the first tab by default
    document.querySelector('.tab-button').click();
    
    // Add form event listeners
    setupFormEventListeners();
    
    // Sync initial mock data with backend
    syncMockDataWithBackend();
});

// Setup form event listeners
function setupFormEventListeners() {
    // Find Optimal Slots functionality
    document.getElementById('scheduling-form').addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const participants = parseEmailList(document.getElementById('participants').value);
        const duration = parseInt(document.getElementById('duration').value);
        const earliestDate = new Date(document.getElementById('earliest-date').value);
        const latestDate = new Date(document.getElementById('latest-date').value);
        const interviewType = document.getElementById('interview-type').value;
        const priority = document.getElementById('priority').value;
        const requesterId = document.getElementById('requester-id').value;
        const department = document.getElementById('department').value;
        
        const requestData = {
            participantEmails: participants,
            durationMinutes: duration,
            earliestDate: earliestDate.toISOString(),
            latestDate: latestDate.toISOString(),
            interviewType: interviewType,
            priority: priority,
            requesterId: requesterId || null,
            department: department || null
        };
        
        try {
            const result = await makeApiCall('find-optimal-slots', requestData);
            displaySchedulingResults(result, participants.length);
        } catch (error) {
            displayError('scheduling-results', 'Failed to find optimal slots: ' + error.message);
        }
    });

    // Validation functionality
    document.getElementById('validation-form').addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const participants = parseEmailList(document.getElementById('val-participants').value);
        const duration = parseInt(document.getElementById('val-duration').value);
        const earliestDate = new Date(document.getElementById('val-earliest').value);
        const latestDate = new Date(document.getElementById('val-latest').value);
        const interviewType = document.getElementById('val-type').value;
        
        const requestData = {
            participantEmails: participants,
            durationMinutes: duration,
            earliestDate: earliestDate.toISOString(),
            latestDate: latestDate.toISOString(),
            interviewType: interviewType,
            priority: 'Normal'
        };
        
        try {
            const result = await makeApiCall('validate', requestData);
            displayValidationResults(result);
        } catch (error) {
            displayError('validation-results', 'Failed to validate request: ' + error.message);
        }
    });

    // Conflict Analysis functionality
    document.getElementById('conflict-form').addEventListener('submit', async function(e) {
        e.preventDefault();
        
        const participants = parseEmailList(document.getElementById('conflict-participants').value);
        const proposedTime = new Date(document.getElementById('proposed-time').value);
        const duration = parseInt(document.getElementById('conflict-duration').value);
        const accessToken = document.getElementById('access-token').value;
        
        const requestData = {
            proposedTime: proposedTime.toISOString(),
            duration: duration,
            participantEmails: participants,
            accessToken: accessToken
        };
        
        try {
            const result = await makeApiCall('analyze-conflicts', requestData);
            displayConflictResults(result);
        } catch (error) {
            displayError('conflict-results', 'Failed to analyze conflicts: ' + error.message);
        }
    });
}

// Display scheduling results
function displaySchedulingResults(result, totalParticipants) {
    const resultsDiv = document.getElementById('scheduling-results');
    const recommendedDiv = document.getElementById('recommended-slots');
    const alternativeDiv = document.getElementById('alternative-slots');
    const insightsDiv = document.getElementById('business-insights');
    
    // Show recommended slots grouped by days
    if (result.recommendedSlots && result.recommendedSlots.length > 0) {
        recommendedDiv.innerHTML = createDayGroupedSlotsHtml(result.recommendedSlots, totalParticipants, 'Recommended');
    } else {
        recommendedDiv.innerHTML = '<p class="info-text"><i class="fas fa-info-circle"></i> No recommended slots found for the specified criteria.</p>';
    }
    
    // Show alternative slots grouped by days
    if (result.alternativeSlots && result.alternativeSlots.length > 0) {
        alternativeDiv.innerHTML = createDayGroupedSlotsHtml(result.alternativeSlots, totalParticipants, 'Alternative');
    } else {
        alternativeDiv.innerHTML = '<p class="info-text"><i class="fas fa-info-circle"></i> No alternative slots available.</p>';
    }
    
    // Show business insights
    if (result.insights) {
        insightsDiv.innerHTML = createBusinessInsightsHtml(result.insights);
    }
    
    resultsDiv.style.display = 'block';
    resultsDiv.scrollIntoView({ behavior: 'smooth' });
}

// Create HTML for slots grouped by days
function createDayGroupedSlotsHtml(slots, totalParticipants, slotType) {
    // Group slots by day
    const slotsByDay = new Map();
    
    slots.forEach(slot => {
        const timeSlot = slot.timeSlot || slot;
        const startDate = new Date(timeSlot.startTime);
        const dateKey = startDate.toDateString(); // Get date string like "Mon Jul 28 2025"
        
        if (!slotsByDay.has(dateKey)) {
            slotsByDay.set(dateKey, []);
        }
        slotsByDay.get(dateKey).push(slot);
    });
    
    // Sort days chronologically
    const sortedDays = Array.from(slotsByDay.keys()).sort((a, b) => new Date(a) - new Date(b));
    
    let html = '';
    
    sortedDays.forEach(dateKey => {
        const daySlots = slotsByDay.get(dateKey);
        const firstSlot = daySlots[0];
        const timeSlot = firstSlot.timeSlot || firstSlot;
        const startDate = new Date(timeSlot.startTime);
        
        // Format date for header
        const dateOptions = { 
            weekday: 'long', 
            year: 'numeric', 
            month: 'long', 
            day: 'numeric' 
        };
        const formattedDate = startDate.toLocaleDateString('en-US', dateOptions);
        
        html += `
            <div class="day-group">
                <div class="day-header">
                    <i class="fas fa-calendar-day"></i>
                    <h4>${formattedDate}</h4>
                    <span class="available-slots-count">${daySlots.length} available slot${daySlots.length !== 1 ? 's' : ''}</span>
                </div>
                <div class="day-slots">
                    ${daySlots.map(slot => createTimeSlotCardHtml(slot, totalParticipants)).join('')}
                </div>
            </div>
        `;
    });
    
    if (html === '') {
        html = `<p class="info-text"><i class="fas fa-info-circle"></i> No ${slotType.toLowerCase()} slots available for the selected date range.</p>`;
    }
    
    return html;
}

// Create HTML for individual time slot card
function createTimeSlotCardHtml(slot, totalParticipants) {
    const timeSlot = slot.timeSlot || slot;
    const score = slot.businessScore || timeSlot.confidence || 0;
    const reasons = slot.businessReasons || [timeSlot.reason] || [];
    
    const startDate = new Date(timeSlot.startTime);
    const endDate = new Date(timeSlot.endTime);
    
    const timeOptions = { 
        hour: '2-digit', 
        minute: '2-digit',
        hour12: true
    };
    
    const startTime = startDate.toLocaleTimeString('en-US', timeOptions);
    const endTime = endDate.toLocaleTimeString('en-US', timeOptions);
    
    // Calculate participant availability
    const availableCount = timeSlot.availableAttendees?.length || 0;
    const conflictingCount = timeSlot.conflictingAttendees?.length || 0;
    const totalCount = totalParticipants || availableCount + conflictingCount;
    
    return `
        <div class="time-slot-card">
            <div class="slot-time-row">
                <div class="slot-time">
                    <i class="fas fa-clock"></i>
                    <span class="time-range">${startTime} - ${endTime}</span>
                </div>
                <div class="slot-score">
                    <span class="score-badge score-${getScoreClass(score)}" title="Time Slot Quality Score - Based on time of day preference, working hours alignment, and calendar availability. Higher scores indicate more optimal meeting times.">
                        ${Math.round(score)}%
                    </span>
                </div>
            </div>
            <div class="slot-availability">
                <i class="fas fa-users"></i>
                <strong>Available:</strong> ${availableCount}/${totalCount} participants
                ${conflictingCount > 0 ? `<span class="conflicted-participants">• ${conflictingCount} conflicted</span>` : ''}
            </div>
            <div class="slot-reasons">
                ${reasons.map(reason => `<span class="reason-tag"><i class="fas fa-check-circle"></i>${reason}</span>`).join('')}
            </div>
        </div>
    `;
}

// Create HTML for time slot
function createTimeSlotHtml(slot, totalParticipants) {
    const timeSlot = slot.timeSlot || slot;
    const score = slot.businessScore || timeSlot.confidence || 0;
    const reasons = slot.businessReasons || [timeSlot.reason] || [];
    
    const startDate = new Date(timeSlot.startTime);
    const endDate = new Date(timeSlot.endTime);
    
    // Format date and time separately for better display
    const dateOptions = { 
        weekday: 'long', 
        year: 'numeric', 
        month: 'long', 
        day: 'numeric' 
    };
    const timeOptions = { 
        hour: '2-digit', 
        minute: '2-digit',
        hour12: true
    };
    
    const formattedDate = startDate.toLocaleDateString('en-US', dateOptions);
    const startTime = startDate.toLocaleTimeString('en-US', timeOptions);
    const endTime = endDate.toLocaleTimeString('en-US', timeOptions);
    
    // Calculate participant availability
    const availableCount = timeSlot.availableAttendees?.length || 0;
    const totalCount = totalParticipants || availableCount + (timeSlot.conflictingAttendees?.length || 0);
    
    return `
        <div class="time-slot-card enhanced">
            <div class="slot-date-header">
                <i class="fas fa-calendar-day"></i>
                <span class="slot-date">${formattedDate}</span>
            </div>
            <div class="slot-time-info">
                <div class="slot-time">
                    <i class="fas fa-clock"></i>
                    <span class="time-range">${startTime} - ${endTime}</span>
                </div>
                <div class="slot-score">
                    <span class="score-badge score-${getScoreClass(score)}" title="Time Slot Quality Score - Based on time of day preference, working hours alignment, and calendar availability. Higher scores indicate more optimal meeting times.">
                        ${Math.round(score)}%
                    </span>
                </div>
            </div>
            <div class="slot-details">
                <div class="slot-availability">
                    <i class="fas fa-users"></i>
                    <strong>Available:</strong> ${availableCount}/${totalCount} participants
                </div>
                <div class="slot-reasons">
                    ${reasons.map(reason => `<span class="reason-tag"><i class="fas fa-check-circle"></i>${reason}</span>`).join('')}
                </div>
            </div>
        </div>
    `;
}

// Get score class for styling
function getScoreClass(score) {
    if (score >= 90) return 'excellent';
    if (score >= 75) return 'good';
    if (score >= 60) return 'fair';
    return 'poor';
}

// Create business insights HTML
function createBusinessInsightsHtml(insights) {
    return `
        <div class="insights-container">
            <div class="insight-card">
                <h4><i class="fas fa-chart-line"></i> Average Availability</h4>
                <p>${insights.averageAvailability?.toFixed(1)}%</p>
            </div>
            
            <div class="insight-card">
                <h4><i class="fas fa-clock"></i> Best Time Windows</h4>
                <ul>
                    ${insights.bestTimeWindows?.map(window => `<li>${window}</li>`).join('') || '<li>No data available</li>'}
                </ul>
            </div>
            
            <div class="insight-card">
                <h4><i class="fas fa-lightbulb"></i> Scheduling Tips</h4>
                <ul>
                    ${insights.schedulingTips?.map(tip => `<li>${tip}</li>`).join('') || '<li>No tips available</li>'}
                </ul>
            </div>
            
            ${insights.challengingPeriods?.length ? `
                <div class="insight-card">
                    <h4><i class="fas fa-exclamation-triangle"></i> Challenging Periods</h4>
                    <ul>
                        ${insights.challengingPeriods.map(period => `<li>${period}</li>`).join('')}
                    </ul>
                </div>
            ` : ''}
        </div>
    `;
}

// Mock Data Section
let mockData = {
    userProfiles: [
        {
            id: '1',
            name: 'John Doe',
            email: 'john.doe@company.com',
            jobTitle: 'Senior Software Engineer',
            department: 'Engineering',
            timeZone: 'Pacific Standard Time'
        },
        {
            id: '2',
            name: 'Jane Smith',
            email: 'jane.smith@company.com',
            jobTitle: 'Product Manager',
            department: 'Product',
            timeZone: 'Eastern Standard Time'
        },
        {
            id: '3',
            name: 'Bob Wilson',
            email: 'interviewer@company.com',
            jobTitle: 'Engineering Manager',
            department: 'Engineering',
            timeZone: 'Pacific Standard Time'
        }
    ],
    workingHours: [
        {
            userEmail: 'john.doe@company.com',
            timeZone: 'Pacific Standard Time',
            daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
            startTime: '09:00:00',
            endTime: '17:00:00'
        },
        {
            userEmail: 'jane.smith@company.com',
            timeZone: 'Eastern Standard Time',
            daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
            startTime: '08:00:00',
            endTime: '16:00:00'
        },
        {
            userEmail: 'interviewer@company.com',
            timeZone: 'Pacific Standard Time',
            daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
            startTime: '08:30:00',
            endTime: '17:30:00'
        }
    ],
    presenceStatus: [
        {
            userEmail: 'john.doe@company.com',
            availability: 'Available',
            activity: 'Available',
            lastModified: new Date().toISOString()
        },
        {
            userEmail: 'jane.smith@company.com',
            availability: 'Busy',
            activity: 'InAMeeting',
            lastModified: new Date().toISOString()
        },
        {
            userEmail: 'interviewer@company.com',
            availability: 'Available',
            activity: 'Available',
            lastModified: new Date().toISOString()
        }
    ],
    calendarAvailability: [
        {
            userEmail: 'john.doe@company.com',
            busySlots: [
                {
                    start: '2025-01-28T10:00:00Z',
                    end: '2025-01-28T11:00:00Z',
                    status: 'Busy',
                    subject: 'Team Meeting'
                },
                {
                    start: '2025-01-28T14:00:00Z',
                    end: '2025-01-28T15:30:00Z',
                    status: 'Busy',
                    subject: 'Code Review'
                }
            ]
        },
        {
            userEmail: 'jane.smith@company.com',
            busySlots: [
                {
                    start: '2025-01-28T09:00:00Z',
                    end: '2025-01-28T10:00:00Z',
                    status: 'Busy',
                    subject: 'Product Planning'
                },
                {
                    start: '2025-01-28T15:00:00Z',
                    end: '2025-01-28T16:00:00Z',
                    status: 'Tentative',
                    subject: 'Client Call'
                }
            ]
        },
        {
            userEmail: 'interviewer@company.com',
            busySlots: [
                {
                    start: '2025-01-28T11:00:00Z',
                    end: '2025-01-28T12:00:00Z',
                    status: 'Busy',
                    subject: 'Interview - Candidate A'
                }
            ]
        }
    ]
};

// Initialize mock data display
function initializeMockData() {
    displayUnifiedUserData();
}

// Display unified user data
function displayUnifiedUserData() {
    const container = document.getElementById('unified-user-data');
    if (!container) return; // Exit if container doesn't exist
    
    const html = mockData.userProfiles.map(user => {
        // Find corresponding working hours, presence, and calendar data
        const workingHours = mockData.workingHours.find(wh => wh.userEmail === user.email) || {};
        const presence = mockData.presenceStatus.find(ps => ps.userEmail === user.email) || {};
        const calendar = mockData.calendarAvailability.find(ca => ca.userEmail === user.email) || { busySlots: [] };

        return `
            <div class="unified-user-card" data-user-id="${user.id}">
                <div class="card-header">
                    <div class="card-title">
                        <i class="fas fa-user"></i>
                        <span class="editable-field" contenteditable="true" data-field="name" data-section="profile">${user.name}</span>
                        <span class="presence-indicator presence-${presence.availability?.toLowerCase() || 'available'}" title="${presence.availability || 'Available'}"></span>
                    </div>
                    <div class="card-actions">
                        <button class="btn-small btn-edit" onclick="toggleUnifiedEdit(this)">
                            <i class="fas fa-edit"></i>
                        </button>
                        <button class="btn-small btn-delete" onclick="deleteUser('${user.id}')">
                            <i class="fas fa-trash"></i>
                        </button>
                    </div>
                </div>
                
                <div class="card-content">
                    <!-- Profile Information -->
                    <div class="user-section">
                        <h5><i class="fas fa-id-card"></i> Profile</h5>
                        <div class="data-row">
                            <span class="field-label">Email:</span>
                            <span class="field-value editable-field" contenteditable="true" data-field="email" data-section="profile">${user.email}</span>
                        </div>
                        <div class="data-row">
                            <span class="field-label">Job Title:</span>
                            <span class="field-value editable-field" contenteditable="true" data-field="jobTitle" data-section="profile">${user.jobTitle}</span>
                        </div>
                        <div class="data-row">
                            <span class="field-label">Department:</span>
                            <span class="field-value editable-field" contenteditable="true" data-field="department" data-section="profile">${user.department}</span>
                        </div>
                        <div class="data-row">
                            <span class="field-label">Time Zone:</span>
                            <select class="field-value editable-select" data-field="timeZone" data-section="profile">
                                <option value="Pacific Standard Time" ${user.timeZone === 'Pacific Standard Time' ? 'selected' : ''}>Pacific Standard Time</option>
                                <option value="Eastern Standard Time" ${user.timeZone === 'Eastern Standard Time' ? 'selected' : ''}>Eastern Standard Time</option>
                                <option value="GMT Standard Time" ${user.timeZone === 'GMT Standard Time' ? 'selected' : ''}>GMT Standard Time</option>
                                <option value="Central Standard Time" ${user.timeZone === 'Central Standard Time' ? 'selected' : ''}>Central Standard Time</option>
                            </select>
                        </div>
                    </div>

                    <!-- Working Hours -->
                    <div class="user-section">
                        <h5><i class="fas fa-clock"></i> Working Hours</h5>
                        <div class="data-row">
                            <span class="field-label">Start Time:</span>
                            <input type="time" class="field-value editable-field" data-field="startTime" data-section="workingHours" value="${workingHours.startTime || '09:00:00'}">
                        </div>
                        <div class="data-row">
                            <span class="field-label">End Time:</span>
                            <input type="time" class="field-value editable-field" data-field="endTime" data-section="workingHours" value="${workingHours.endTime || '17:00:00'}">
                        </div>
                        <div class="data-row">
                            <span class="field-label">Days:</span>
                            <span class="field-value">${(workingHours.daysOfWeek || ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday']).join(', ')}</span>
                        </div>
                    </div>

                    <!-- Presence Status -->
                    <div class="user-section">
                        <h5><i class="fas fa-signal"></i> Presence</h5>
                        <div class="data-row">
                            <span class="field-label">Availability:</span>
                            <select class="field-value editable-select" data-field="availability" data-section="presence">
                                <option value="Available" ${presence.availability === 'Available' ? 'selected' : ''}>Available</option>
                                <option value="Busy" ${presence.availability === 'Busy' ? 'selected' : ''}>Busy</option>
                                <option value="DoNotDisturb" ${presence.availability === 'DoNotDisturb' ? 'selected' : ''}>Do Not Disturb</option>
                                <option value="Away" ${presence.availability === 'Away' ? 'selected' : ''}>Away</option>
                            </select>
                        </div>
                        <div class="data-row">
                            <span class="field-label">Activity:</span>
                            <select class="field-value editable-select" data-field="activity" data-section="presence">
                                <option value="Available" ${presence.activity === 'Available' ? 'selected' : ''}>Available</option>
                                <option value="InACall" ${presence.activity === 'InACall' ? 'selected' : ''}>In a Call</option>
                                <option value="InAMeeting" ${presence.activity === 'InAMeeting' ? 'selected' : ''}>In a Meeting</option>
                                <option value="Busy" ${presence.activity === 'Busy' ? 'selected' : ''}>Busy</option>
                            </select>
                        </div>
                    </div>

                    <!-- Calendar Events -->
                    <div class="user-section">
                        <h5><i class="fas fa-calendar"></i> Calendar Events (${calendar.busySlots.length})</h5>
                        <div class="calendar-events">
                            ${calendar.busySlots.map((slot, slotIndex) => `
                                <div class="busy-slot-unified" data-slot-index="${slotIndex}">
                                    <div class="slot-details">
                                        <div class="slot-time">
                                            <input type="datetime-local" value="${slot.start.slice(0, -1)}" data-field="start" class="editable-field" data-section="calendar">
                                            to
                                            <input type="datetime-local" value="${slot.end.slice(0, -1)}" data-field="end" class="editable-field" data-section="calendar">
                                        </div>
                                        <div class="slot-subject">
                                            <input type="text" value="${slot.subject || 'Meeting'}" data-field="subject" class="editable-field" data-section="calendar" placeholder="Meeting subject">
                                        </div>
                                    </div>
                                    <div class="slot-actions">
                                        <select class="slot-status status-${slot.status?.toLowerCase() || 'busy'}" data-field="status" data-section="calendar">
                                            <option value="Busy" ${slot.status === 'Busy' ? 'selected' : ''}>Busy</option>
                                            <option value="Tentative" ${slot.status === 'Tentative' ? 'selected' : ''}>Tentative</option>
                                        </select>
                                        <button class="btn-small btn-delete" onclick="deleteBusySlotUnified('${user.email}', ${slotIndex})">
                                            <i class="fas fa-trash"></i>
                                        </button>
                                    </div>
                                </div>
                            `).join('')}
                            ${calendar.busySlots.length === 0 ? '<p class="no-events">No calendar events</p>' : ''}
                            <button class="btn-small btn-add" onclick="addBusySlotUnified('${user.email}')">
                                <i class="fas fa-plus"></i> Add Calendar Event
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        `;
    }).join('');
    
    container.innerHTML = html + `
        <div class="add-new-btn" onclick="addNewUser()">
            <i class="fas fa-plus"></i>
            Add New User
        </div>
    `;
}

// Mock data management functions
function toggleUnifiedEdit(button) {
    const card = button.closest('.unified-user-card');
    const editableFields = card.querySelectorAll('.editable-field, .editable-select');
    
    if (button.innerHTML.includes('edit')) {
        // Enable editing
        editableFields.forEach(field => {
            if (field.tagName === 'SELECT') {
                field.disabled = false;
            } else if (field.tagName === 'INPUT') {
                field.disabled = false;
                field.style.background = '#fff9c4';
            } else {
                field.setAttribute('contenteditable', 'true');
                field.style.background = '#fff9c4';
            }
        });
        button.innerHTML = '<i class="fas fa-save"></i>';
        button.classList.remove('btn-edit');
        button.classList.add('btn-save');
    } else {
        // Save changes
        saveUnifiedCardData(card);
        editableFields.forEach(field => {
            if (field.tagName === 'SELECT') {
                field.disabled = true;
            } else if (field.tagName === 'INPUT') {
                field.disabled = true;
                field.style.background = 'transparent';
            } else {
                field.setAttribute('contenteditable', 'false');
                field.style.background = 'transparent';
            }
        });
        button.innerHTML = '<i class="fas fa-edit"></i>';
        button.classList.remove('btn-save');
        button.classList.add('btn-edit');
    }
}

function saveUnifiedCardData(card) {
    const userId = card.getAttribute('data-user-id');
    const user = mockData.userProfiles.find(u => u.id === userId);
    if (!user) return;

    const editableFields = card.querySelectorAll('.editable-field, .editable-select');
    
    editableFields.forEach(field => {
        const fieldName = field.getAttribute('data-field');
        const section = field.getAttribute('data-section');
        const value = field.tagName === 'SELECT' ? field.value : 
                     field.tagName === 'INPUT' ? field.value : 
                     field.textContent.trim();
        
        if (section === 'profile' && fieldName) {
            user[fieldName] = value;
        } else if (section === 'workingHours' && fieldName) {
            let workingHours = mockData.workingHours.find(wh => wh.userEmail === user.email);
            if (!workingHours) {
                workingHours = {
                    userEmail: user.email,
                    timeZone: 'Pacific Standard Time',
                    daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
                    startTime: '09:00:00',
                    endTime: '17:00:00'
                };
                mockData.workingHours.push(workingHours);
            }
            workingHours[fieldName] = value;
        } else if (section === 'presence' && fieldName) {
            let presence = mockData.presenceStatus.find(ps => ps.userEmail === user.email);
            if (!presence) {
                presence = {
                    userEmail: user.email,
                    availability: 'Available',
                    activity: 'Available',
                    lastModified: new Date().toISOString()
                };
                mockData.presenceStatus.push(presence);
            }
            presence[fieldName] = value;
            presence.lastModified = new Date().toISOString();
        }
    });
    
    updateSchedulingForms();
    syncMockDataWithBackend(); // Sync changes with backend
}

function addBusySlotUnified(userEmail) {
    let calendar = mockData.calendarAvailability.find(ca => ca.userEmail === userEmail);
    if (!calendar) {
        calendar = { userEmail: userEmail, busySlots: [] };
        mockData.calendarAvailability.push(calendar);
    }
    
    const now = new Date();
    const endTime = new Date(now.getTime() + 60 * 60 * 1000);
    
    const newSlot = {
        start: now.toISOString(),
        end: endTime.toISOString(),
        status: 'Busy',
        subject: 'New Meeting'
    };
    
    calendar.busySlots.push(newSlot);
    displayUnifiedUserData();
    syncMockDataWithBackend(); // Sync changes with backend
}

function deleteBusySlotUnified(userEmail, slotIndex) {
    if (confirm('Are you sure you want to delete this calendar event?')) {
        const calendar = mockData.calendarAvailability.find(ca => ca.userEmail === userEmail);
        if (calendar && calendar.busySlots[slotIndex]) {
            calendar.busySlots.splice(slotIndex, 1);
            displayUnifiedUserData();
            syncMockDataWithBackend(); // Sync changes with backend
        }
    }
}

function addNewUser() {
    const newUser = {
        id: Date.now().toString(),
        name: 'New User',
        email: 'newuser@company.com',
        jobTitle: 'Software Engineer',
        department: 'Engineering',
        timeZone: 'Pacific Standard Time'
    };
    
    mockData.userProfiles.push(newUser);
    
    // Add corresponding working hours, presence, and calendar data
    mockData.workingHours.push({
        userEmail: newUser.email,
        timeZone: 'Pacific Standard Time',
        daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
        startTime: '09:00:00',
        endTime: '17:00:00'
    });
    
    mockData.presenceStatus.push({
        userEmail: newUser.email,
        availability: 'Available',
        activity: 'Available',
        lastModified: new Date().toISOString()
    });
    
    mockData.calendarAvailability.push({
        userEmail: newUser.email,
        busySlots: []
    });
    
    displayUnifiedUserData();
    updateSchedulingForms();
}

function deleteUser(userId) {
    if (confirm('Are you sure you want to delete this user? This will remove all their data.')) {
        const user = mockData.userProfiles.find(u => u.id === userId);
        if (user) {
            mockData.userProfiles = mockData.userProfiles.filter(u => u.id !== userId);
            mockData.workingHours = mockData.workingHours.filter(wh => wh.userEmail !== user.email);
            mockData.presenceStatus = mockData.presenceStatus.filter(ps => ps.userEmail !== user.email);
            mockData.calendarAvailability = mockData.calendarAvailability.filter(ca => ca.userEmail !== user.email);
            
            displayUnifiedUserData();
            updateSchedulingForms();
        }
    }
}

function regenerateCalendarData() {
    const duration = parseInt(document.getElementById('generation-duration')?.value || 7);
    const density = document.getElementById('generation-density')?.value || 'medium';
    
    const now = new Date();
    const endDate = new Date(now.getTime() + duration * 24 * 60 * 60 * 1000);
    
    mockData.userProfiles.forEach(user => {
        const calendar = mockData.calendarAvailability.find(ca => ca.userEmail === user.email) || 
                        { userEmail: user.email, busySlots: [] };
        
        if (!mockData.calendarAvailability.find(ca => ca.userEmail === user.email)) {
            mockData.calendarAvailability.push(calendar);
        }
        
        calendar.busySlots = [];
        
        let meetingsPerDay;
        switch (density) {
            case 'low': meetingsPerDay = [0, 1]; break;
            case 'high': meetingsPerDay = [3, 5]; break;
            default: meetingsPerDay = [1, 3]; break;
        }
        
        const currentDate = new Date(now);
        while (currentDate <= endDate) {
            if (currentDate.getDay() !== 0 && currentDate.getDay() !== 6) {
                const numMeetings = Math.floor(Math.random() * (meetingsPerDay[1] - meetingsPerDay[0] + 1)) + meetingsPerDay[0];
                
                for (let i = 0; i < numMeetings; i++) {
                    const meetingStart = new Date(currentDate);
                    meetingStart.setHours(Math.floor(Math.random() * 8) + 9);
                    meetingStart.setMinutes(Math.floor(Math.random() * 2) * 30);
                    
                    const meetingDuration = (Math.floor(Math.random() * 3) + 1) * 30;
                    const meetingEnd = new Date(meetingStart.getTime() + meetingDuration * 60 * 1000);
                    
                    if (meetingEnd.getHours() <= 17) {
                        const subjects = ['Team Meeting', 'Code Review', 'Product Planning', 'Client Call', 
                                        'Sprint Planning', '1:1 Meeting', 'All Hands', 'Training Session'];
                        
                        calendar.busySlots.push({
                            start: meetingStart.toISOString(),
                            end: meetingEnd.toISOString(),
                            status: Math.random() < 0.1 ? 'Tentative' : 'Busy',
                            subject: subjects[Math.floor(Math.random() * subjects.length)]
                        });
                    }
                }
            }
            currentDate.setDate(currentDate.getDate() + 1);
        }
        
        calendar.busySlots.sort((a, b) => new Date(a.start) - new Date(b.start));
    });
    
    displayUnifiedUserData();
    syncMockDataWithBackend(); // Sync changes with backend
    alert(`Calendar events regenerated for ${duration} days with ${density} density.`);
}

function updateSchedulingForms() {
    const emails = mockData.userProfiles.map(user => user.email).join('\n');
    
    const participantFields = [
        document.getElementById('participants'),
        document.getElementById('val-participants'),
        document.getElementById('conflict-participants')
    ];
    
    participantFields.forEach(field => {
        if (field) {
            field.value = emails;
        }
    });
}

function resetMockData() {
    if (confirm('Are you sure you want to reset all mock data to defaults?')) {
        initializeMockData();
        updateSchedulingForms();
        alert('Mock data has been reset to defaults.');
    }
}

function generateRandomData() {
    if (confirm('Are you sure you want to generate random mock data? This will replace current data.')) {
        const randomNames = ['Alice Johnson', 'Bob Smith', 'Carol Davis', 'David Wilson', 'Eva Brown'];
        const randomTitles = ['Software Engineer', 'Product Manager', 'UX Designer', 'Engineering Manager', 'HR Business Partner'];
        const randomDepartments = ['Engineering', 'Product', 'Design', 'Management', 'Human Resources'];
        const timezones = ['Pacific Standard Time', 'Eastern Standard Time', 'GMT Standard Time', 'Central Standard Time'];
        const availabilityStates = ['Available', 'Busy', 'DoNotDisturb', 'Away'];
        const activityStates = ['Available', 'InACall', 'InAMeeting', 'Busy'];
        
        mockData.userProfiles = randomNames.map((name, index) => ({
            id: (index + 1).toString(),
            name: name,
            email: name.toLowerCase().replace(' ', '.') + '@company.com',
            jobTitle: randomTitles[Math.floor(Math.random() * randomTitles.length)],
            department: randomDepartments[Math.floor(Math.random() * randomDepartments.length)],
            timeZone: timezones[Math.floor(Math.random() * timezones.length)]
        }));
        
        mockData.workingHours = mockData.userProfiles.map(user => ({
            userEmail: user.email,
            timeZone: user.timeZone,
            daysOfWeek: ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday'],
            startTime: ['08:00:00', '08:30:00', '09:00:00'][Math.floor(Math.random() * 3)],
            endTime: ['16:30:00', '17:00:00', '17:30:00'][Math.floor(Math.random() * 3)]
        }));
        
        mockData.presenceStatus = mockData.userProfiles.map(user => ({
            userEmail: user.email,
            availability: availabilityStates[Math.floor(Math.random() * availabilityStates.length)],
            activity: activityStates[Math.floor(Math.random() * activityStates.length)],
            lastModified: new Date().toISOString()
        }));
        
        mockData.calendarAvailability = mockData.userProfiles.map(user => ({
            userEmail: user.email,
            busySlots: []
        }));
        
        regenerateCalendarData();
        
        displayUnifiedUserData();
        updateSchedulingForms();
        alert('Random mock data has been generated.');
    }
}

// Function to sync frontend mock data with backend
async function syncMockDataWithBackend() {
    try {
        console.log('Syncing mock data with backend...');
        
        const response = await fetch('/api/scheduling/update-mock-data', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({
                userProfiles: mockData.userProfiles.map(user => ({
                    id: user.id,
                    name: user.name,
                    email: user.email,
                    jobTitle: user.jobTitle,
                    department: user.department,
                    timeZone: user.timeZone
                })),
                workingHours: mockData.workingHours.map(wh => ({
                    userEmail: wh.userEmail,
                    timeZone: wh.timeZone,
                    daysOfWeek: wh.daysOfWeek,
                    startTime: wh.startTime,
                    endTime: wh.endTime
                })),
                presenceStatus: mockData.presenceStatus.map(ps => ({
                    userEmail: ps.userEmail,
                    availability: ps.availability,
                    activity: ps.activity,
                    lastModified: ps.lastModified
                })),
                calendarAvailability: mockData.calendarAvailability.map(ca => ({
                    userEmail: ca.userEmail,
                    busySlots: ca.busySlots.map(slot => ({
                        start: slot.start,
                        end: slot.end,
                        status: slot.status,
                        subject: slot.subject
                    }))
                }))
            })
        });
        
        if (response.ok) {
            console.log('Mock data synced successfully with backend');
        } else {
            const error = await response.json();
            console.error('Failed to sync mock data:', error);
        }
    } catch (error) {
        console.error('Error syncing mock data with backend:', error);
    }
}

function exportMockData() {
    const dataStr = JSON.stringify(mockData, null, 2);
    const dataBlob = new Blob([dataStr], { type: 'application/json' });
    const url = URL.createObjectURL(dataBlob);
    
    const link = document.createElement('a');
    link.href = url;
    link.download = 'mock-data.json';
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(url);
}

// Placeholder functions for other features
function displayValidationResults(result) {
    console.log('Validation results:', result);
}

function displayConflictResults(result) {
    console.log('Conflict results:', result);
}

function displayError(containerId, message) {
    const container = document.getElementById(containerId);
    if (container) {
        container.innerHTML = `
            <div class="status-badge status-error">
                <i class="fas fa-exclamation-triangle"></i>
                ${message}
            </div>
        `;
        container.style.display = 'block';
        container.scrollIntoView({ behavior: 'smooth' });
    }
}