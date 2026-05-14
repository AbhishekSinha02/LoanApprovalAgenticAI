using LoanApproval.Agents;
using LoanApproval.Api.Endpoints;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLoanApprovalAgents(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Loan Approval Agentic AI API",
        Version = "v1",
        Description = "Multi-agent AI system for automated loan underwriting"
    });
});

builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Loan Approval API v1"));
}

app.UseHttpsRedirection();

// Map endpoints
app.MapLoanApplicationEndpoints();
app.MapHealthCheck();

app.Run();
