using Microsoft.AspNetCore.Components.Authorization;
using System.Security.Claims;

namespace MES.TestLogin.Services
{
    public class CustomAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly IAuthService _authService;

        public CustomAuthenticationStateProvider(IAuthService authService)
        {
            _authService = authService;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var token = await _authService.GetTokenAsync();
            
            if (!string.IsNullOrEmpty(token))
            {
                var identity = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "authenticated-user"),
                    new Claim("jwt", token)
                }, "apiauth");

                var user = new ClaimsPrincipal(identity);
                Console.WriteLine("User authenticated with token");
                return new AuthenticationState(user);
            }
            else
            {
                Console.WriteLine("User not authenticated");
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }
        }

        public async Task MarkUserAsAuthenticated(string token)
        {
            await _authService.GetTokenAsync();
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public async Task MarkUserAsLoggedOut()
        {
            await _authService.LogoutAsync();
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}