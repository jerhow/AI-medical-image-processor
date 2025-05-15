using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using MedicalImageAI.Api.Services;
using MedicalImageAI.Api.BackgroundServices;
using MedicalImageAI.Api.BackgroundServices.Interfaces;
using MedicalImageAI.Api.Data;
using MedicalImageAI.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// --- Add CORS services and define a policy ---
string CORSPolicyAllowSpecificOrigins = "_corsPolicyAllowSpecificOrigins"; // CORS policy name
builder.Services.AddCors(options =>
{
    options.AddPolicy(
        name: CORSPolicyAllowSpecificOrigins,
        policy =>
        {
            // Read allowed origins from configuration
            var allowedOriginsSetting = builder.Configuration["AllowedCorsOrigins"];
            if (!string.IsNullOrEmpty(allowedOriginsSetting))
            {
                var origins = allowedOriginsSetting.Split(',')
                    .Select(o => o.Trim())
                    .Where(o => !string.IsNullOrEmpty(o))
                    .ToArray();

                if (origins.Any())
                {
                    policy.WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    // TODO: Log the origins being used for diagnostics
                    // var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
                    // logger.LogInformation("CORS policy configured for origins: {Origins}", string.Join(", ", origins));
                }
                else
                {
                    // No origins configured, policy will be restrictive (no origins allowed)
                    // TODO: Log a warning
                    // var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
                    // logger.LogWarning("AllowedCorsOrigins configuration is empty. CORS policy will be highly restrictive.");
                }
            }
            else
            {
                // AllowedCorsOrigins not found in configuration
                // This is a fallback for local development
                if (builder.Environment.IsDevelopment())
                {
                    policy.WithOrigins("http://localhost:5272", "https://localhost:7232")
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    // var logger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
                    // logger.LogWarning("AllowedCorsOrigins not configured, using default development origins.");
                }
                
                // else for production, it would remain restrictive (no origins allowed by default)
            }
        });
});

// --- Add services to the web application container ---
builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register the Custom Vision service
builder.Services.AddScoped<ICustomVisionService, CustomVisionService>();

// Register the Blob Storage service
builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();

// Register the Blob Service Client as a singleton
builder.Services.AddSingleton(x => new BlobServiceClient(builder.Configuration.GetValue<string>("BlobStorage:ConnectionString")));

// Register the background queue as a singleton
int backgroundQueueCapacity = builder.Configuration.GetValue<int>("BackgroundQueue:Capacity");
builder.Services.AddSingleton<IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>>>(ctx => {
    return new BackgroundQueue<Func<IServiceProvider, CancellationToken, Task>>(backgroundQueueCapacity);
});

// Register the background queue worker as a hosted service
builder.Services.AddHostedService<QueuedHostedService>();

// Configure and register the DbContext for Entity Framework Core with SQL Server
var connectionString = builder.Configuration["AzureSql:ConnectionString"];
if (string.IsNullOrEmpty(connectionString))
{
    // Assuming Azure SQL is available, even for dev, as provisioned
    throw new InvalidOperationException("Azure SQL connection string 'AzureSql:ConnectionString' not found.");
}
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseCors(CORSPolicyAllowSpecificOrigins); // Use the CORS policy defined above

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseMiddleware<ApiKeyMiddleware>();

app.MapControllers();

app.Run();
