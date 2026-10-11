using CatalogStore.BackendAPI.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace CatalogStore.BackendAPI.Services.Auth
{
    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly IConfiguration _configuration;
        public JwtTokenService(IConfiguration configuration)
        {
            this._configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }
        public string GenerateToken(ApplicationUser usuario, IList<string> roles)
        {
            if (usuario.Email == null)
                throw new ArgumentNullException(nameof(usuario), "El usuario no puede estar sin email.");
            
            if (roles == null ) 
                throw new ArgumentNullException(nameof(roles), "La lista de roles no puede estar vacía.");
            
            if (roles.Count == 0)
                throw new ArgumentException("La lista de roles no puede estar vacía.", nameof(roles));
            
            List<Claim> claims = new List<Claim>();

            claims.Add(new Claim(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()));
            claims.Add(new Claim(JwtRegisteredClaimNames.UniqueName, usuario.Email));
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            foreach (string role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(_configuration["Jwt:ExpireMinutes"]!)),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public Guid? GetUserIdFromExpiredToken(string token)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidAudience = _configuration["Jwt:Audience"],
                IssuerSigningKey = key
            };

            try
            {
                // MapInboundClaims = false: por defecto el handler renombra "sub" a ClaimTypes.NameIdentifier y el
                // FindFirst("sub") de abajo devolvía null, así que la renovación del token respondía siempre 401.
                var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
                var principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
                if (validatedToken is not JwtSecurityToken jwt || !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
                    return null;

                // Un token vencido solo se puede renovar durante un tiempo; pasado ese límite hay que volver a
                // iniciar sesión. Sin este tope, un token viejo (o robado) generaría tokens nuevos para siempre.
                var maxAge = TimeSpan.FromHours(_configuration.GetValue<int?>("Jwt:MaxRefreshAgeHours") ?? 8);
                if (DateTime.UtcNow - jwt.ValidTo > maxAge)
                    return null;

                var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                return Guid.TryParse(sub, out var userId) ? userId : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
