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
    
    // Show the first tab by default
    document.querySelector('.tab-button').click();
});