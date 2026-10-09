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
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Seat Reservation API", Version = "v1" });
    // Swagger'daki "Authorize" düğmesi: /api/auth/login'den alınan token'ı yapıştırınca korumalı uçlar denenebilir.
    c.AddSecurityDefinition("Bearer", new()
    {
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Giriş yanıtındaki token'ı yapıştır (\"Bearer\" öneki gerekmez).",
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        },
    });
});

// Zaman her yerde TimeProvider üzerinden okunur; testler sahte saat enjekte edip süre dolumunu bekletmeden sınar.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddApplication();
builder.Services.Configure<SeatReservation.Application.Reservations.ReservationOptions>(
    builder.Configuration.GetSection(SeatReservation.Application.Reservations.ReservationOptions.SectionName));
builder.Services.Configure<SeatReservation.Application.Demo.DemoOptions>(
    builder.Configuration.GetSection(SeatReservation.Application.Demo.DemoOptions.SectionName));
builder.Services.AddInfrastructure();

builder.Services.AddHostedService<SeatReservation.Api.BackgroundServices.HoldExpirationWorker>();

// CORS yalnızca Cors__AllowedOrigins (virgülle ayrılmış) doluysa açılır; boşsa tarayıcı başka origin'den çağıramaz.
// Yerel geliştirmede Vite proxy'si aynı origin gibi davrandığı için gerekmez; canlıda arayüz ayrı alan adındadır.
// Kimlik bilgisi çerezle değil Authorization başlığıyla taşındığı için AllowCredentials bilerek yok.
// Tembel okunur (JWT ayarıyla aynı sebep: test ayarları eager okumada görünmez).
builder.Services.AddCors();
builder.Services.AddOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>()
    .Configure<IConfiguration>((cors, config) =>
    {
        var origins = (config["Cors:AllowedOrigins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (origins.Length == 0) return;
        // Date açıkça açılır: arayüzün geri sayımı sunucu saatine göre düzeltmesi için başlığı okuyabilmesi gerekir
        // (tarayıcı varsayılan olarak çapraz origin yanıtlarda yalnızca "güvenli" başlıkları gösterir, Date bunlardan değil).
        cors.AddDefaultPolicy(p => p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("Date"));
    });

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

// Swagger her ortamda açık: bu proje bir vitrin, `docker compose up` (Production ortamı) sonrası
// ilk bakılacak yer burası. Gerçek bir ürün olsaydı yalnızca Development'ta açardık.
app.UseSwagger();
app.UseSwaggerUI();

// HTTPS yönlendirmesi yok: TLS'i API'nin önündeki proxy/ingress sonlandırır, konteyner yalnızca HTTP dinler.
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await DatabaseInitializer.InitializeAsync(app.Services);

app.Run();

// WebApplicationFactory<Program> için gerekli.
public partial class Program;
