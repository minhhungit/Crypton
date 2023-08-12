﻿using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Crypton.Application;
using Crypton.Domain;
using Crypton.Infrastructure.Filters;
using Crypton.Infrastructure.Policies;
using Crypton.Infrastructure.Services.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.OpenApi.Models;
using ZymLabs.NSwag.FluentValidation;

namespace Crypton.WebAPI;

/// <summary>
/// Configure services for the web api.
/// </summary>
public static class ConfigureServices
{
    private static readonly string[] CompressionTypes = { "application/octet-stream" };

    /// <summary>
    /// Add web api services.
    /// </summary>
    /// <param name="services">ServiceCollection.</param>
    /// <param name="env">Environment.</param>
    /// <returns>Configured Services.</returns>
    public static IServiceCollection AddWebApiServices(this IServiceCollection services, IWebHostEnvironment env)
    {
        services.AddHttpContextAccessor();

        services.Configure<RouteOptions>(x =>
        {
            x.LowercaseUrls = true;
            x.LowercaseQueryStrings = true;
            x.AppendTrailingSlash = false;
        });

        services
            .AddControllers(o =>
            {
                o.Filters.Add<FluentValidationFilter>();
                o.RespectBrowserAcceptHeader = true;
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = new SnakeCaseNamingPolicy();
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddSignalR(o => { o.EnableDetailedErrors = env.IsDevelopment(); });

        services.AddValidatorsFromAssembly(DomainAssembly.Assembly);
        services.AddValidatorsFromAssembly(ApplicationAssembly.Assembly);

        services.AddFluentValidationAutoValidation()
            .AddFluentValidationClientsideAdapters();

        services.AddScoped(provider =>
        {
            var validationRules = provider.GetService<IEnumerable<FluentValidationRule>>();
            var loggerFactory = provider.GetService<ILoggerFactory>();

            return new FluentValidationSchemaProcessor(provider, validationRules, loggerFactory);
        });

        if (env.IsDevelopment())
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SupportNonNullableReferenceTypes();

                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Crypton.WebAPI",
                    Version = "v1",
                    Description = "Crypton Web API",
                    Contact = new OpenApiContact
                    {
                        Name = "Crypton",
                        Email = "ddjerqq@gmail.com",
                        Url = new Uri("https://github.com/ddjerqq"),
                    },
                });

                c.ResolveConflictingActions(x => x.First());
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "JWT Authorization header using the Bearer scheme.",
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer",
                            },
                        },

                        new string[] { }
                    },
                });

                // Set the comments path for the Swagger JSON and UI.
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
            });
        }

        services.AddCors();
        services.AddResponseCaching();
        services.AddResponseCompression(o =>
        {
            o.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(CompressionTypes);

            o.Providers.Add<GzipCompressionProvider>();
            o.Providers.Add<BrotliCompressionProvider>();
        });
        return services;
    }

    /// <summary>
    /// Add rate limiting middleware.
    /// </summary>
    /// <param name="services">ServiceCollection.</param>
    /// <param name="configuration">Configuration.</param>
    /// <returns>Configured Services.</returns>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        TokenBucketRateLimitOptions tokenBucketRateLimitOptions = new();
        configuration
            .GetSection(TokenBucketRateLimitOptions.RateLimitConfigSection)
            .Bind(tokenBucketRateLimitOptions);

        services.AddRateLimiter(rateLimitOptions =>
        {
            rateLimitOptions.RejectionStatusCode = 429;

            rateLimitOptions.OnRejected = (ctx, _) =>
            {
                if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    ctx.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds)
                        .ToString(NumberFormatInfo.InvariantInfo);
                }

                return ValueTask.CompletedTask;
            };

            // authenticated policy name
            rateLimitOptions.AddPolicy(TokenBucketRateLimitOptions.PolicyName, ctx =>
            {
                var userIdentifier = ctx.User
                    .Claims
                    .FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?
                    .Value;

                if (!string.IsNullOrEmpty(userIdentifier))
                {
                    // TODO: different configuration for authenticated users here
                    return RateLimitPartition.GetTokenBucketLimiter(userIdentifier, _ =>
                        new TokenBucketRateLimiterOptions
                        {
                            TokenLimit = tokenBucketRateLimitOptions.TokenLimit,
                            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                            QueueLimit = tokenBucketRateLimitOptions.QueueLimit,
                            ReplenishmentPeriod = TimeSpan.FromSeconds(tokenBucketRateLimitOptions.ReplenishmentPeriod),
                            TokensPerPeriod = tokenBucketRateLimitOptions.TokensPerPeriod,
                            AutoReplenishment = tokenBucketRateLimitOptions.AutoReplenishment,
                        });
                }

                // TODO: and different configuration for non-authenticated users.
                return RateLimitPartition.GetTokenBucketLimiter("anonymous", _ =>
                    new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = tokenBucketRateLimitOptions.TokenLimit,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = tokenBucketRateLimitOptions.QueueLimit,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(tokenBucketRateLimitOptions.ReplenishmentPeriod),
                        TokensPerPeriod = tokenBucketRateLimitOptions.TokensPerPeriod,
                        AutoReplenishment = true,
                    });
            });
        });

        return services;
    }
}