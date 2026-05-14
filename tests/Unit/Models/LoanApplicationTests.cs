using FluentAssertions;
using LoanApproval.Shared.Models;

namespace LoanApproval.Tests.Unit.Models;

public class LoanApplicationTests
{
    private static LoanApplication BuildSampleApplication(
        int creditScore = 720,
        decimal annualIncome = 90_000m,
        decimal loanAmount = 250_000m,
        LoanType loanType = LoanType.Mortgage) =>
        new()
        {
            Applicant = new ApplicantInfo
            {
                FirstName = "Jane",
                LastName = "Smith",
                SocialSecurityNumberHash = "hash-placeholder",
                DateOfBirth = new DateTime(1985, 3, 15),
                Address = new Address { Street = "123 Main St", City = "Springfield", State = "IL", ZipCode = "62701" },
                EmploymentStatus = EmploymentStatus.FullTime,
                AnnualIncome = annualIncome,
                MonthlyDebtObligations = 500m,
                CreditScore = creditScore,
                YearsAtCurrentJob = 5,
                EmployerName = "Acme Corp"
            },
            LoanDetails = new LoanDetails
            {
                LoanType = loanType,
                RequestedAmount = loanAmount,
                TermMonths = 360,
                Purpose = "Home purchase",
                CollateralValue = 300_000m
            }
        };

    [Fact]
    public void LoanApplication_DefaultsToNewGuid()
    {
        var app1 = BuildSampleApplication();
        var app2 = BuildSampleApplication();
        app1.ApplicationId.Should().NotBe(app2.ApplicationId);
    }

    [Fact]
    public void ApplicantInfo_ComputesFullNameCorrectly()
    {
        var app = BuildSampleApplication();
        app.Applicant.FullName.Should().Be("Jane Smith");
    }

    [Fact]
    public void ApplicantInfo_ComputesAgeCorrectly()
    {
        var app = BuildSampleApplication();
        // Born 1985 → age should be 40 or 41 depending on date
        app.Applicant.Age.Should().BeInRange(38, 45);
    }

    [Fact]
    public void Address_ToStringFormatsCorrectly()
    {
        var address = new Address { Street = "456 Oak Ave", City = "Chicago", State = "IL", ZipCode = "60601" };
        address.ToString().Should().Be("456 Oak Ave, Chicago, IL 60601");
    }

    [Fact]
    public void Address_WithUnit_ToStringIncludesUnit()
    {
        var address = new Address { Street = "456 Oak Ave", Unit = "Apt 3B", City = "Chicago", State = "IL", ZipCode = "60601" };
        address.ToString().Should().Contain("Apt 3B");
    }

    [Fact]
    public void LoanDecision_ApprovedDecisionHasAmount()
    {
        var decision = new LoanDecision
        {
            ApplicationId = Guid.NewGuid(),
            Status = DecisionStatus.Approved,
            Reasoning = "All criteria met",
            OverallScore = 82,
            RiskLevel = RiskLevel.Low,
            ApprovedAmount = 250_000m,
            InterestRate = 6.75m,
            TermMonths = 360
        };

        decision.ApprovedAmount.Should().Be(250_000m);
        decision.Status.Should().Be(DecisionStatus.Approved);
    }
}
