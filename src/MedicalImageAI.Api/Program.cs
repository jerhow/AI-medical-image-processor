using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using MedicalImageAI.Api.Services;
using MedicalImageAI.Api.BackgroundServices;
using MedicalImageAI.Api.BackgroundServices.Interfaces;
using MedicalImageAI.Api.Data;
using MedicalImageAI.Api.Entities;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
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

app.MapControllers();

app.Run();
