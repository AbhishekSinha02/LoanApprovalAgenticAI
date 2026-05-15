using System.Text.Json.Serialization;
using LoanApproval.Agents;
using LoanApproval.Api.Endpoints;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, lc) => lc
    .ReadFrom.Configuration(ctx.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "{Timestamp:HH:mm:ss} {Level:u3} {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/loan-approval-.log",
        rollingInterval: Serilog.RollingInterval.Day,
        outputTemplate: "{Timestamp:HH:mm:ss.fff} {Level:u3} [{SourceContext}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7,
        shared: true));

builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddLoanApprovalAgents(builder.Configuration);

var timeoutMinutes = builder.Configuration.GetValue("LoanApproval:TimeoutMinutes", 10);
builder.Services.AddRequestTimeouts(options =>
    options.AddPolicy("LoanApproval", TimeSpan.FromMinutes(timeoutMinutes)));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Loan Approval Agentic AI API",
        Version = "v1",
        Description = "Multi-agent AI system for automated loan underwriting"
    });
    c.UseInlineDefinitionsForEnums(); // show enum string names in Swagger UI
});

var aiConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(aiConnectionString))
    builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Loan Approval API v1"));
}

app.UseRequestTimeouts();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

// Map endpoints
app.MapLoanApplicationEndpoints();
app.MapHealthCheck();

app.Run();
