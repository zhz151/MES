using MES.Core.DTOs.Auth;

namespace MES.Core.Interfaces.Auth;

public interface IJwtService
{
    Task<string> GenerateTokenAsync(JwtGenerationRequest request);
    string GetUsernameFromToken(string token);
    bool ValidateToken(string token);
}
