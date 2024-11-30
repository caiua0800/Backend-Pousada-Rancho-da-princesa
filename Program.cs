using Microsoft.Extensions.Options;
using MongoDB.Driver;
using DotnetBackend.Models;
using DotnetBackend.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DotnetBackend.Queries;

var builder = WebApplication.CreateBuilder(args);

// Configurações do MongoDB
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDB"));

builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<MongoDBSettings>>().Value;
    return new MongoClient(settings.ConnectionString);
});

builder.Services.AddSingleton<MongoDbService>();

// Configuração do CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", builder =>
        builder.WithOrigins("https://adminoscar.modelodesoftwae.com") // Permita apenas o domínio específico
               .AllowAnyMethod()  // Permita todos os métodos (GET, POST, etc.)
               .AllowAnyHeader()); // Permita todos os cabeçalhos
});

builder.Services.AddScoped<VendaService>();
builder.Services.AddScoped<ClientService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddScoped<WithdrawalService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<ClientQueries>();
builder.Services.AddScoped<SystemConfigService>();
builder.Services.AddScoped<CounterService>();
builder.Services.AddScoped<ExtractService>();
builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<WebSocketHandler>();

builder.Services.AddControllers();

var key = builder.Configuration["Jwt:Key"] ?? "default-key";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateIssuerSigningKey = true,
    };
});

// Configuração do Swagger
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configurações para ambiente de desenvolvimento
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DotnetBackend API V1");
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAllOrigins"); // Use a política específica de CORS
app.UseAuthentication();
app.UseAuthorization();

// Configuração para WebSocket
app.UseWebSockets();
app.Use(async (context, next) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var webSocketHandler = context.RequestServices.GetRequiredService<WebSocketHandler>();
        await webSocketHandler.HandleWebSocketAsync(webSocket);
    }
    else
    {
        await next();
    }
});

app.MapControllers();
app.Run();