using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

namespace WEB_AUTH_API.DataAccess
{
    public class Startup
    {
        public static string connectionString;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            connectionString = Configuration.GetConnectionString("DefaultConnection");
            var jwtSettings = Configuration.GetSection("Jwt");
            var key = jwtSettings["Key"];

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer("Keycloak", options =>
                {
                    options.Authority = "http://localhost:9090/realms/DTI";
                    options.Audience = "web-security-backend";
                    options.RequireHttpsMetadata = false; // Disable in development                      // Use HTTPS in production
                })
            .AddJwtBearer("CustomJWT", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                };
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("KeycloakPolicy", policy =>
                    policy.RequireAuthenticatedUser().AddAuthenticationSchemes("Keycloak"));

                options.AddPolicy("CustomJWTPolicy", policy =>
                    policy.RequireAuthenticatedUser().AddAuthenticationSchemes("CustomJWT"));

                options.AddPolicy("CombinedPolicy", policy =>
                    policy.RequireAuthenticatedUser()
                          .AddAuthenticationSchemes("Keycloak", "CustomJWT"));
            });

            services.AddAuthorization(options =>
            {
                options.AddPolicy("AdminPolicy", policy => policy.RequireRole("Admin"));
                options.AddPolicy("UserPolicy", policy => policy.RequireRole("User"));
            });

            services.AddControllers();
            services.AddSwaggerGen();
            services.AddMvc();
            services.AddMvc().AddNewtonsoftJson();

            // Configure Swagger to include JWT Authentication
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter 'Bearer' [space] and then your token in the text input below. Example: 'Bearer 12345abcdef'",
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        new string[] {}
                    }
                });
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            });

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
