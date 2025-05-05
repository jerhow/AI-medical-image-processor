using Azure.Storage.Blobs;
using MedicalImageAI.Api.Services;
using MedicalImageAI.Api.BackgroundServices;
using MedicalImageAI.Api.BackgroundServices.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Azure Blob Service Client registration
builder.Services.AddSingleton(x => new BlobServiceClient(builder.Configuration.GetValue<string>("BlobStorage:ConnectionString")));

builder.Services.AddScoped<ICustomVisionService, CustomVisionService>();

// Register the background queue as a singleton
builder.Services.AddSingleton<IBackgroundQueue<Func<IServiceProvider, CancellationToken, Task>>>(ctx => {
    return new BackgroundQueue<Func<IServiceProvider, CancellationToken, Task>>(100); // Example capacity - adjust if needed
});

builder.Services.AddHostedService<QueuedHostedService>();

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
