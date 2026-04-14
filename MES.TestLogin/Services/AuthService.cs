using MES.TestLogin.Models;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace MES.TestLogin.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginModel loginModel);
        Task LogoutAsync();
        Task<bool> IsAuthenticatedAsync();
        Task<string?> GetTokenAsync();
    }

    public class AuthService : IAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IJSRuntime _jsRuntime;

        public AuthService(HttpClient httpClient, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _jsRuntime = jsRuntime;
        }

        public async Task<LoginResponse?> LoginAsync(LoginModel loginModel)
        {
            try
            {
                Console.WriteLine("Attempting login with email: " + loginModel.Email);
                var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginModel);
                
                Console.WriteLine("Login response status: " + response.StatusCode);
                
                if (response.IsSuccessStatusCode)
                {
                    var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>();
                    
                    if (apiResponse?.Success == true && !string.IsNullOrEmpty(apiResponse.Data?.Token))
                    {
                        Console.WriteLine("Login successful, token received");
                        await _jsRuntime.InvokeVoidAsync("localStorage.setItem", "authToken", apiResponse.Data.Token);
                        return apiResponse.Data;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine("Login failed, error: " + errorContent);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return null;
            }
        }

        public async Task LogoutAsync()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", "authToken");
        }

        public async Task<bool> IsAuthenticatedAsync()
        {
            var token = await GetTokenAsync();
            return !string.IsNullOrEmpty(token);
        }

        public async Task<string?> GetTokenAsync()
        {
            try
            {
                return await _jsRuntime.InvokeAsync<string>("localStorage.getItem", "authToken");
            }
            catch
            {
                return null;
            }
        }
    }
}