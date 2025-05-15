using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApi.Data.Context;
using WebApi.Data.Entities;
using WebApi.Interfaces;
using WebApi.Repositories;
using WebApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=C:\Users\Emanuel\source\repos\backends-microservices\AuthService\WebApi\Data\Databases\AuthDb.mdf;Integrated Security=True;Connect Timeout=30");
});

builder.Services.AddIdentity<AppUserEntity, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Bearer-Token");
    });
});

builder.Services.AddScoped<IAppUserRepository, AppUserRepository>();   
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();   
builder.Services.AddScoped<IRefreshTokenFamilyRepository, RefrehTokenFamilyRepository>();
builder.Services.AddTransient<TokenService>();
builder.Services.AddScoped<AuthService>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
