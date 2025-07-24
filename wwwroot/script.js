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
        displaySchedulingResults(result);
    } catch (error) {
        displayError('scheduling-results', 'Failed to find optimal slots: ' + error.message);
    }
});

// Display scheduling results
function displaySchedulingResults(result) {
    const resultsDiv = document.getElementById('scheduling-results');
    const recommendedDiv = document.getElementById('recommended-slots');
    const alternativeDiv = document.getElementById('alternative-slots');
    const insightsDiv = document.getElementById('business-insights');
    
    // Show recommended slots
    if (result.recommendedSlots && result.recommendedSlots.length > 0) {
        recommendedDiv.innerHTML = result.recommendedSlots.map(slot => createTimeSlotHtml(slot)).join('');
    } else {
        recommendedDiv.innerHTML = '<p class="info-text"><i class="fas fa-info-circle"></i> No recommended slots found for the specified criteria.</p>';
    }
    
    // Show alternative slots
    if (result.alternativeSlots && result.alternativeSlots.length > 0) {
        alternativeDiv.innerHTML = result.alternativeSlots.map(slot => createTimeSlotHtml(slot)).join('');
    } else {
        alternativeDiv.innerHTML = '<p class="info-text"><i class="fas fa-info-circle"></i> No alternative slots available.</p>';
    }
    
    // Show business insights
    if (result.insights) {
        insightsDiv.innerHTML = createBusinessInsightsHtml(result.insights);
    }
    
    // Show reasoning if available
    if (result.recommendationReasoning) {
        const reasoningDiv = document.createElement('div');
        reasoningDiv.className = 'insight-card';
        reasoningDiv.innerHTML = `
            <h4><i class="fas fa-brain"></i> Recommendation Reasoning</h4>
            <p>${result.recommendationReasoning}</p>
        `;
        insightsDiv.appendChild(reasoningDiv);
    }
    
    resultsDiv.style.display = 'block';
    resultsDiv.scrollIntoView({ behavior: 'smooth' });
}

// Create time slot HTML
function createTimeSlotHtml(slot) {
    const startTime = new Date(slot.startTime).toLocaleString();
    const endTime = new Date(slot.endTime).toLocaleString();
    const businessScore = (slot.businessScore * 100).toFixed(1);
    const confidence = (slot.confidence * 100).toFixed(1);
    
    const reasonsHtml = slot.reasons && slot.reasons.length > 0 
        ? `<div class="slot-reasons">
             <strong>Reasons:</strong>
             <ul>${slot.reasons.map(reason => `<li>${reason}</li>`).join('')}</ul>
           </div>`
        : '';
    
    return `
        <div class="time-slot">
            <div class="slot-time">
                <i class="fas fa-clock"></i> ${startTime} - ${endTime}
            </div>
            <div class="slot-score">
                <span class="score-badge">Score: ${businessScore}%</span>
                <span class="confidence-badge">Confidence: ${confidence}%</span>
            </div>
            ${reasonsHtml}
        </div>
    `;
}

// Create business insights HTML
function createBusinessInsightsHtml(insights) {
    const avgAvailability = (insights.averageAvailability * 100).toFixed(1);
    
    return `
        <div class="insights-grid">
            <div class="insight-card">
                <h4><i class="fas fa-chart-line"></i> Availability Overview</h4>
                <p>Average Availability: <strong>${avgAvailability}%</strong></p>
            </div>
            
            ${insights.bestTimeWindows && insights.bestTimeWindows.length > 0 ? `
            <div class="insight-card">
                <h4><i class="fas fa-clock"></i> Best Time Windows</h4>
                <ul>
                    ${insights.bestTimeWindows.map(window => `<li>${window}</li>`).join('')}
                </ul>
            </div>
            ` : ''}
            
            ${insights.challengingPeriods && insights.challengingPeriods.length > 0 ? `
            <div class="insight-card">
                <h4><i class="fas fa-exclamation-triangle"></i> Challenging Periods</h4>
                <ul>
                    ${insights.challengingPeriods.map(period => `<li>${period}</li>`).join('')}
                </ul>
            </div>
            ` : ''}
            
            ${insights.schedulingTips && insights.schedulingTips.length > 0 ? `
            <div class="insight-card">
                <h4><i class="fas fa-lightbulb"></i> Scheduling Tips</h4>
                <ul>
                    ${insights.schedulingTips.map(tip => `<li>${tip}</li>`).join('')}
                </ul>
            </div>
            ` : ''}
        </div>
    `;
}

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
        displayError('validation-results', 'Validation failed: ' + error.message);
    }
});

// Display validation results
function displayValidationResults(result) {
    const resultsDiv = document.getElementById('validation-results');
    const statusDiv = document.getElementById('validation-status');
    const errorsDiv = document.getElementById('validation-errors');
    const warningsDiv = document.getElementById('validation-warnings');
    const suggestionsDiv = document.getElementById('validation-suggestions');
    
    // Clear previous results
    statusDiv.innerHTML = '';
    errorsDiv.innerHTML = '';
    warningsDiv.innerHTML = '';
    suggestionsDiv.innerHTML = '';
    
    // Show validation status
    if (result.isValid) {
        statusDiv.innerHTML = `
            <div class="status-badge status-success">
                <i class="fas fa-check-circle"></i>
                Validation Successful - Request is valid
            </div>
        `;
    } else {
        statusDiv.innerHTML = `
            <div class="status-badge status-error">
                <i class="fas fa-times-circle"></i>
                Validation Failed - Please fix the errors below
            </div>
        `;
    }
    
    // Show errors
    if (result.errors && result.errors.length > 0) {
        errorsDiv.innerHTML = `
            <h4><i class="fas fa-times-circle"></i> Errors</h4>
            ${result.errors.map(error => `
                <div class="error-item">
                    <i class="fas fa-times"></i>
                    <div>
                        <strong>${error.code}:</strong> ${error.message}
                        ${error.field ? `<br><small>Field: ${error.field}</small>` : ''}
                    </div>
                </div>
            `).join('')}
        `;
    }
    
    // Show warnings
    if (result.warnings && result.warnings.length > 0) {
        warningsDiv.innerHTML = `
            <h4><i class="fas fa-exclamation-triangle"></i> Warnings</h4>
            ${result.warnings.map(warning => `
                <div class="warning-item">
                    <i class="fas fa-exclamation-triangle"></i>
                    <div>
                        <strong>${warning.code}:</strong> ${warning.message}
                        ${warning.suggestion ? `<br><small>Suggestion: ${warning.suggestion}</small>` : ''}
                        ${warning.field ? `<br><small>Field: ${warning.field}</small>` : ''}
                    </div>
                </div>
            `).join('')}
        `;
    }
    
    // Show suggestions
    if (result.suggestions && result.suggestions.length > 0) {
        suggestionsDiv.innerHTML = `
            <h4><i class="fas fa-lightbulb"></i> Suggestions</h4>
            <ul>
                ${result.suggestions.map(suggestion => `<li>${suggestion}</li>`).join('')}
            </ul>
        `;
    }
    
    resultsDiv.style.display = 'block';
    resultsDiv.scrollIntoView({ behavior: 'smooth' });
}

// Conflict Analysis functionality
document.getElementById('conflict-form').addEventListener('submit', async function(e) {
    e.preventDefault();
    
    const participants = parseEmailList(document.getElementById('conflict-participants').value);
    const proposedTime = new Date(document.getElementById('proposed-time').value);
    const duration = parseInt(document.getElementById('conflict-duration').value);
    const accessToken = document.getElementById('access-token').value;
    
    const requestData = {
        participantEmails: participants,
        proposedTime: proposedTime.toISOString(),
        durationMinutes: duration,
        accessToken: accessToken
    };
    
    try {
        const result = await makeApiCall('analyze-conflicts', requestData);
        displayConflictResults(result);
    } catch (error) {
        displayError('conflict-results', 'Conflict analysis failed: ' + error.message);
    }
});

// Display conflict analysis results
function displayConflictResults(result) {
    const resultsDiv = document.getElementById('conflict-results');
    const statusDiv = document.getElementById('conflict-status');
    const detailsDiv = document.getElementById('conflict-details');
    
    // Clear previous results
    statusDiv.innerHTML = '';
    detailsDiv.innerHTML = '';
    
    // Show conflict status
    if (result.hasConflicts) {
        statusDiv.innerHTML = `
            <div class="status-badge status-error">
                <i class="fas fa-exclamation-triangle"></i>
                Conflicts Detected - ${result.impactLevel} Impact
            </div>
        `;
    } else {
        statusDiv.innerHTML = `
            <div class="status-badge status-success">
                <i class="fas fa-check-circle"></i>
                No Conflicts Found
            </div>
        `;
    }
    
    // Show impact description
    if (result.impactDescription) {
        detailsDiv.innerHTML += `
            <div class="insight-card">
                <h4><i class="fas fa-info-circle"></i> Impact Analysis</h4>
                <p>${result.impactDescription}</p>
            </div>
        `;
    }
    
    // Show affected participants
    if (result.affectedParticipants && result.affectedParticipants.length > 0) {
        detailsDiv.innerHTML += `
            <div class="insight-card">
                <h4><i class="fas fa-users"></i> Affected Participants</h4>
                <ul>
                    ${result.affectedParticipants.map(participant => `<li>${participant}</li>`).join('')}
                </ul>
            </div>
        `;
    }
    
    // Show mitigation suggestions
    if (result.mitigationSuggestions && result.mitigationSuggestions.length > 0) {
        detailsDiv.innerHTML += `
            <div class="insight-card">
                <h4><i class="fas fa-lightbulb"></i> Mitigation Suggestions</h4>
                <ul>
                    ${result.mitigationSuggestions.map(suggestion => `<li>${suggestion}</li>`).join('')}
                </ul>
            </div>
        `;
    }
    
    // Show detailed conflicts
    if (result.conflicts && result.conflicts.length > 0) {
        const conflictsHtml = result.conflicts.map(conflict => `
            <div class="conflict-card">
                <div class="conflict-header">
                    <strong>${conflict.participantEmail}</strong>
                    <span class="severity-badge severity-${conflict.severity.toLowerCase()}">
                        ${conflict.severity}
                    </span>
                </div>
                <p><strong>Type:</strong> ${conflict.type}</p>
                <p><strong>Time:</strong> ${new Date(conflict.conflictStart).toLocaleString()} - ${new Date(conflict.conflictEnd).toLocaleString()}</p>
                ${conflict.description ? `<p><strong>Description:</strong> ${conflict.description}</p>` : ''}
                <p><strong>Can be resolved:</strong> ${conflict.canBeResolved ? 'Yes' : 'No'}</p>
            </div>
        `).join('');
        
        detailsDiv.innerHTML += `
            <div class="insight-card">
                <h4><i class="fas fa-exclamation-triangle"></i> Detailed Conflicts</h4>
                ${conflictsHtml}
            </div>
        `;
    }
    
    resultsDiv.style.display = 'block';
    resultsDiv.scrollIntoView({ behavior: 'smooth' });
}

// Display error message
function displayError(containerId, message) {
    const container = document.getElementById(containerId);
    container.innerHTML = `
        <div class="status-badge status-error">
            <i class="fas fa-exclamation-triangle"></i>
            ${message}
        </div>
    `;
    container.style.display = 'block';
    container.scrollIntoView({ behavior: 'smooth' });
}

// Initialize the application
document.addEventListener('DOMContentLoaded', function() {
    initializeDates();
    initializeMockData();
    
    // Show the first tab by default
    document.querySelector('.tab-button').click();
});

// Mock Data Management
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
    displayUserProfiles();
    displayWorkingHours();
    displayPresenceStatus();
    displayCalendarAvailability();
}

// Display user profiles
function displayUserProfiles() {
    const container = document.getElementById('user-profiles');
    const html = mockData.userProfiles.map(user => `
        <div class="user-profile-card" data-user-id="${user.id}">
            <div class="card-header">
                <div class="card-title">
                    <i class="fas fa-user"></i>
                    <span class="editable-field" contenteditable="true" data-field="name">${user.name}</span>
                </div>
                <div class="card-actions">
                    <button class="btn-small btn-edit" onclick="toggleEdit(this)">
                        <i class="fas fa-edit"></i>
                    </button>
                    <button class="btn-small btn-delete" onclick="deleteUser('${user.id}')">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
            <div class="card-content">
                <div class="data-field">
                    <span class="field-label">Email:</span>
                    <span class="field-value editable-field" contenteditable="true" data-field="email">${user.email}</span>
                </div>
                <div class="data-field">
                    <span class="field-label">Job Title:</span>
                    <span class="field-value editable-field" contenteditable="true" data-field="jobTitle">${user.jobTitle}</span>
                </div>
                <div class="data-field">
                    <span class="field-label">Department:</span>
                    <span class="field-value editable-field" contenteditable="true" data-field="department">${user.department}</span>
                </div>
                <div class="data-field">
                    <span class="field-label">Time Zone:</span>
                    <select class="field-value editable-select" data-field="timeZone">
                        <option value="Pacific Standard Time" ${user.timeZone === 'Pacific Standard Time' ? 'selected' : ''}>Pacific Standard Time</option>
                        <option value="Eastern Standard Time" ${user.timeZone === 'Eastern Standard Time' ? 'selected' : ''}>Eastern Standard Time</option>
                        <option value="GMT Standard Time" ${user.timeZone === 'GMT Standard Time' ? 'selected' : ''}>GMT Standard Time</option>
                        <option value="Central Standard Time" ${user.timeZone === 'Central Standard Time' ? 'selected' : ''}>Central Standard Time</option>
                    </select>
                </div>
            </div>
        </div>
    `).join('');
    
    container.innerHTML = html + `
        <div class="add-new-btn" onclick="addNewUser()">
            <i class="fas fa-plus"></i>
            Add New User
        </div>
    `;
}

// Display working hours
function displayWorkingHours() {
    const container = document.getElementById('working-hours');
    const html = mockData.workingHours.map((hours, index) => `
        <div class="working-hours-card" data-hours-index="${index}">
            <div class="card-header">
                <div class="card-title">
                    <i class="fas fa-clock"></i>
                    ${hours.userEmail}
                </div>
                <div class="card-actions">
                    <button class="btn-small btn-edit" onclick="toggleEdit(this)">
                        <i class="fas fa-edit"></i>
                    </button>
                    <button class="btn-small btn-delete" onclick="deleteWorkingHours(${index})">
                        <i class="fas fa-trash"></i>
                    </button>
                </div>
            </div>
            <div class="card-content">
                <div class="data-field">
                    <span class="field-label">Time Zone:</span>
                    <select class="field-value editable-select" data-field="timeZone">
                        <option value="Pacific Standard Time" ${hours.timeZone === 'Pacific Standard Time' ? 'selected' : ''}>Pacific Standard Time</option>
                        <option value="Eastern Standard Time" ${hours.timeZone === 'Eastern Standard Time' ? 'selected' : ''}>Eastern Standard Time</option>
                        <option value="GMT Standard Time" ${hours.timeZone === 'GMT Standard Time' ? 'selected' : ''}>GMT Standard Time</option>
                        <option value="Central Standard Time" ${hours.timeZone === 'Central Standard Time' ? 'selected' : ''}>Central Standard Time</option>
                    </select>
                </div>
                <div class="data-field">
                    <span class="field-label">Start Time:</span>
                    <input type="time" class="field-value editable-field" data-field="startTime" value="${hours.startTime}">
                </div>
                <div class="data-field">
                    <span class="field-label">End Time:</span>
                    <input type="time" class="field-value editable-field" data-field="endTime" value="${hours.endTime}">
                </div>
                <div class="data-field">
                    <span class="field-label">Days:</span>
                    <span class="field-value">${hours.daysOfWeek.join(', ')}</span>
                </div>
            </div>
        </div>
    `).join('');
    
    container.innerHTML = html;
}

// Display presence status
function displayPresenceStatus() {
    const container = document.getElementById('presence-status');
    const html = mockData.presenceStatus.map((presence, index) => `
        <div class="presence-card" data-presence-index="${index}">
            <div class="card-header">
                <div class="card-title">
                    <i class="fas fa-signal"></i>
                    ${presence.userEmail}
                </div>
                <div class="card-actions">
                    <button class="btn-small btn-edit" onclick="toggleEdit(this)">
                        <i class="fas fa-edit"></i>
                    </button>
                </div>
            </div>
            <div class="card-content">
                <div class="data-field">
                    <span class="field-label">Availability:</span>
                    <select class="field-value editable-select" data-field="availability">
                        <option value="Available" ${presence.availability === 'Available' ? 'selected' : ''}>Available</option>
                        <option value="Busy" ${presence.availability === 'Busy' ? 'selected' : ''}>Busy</option>
                        <option value="DoNotDisturb" ${presence.availability === 'DoNotDisturb' ? 'selected' : ''}>Do Not Disturb</option>
                        <option value="Away" ${presence.availability === 'Away' ? 'selected' : ''}>Away</option>
                        <option value="BeRightBack" ${presence.availability === 'BeRightBack' ? 'selected' : ''}>Be Right Back</option>
                    </select>
                </div>
                <div class="data-field">
                    <span class="field-label">Activity:</span>
                    <select class="field-value editable-select" data-field="activity">
                        <option value="Available" ${presence.activity === 'Available' ? 'selected' : ''}>Available</option>
                        <option value="InACall" ${presence.activity === 'InACall' ? 'selected' : ''}>In a Call</option>
                        <option value="InAMeeting" ${presence.activity === 'InAMeeting' ? 'selected' : ''}>In a Meeting</option>
                        <option value="Busy" ${presence.activity === 'Busy' ? 'selected' : ''}>Busy</option>
                        <option value="Away" ${presence.activity === 'Away' ? 'selected' : ''}>Away</option>
                    </select>
                </div>
                <div class="data-field">
                    <span class="field-label">Last Modified:</span>
                    <span class="field-value">${new Date(presence.lastModified).toLocaleString()}</span>
                </div>
            </div>
        </div>
    `).join('');
    
    container.innerHTML = html;
}

// Display calendar availability
function displayCalendarAvailability() {
    const container = document.getElementById('calendar-availability');
    const html = mockData.calendarAvailability.map((calendar, index) => `
        <div class="calendar-card" data-calendar-index="${index}">
            <div class="card-header">
                <div class="card-title">
                    <i class="fas fa-calendar"></i>
                    ${calendar.userEmail}
                </div>
                <div class="card-actions">
                    <button class="btn-small btn-edit" onclick="addBusySlot(${index})">
                        <i class="fas fa-plus"></i> Add Slot
                    </button>
                </div>
            </div>
            <div class="card-content">
                ${calendar.busySlots.map((slot, slotIndex) => `
                    <div class="busy-slot" data-slot-index="${slotIndex}">
                        <div>
                            <div class="slot-time">
                                <input type="datetime-local" value="${slot.start.slice(0, -1)}" data-field="start" class="editable-field">
                                to
                                <input type="datetime-local" value="${slot.end.slice(0, -1)}" data-field="end" class="editable-field">
                            </div>
                            <div style="margin-top: 5px;">
                                <input type="text" value="${slot.subject}" data-field="subject" class="editable-field" placeholder="Meeting subject">
                            </div>
                        </div>
                        <div>
                            <select class="slot-status status-${slot.status.toLowerCase()}" data-field="status">
                                <option value="Busy" ${slot.status === 'Busy' ? 'selected' : ''}>Busy</option>
                                <option value="Tentative" ${slot.status === 'Tentative' ? 'selected' : ''}>Tentative</option>
                            </select>
                            <button class="btn-small btn-delete" onclick="deleteBusySlot(${index}, ${slotIndex})">
                                <i class="fas fa-trash"></i>
                            </button>
                        </div>
                    </div>
                `).join('')}
                ${calendar.busySlots.length === 0 ? '<p style="color: #6c757d; font-style: italic;">No busy slots</p>' : ''}
            </div>
        </div>
    `).join('');
    
    container.innerHTML = html;
}

// Toggle edit mode for a card
function toggleEdit(button) {
    const card = button.closest('.user-profile-card, .working-hours-card, .presence-card, .calendar-card');
    const editableFields = card.querySelectorAll('.editable-field, .editable-select');
    
    if (button.innerHTML.includes('edit')) {
        // Enable editing
        editableFields.forEach(field => {
            if (field.tagName === 'SELECT') {
                field.disabled = false;
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
        saveCardData(card);
        editableFields.forEach(field => {
            if (field.tagName === 'SELECT') {
                field.disabled = true;
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

// Save card data to mock data
function saveCardData(card) {
    const editableFields = card.querySelectorAll('.editable-field, .editable-select');
    
    if (card.classList.contains('user-profile-card')) {
        const userId = card.getAttribute('data-user-id');
        const user = mockData.userProfiles.find(u => u.id === userId);
        
        editableFields.forEach(field => {
            const fieldName = field.getAttribute('data-field');
            const value = field.tagName === 'SELECT' ? field.value : field.textContent.trim();
            if (user && fieldName) {
                user[fieldName] = value;
            }
        });
    }
    // Add similar logic for other card types...
    
    // Update the display to reflect changes
    updateSchedulingForms();
}

// Add new user
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
    displayUserProfiles();
    updateSchedulingForms();
}

// Delete user
function deleteUser(userId) {
    if (confirm('Are you sure you want to delete this user?')) {
        mockData.userProfiles = mockData.userProfiles.filter(u => u.id !== userId);
        displayUserProfiles();
        updateSchedulingForms();
    }
}

// Add busy slot
function addBusySlot(calendarIndex) {
    const now = new Date();
    const endTime = new Date(now.getTime() + 60 * 60 * 1000); // 1 hour later
    
    const newSlot = {
        start: now.toISOString(),
        end: endTime.toISOString(),
        status: 'Busy',
        subject: 'New Meeting'
    };
    
    mockData.calendarAvailability[calendarIndex].busySlots.push(newSlot);
    displayCalendarAvailability();
}

// Delete busy slot
function deleteBusySlot(calendarIndex, slotIndex) {
    if (confirm('Are you sure you want to delete this busy slot?')) {
        mockData.calendarAvailability[calendarIndex].busySlots.splice(slotIndex, 1);
        displayCalendarAvailability();
    }
}

// Reset mock data to defaults
function resetMockData() {
    if (confirm('Are you sure you want to reset all mock data to defaults?')) {
        // Reset to original mock data
        initializeMockData();
        updateSchedulingForms();
        alert('Mock data has been reset to defaults.');
    }
}

// Generate random mock data
function generateRandomData() {
    if (confirm('Are you sure you want to generate random mock data? This will replace current data.')) {
        // Generate random users
        const randomNames = ['Alice Johnson', 'Bob Smith', 'Carol Davis', 'David Wilson', 'Eva Brown'];
        const randomTitles = ['Software Engineer', 'Product Manager', 'UX Designer', 'Engineering Manager', 'HR Business Partner'];
        const randomDepartments = ['Engineering', 'Product', 'Design', 'Management', 'Human Resources'];
        const timezones = ['Pacific Standard Time', 'Eastern Standard Time', 'GMT Standard Time', 'Central Standard Time'];
        
        mockData.userProfiles = randomNames.map((name, index) => ({
            id: (index + 1).toString(),
            name: name,
            email: name.toLowerCase().replace(' ', '.') + '@company.com',
            jobTitle: randomTitles[Math.floor(Math.random() * randomTitles.length)],
            department: randomDepartments[Math.floor(Math.random() * randomDepartments.length)],
            timeZone: timezones[Math.floor(Math.random() * timezones.length)]
        }));
        
        // Generate random calendar data and other mock data...
        initializeMockData();
        updateSchedulingForms();
        alert('Random mock data has been generated.');
    }
}

// Export mock data
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

// Update scheduling forms with current mock data
function updateSchedulingForms() {
    const emails = mockData.userProfiles.map(user => user.email).join('\n');
    
    // Update participant fields
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

// Listen for changes in mock data fields
document.addEventListener('change', function(e) {
    if (e.target.classList.contains('editable-field') || e.target.classList.contains('editable-select')) {
        const card = e.target.closest('.user-profile-card, .working-hours-card, .presence-card, .calendar-card');
        if (card) {
            saveCardData(card);
        }
    }
});