using CIS.Application.Abstractions.Authentication;

namespace CIS_WebAPI.Security
{
    public sealed class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string? UserId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;

                if (user?.Identity?.IsAuthenticated != true)
                {
                    return null;
                }

                var userId = user.FindFirst("sub")?.Value;

                if (!Guid.TryParseExact(userId, "D", out var parsedId))
                {
                    return null;
                }

                return parsedId.ToString("D");
            }
        }
    }
}
