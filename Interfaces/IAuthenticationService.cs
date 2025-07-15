namespace InterviewSchedulingBot.Interfaces
{
    public interface IAuthenticationService
    {
        Task<string?> GetAccessTokenAsync(string userId);
        Task StoreTokenAsync(string userId, string accessToken, string? refreshToken = null, DateTimeOffset? expiresOn = null);
        Task<bool> IsUserAuthenticatedAsync(string userId);
        Task ClearTokenAsync(string userId);
        string GetAuthorizationUrl(string userId, string conversationId);
    }
}