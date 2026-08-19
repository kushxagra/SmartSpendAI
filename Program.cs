using Microsoft.EntityFrameworkCore;
using SmartSpendAI.Data;
using SmartSpendAI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<PdfTextExtractor>();
builder.Services.AddScoped<ValidationService>();
builder.Services.AddScoped<InvoiceProcessingService>();
builder.Services.AddScoped<RiskService>();
builder.Services.AddHttpClient<AIService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

using (var scope = app.Services.CreateScope())
    DbSeeder.Seed(scope.ServiceProvider.GetRequiredService<AppDbContext>());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
