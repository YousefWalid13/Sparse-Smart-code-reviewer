using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sparse_Smart_code_reviewer.Data;
using Sparse_Smart_code_reviewer.Models;
using Sparse_Smart_code_reviewer.Services.interfaces;
using Sparse_Smart_code_reviewer.Services.services;
using System.Text;

namespace Sparse_Smart_code_reviewer;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // =========================================================
        // Controllers
        // =========================================================

        builder.Services.AddControllers();

        // =========================================================
        // Database
        // =========================================================

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("DefaultConnection")
            ));

        // =========================================================
        // Services
        // =========================================================

        builder.Services.AddScoped<ICodeService, CodeService>();
        builder.Services.AddScoped<IAuthService, AuthService>();

        builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

        // =========================================================
        // Authentication
        // =========================================================

        builder.Services
            .AddAuthentication(options =>
            {
                // JWT is the default authentication mechanism
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;

                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })

            // =====================================================
            // JWT Authentication
            // =====================================================

            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ValidIssuer =
                            builder.Configuration["Jwt:Issuer"],

                        ValidAudience =
                            builder.Configuration["Jwt:Audience"],

                        IssuerSigningKey =
                            new SymmetricSecurityKey(
                                Encoding.UTF8.GetBytes(
                                    builder.Configuration["Jwt:Key"]!
                                )
                            ),

                        ClockSkew = TimeSpan.Zero
                    };
            })

            // =====================================================
            // Google Authentication
            // =====================================================

            .AddGoogle(options =>
            {
                options.ClientId =
                    builder.Configuration[
                        "Authentication:Google:ClientId"
                    ]!;

                options.ClientSecret =
                    builder.Configuration[
                        "Authentication:Google:ClientSecret"
                    ]!;
            })

            // =====================================================
            // GitHub Authentication
            // =====================================================

            .AddOAuth("GitHub", options =>
            {
                options.ClientId =
                    builder.Configuration[
                        "Authentication:GitHub:ClientId"
                    ]!;

                options.ClientSecret =
                    builder.Configuration[
                        "Authentication:GitHub:ClientSecret"
                    ]!;

                // GitHub callback URL
                options.CallbackPath = "/signin-github";

                // GitHub OAuth endpoints
                options.AuthorizationEndpoint =
                    "https://github.com/login/oauth/authorize";

                options.TokenEndpoint =
                    "https://github.com/login/oauth/access_token";

                options.UserInformationEndpoint =
                    "https://api.github.com/user";

                // Request user's email
                options.Scope.Add("user:email");

                // Save OAuth access token
                options.SaveTokens = true;
            });

        // =========================================================
        // Authorization
        // =========================================================

        builder.Services.AddAuthorization();

        // =========================================================
        // Swagger
        // =========================================================

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // =========================================================
        // Build Application
        // =========================================================

        var app = builder.Build();

        // =========================================================
        // Swagger Middleware
        // =========================================================

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        // =========================================================
        // HTTPS
        // =========================================================

        app.UseHttpsRedirection();

        // =========================================================
        // Authentication & Authorization
        // =========================================================

        app.UseAuthentication();
        app.UseAuthorization();

        // =========================================================
        // Controllers
        // =========================================================

        app.MapControllers();

        // =========================================================
        // Run
        // =========================================================

        app.Run();
    }
}