using FactoryPulse.Application.Common;

namespace FactoryPulse.Application.Interfaces;

public interface IJwtTokenGenerator
{
    AccessToken GenerateAccessToken(string userId, string email, IEnumerable<string> roles);
}
