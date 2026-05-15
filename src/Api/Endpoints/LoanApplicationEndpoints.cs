using LoanApproval.Shared.Interfaces;
using LoanApproval.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoanApproval.Api.Endpoints;

public static class LoanApplicationEndpoints
{
    public static IEndpointRouteBuilder MapLoanApplicationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/loans")
            .WithTags("Loan Applications");

        group.MapPost("/apply", SubmitApplicationAsync)
            .WithName("SubmitLoanApplication")
            .WithSummary("Submit a loan application for AI-powered underwriting")
            .Produces<LoanDecision>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status500InternalServerError)
            .WithRequestTimeout("LoanApproval");

        return app;
    }

    private static async Task<IResult> SubmitApplicationAsync(
        [FromBody] LoanApplication application,
        ILoanOrchestrator orchestrator,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (application is null)
            return Results.Problem("Request body is required.", statusCode: 400);

        if (application.Applicant is null)
            return Results.Problem("Applicant information is required.", statusCode: 400);

        if (application.LoanDetails is null)
            return Results.Problem("Loan details are required.", statusCode: 400);

        if (application.LoanDetails.RequestedAmount <= 0)
            return Results.Problem("Requested amount must be greater than zero.", statusCode: 400);

        var logger = loggerFactory.CreateLogger("LoanApplication");
        logger.LogInformation("Received loan application {ApplicationId} for ${Amount:N0} {LoanType}",
            application.ApplicationId,
            application.LoanDetails.RequestedAmount,
            application.LoanDetails.LoanType);

        try
        {
            var decision = await orchestrator.ProcessApplicationAsync(application, cancellationToken);

            logger.LogInformation("Application {ApplicationId} decided: {Status} (score: {Score})",
                application.ApplicationId, decision.Status, decision.OverallScore);

            return Results.Ok(decision);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Application {ApplicationId} request was cancelled by client or server timeout",
                application.ApplicationId);
            return Results.Problem(
                "The request timed out. Your application has been received and will be reviewed manually.",
                statusCode: 503, title: "Service Unavailable");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error processing application {ApplicationId}", application.ApplicationId);
            return Results.Problem(
                "Your application has been received. A loan officer will contact you within 2 business days.",
                statusCode: 503, title: "Service Unavailable");
        }
    }
}

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthCheck(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }))
            .WithName("HealthCheck")
            .WithTags("Health")
            .ExcludeFromDescription();

        return app;
    }
}
