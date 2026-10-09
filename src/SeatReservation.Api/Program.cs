using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using SeatReservation.Api.Infrastructure;
using SeatReservation.Application;
using SeatReservation.Infrastructure;
using SeatReservation.Infrastructure.Persistence;
using SeatReservation.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Enum'lar JSON'da sayı değil ad olarak gider ("Held"): istemci ve Swagger için okunabilir, sıra değişse bozulmaz.
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Zaman her yerde TimeProvider üzerinden okunur; testler sahte saat enjekte edip süre dolumunu bekletmeden sınar.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.AddInfrastructure();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
// JwtBearerOptions, JwtOptions'tan tembel doldurulur: yapılandırma Program.cs'te eager okunmaz
// (WebApplicationFactory ile eklenen test ayarları .NET 8'de eager okumada görünmez).
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            NameClaimType = "sub",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTPS yönlendirmesi yok: TLS'i API'nin önündeki proxy/ingress sonlandırır, konteyner yalnızca HTTP dinler.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await DatabaseInitializer.InitializeAsync(app.Services);

app.Run();

// WebApplicationFactory<Program> için gerekli.
public partial class Program;
