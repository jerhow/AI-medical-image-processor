using Azure.Storage.Blobs;
using MedicalImageAI.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Azure Blob Service Client registration
builder.Services.AddSingleton(x => new BlobServiceClient(builder.Configuration.GetValue<string>("BlobStorage:ConnectionString")));

builder.Services.AddScoped<ICustomVisionService, CustomVisionService>();

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
