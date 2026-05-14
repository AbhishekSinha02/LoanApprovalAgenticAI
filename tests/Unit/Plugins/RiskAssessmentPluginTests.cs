using System.Text.Json;
using FluentAssertions;
using LoanApproval.Agents.Plugins;

namespace LoanApproval.Tests.Unit.Plugins;

public class RiskAssessmentPluginTests
{
    private readonly RiskAssessmentPlugin _plugin = new();

    [Fact]
    public void CalculateRiskScore_ExcellentProfile_ReturnsLowRisk()
    {
        var json = _plugin.CalculateRiskScore(
            creditScore: 800,
            dtiRatio: 20m,
            yearsEmployed: 10,
            loanType: "Mortgage",
            ltvRatio: 60m);

        var result = JsonSerializer.Deserialize<JsonElement>(json);
        result.GetProperty("riskLevel").GetString().Should().Be("Low");
        result.GetProperty("riskScore").GetInt32().Should().BeLessThan(25);
    }

    [Fact]
    public void CalculateRiskScore_PoorProfile_ReturnsHighRisk()
    {
        var json = _plugin.CalculateRiskScore(
            creditScore: 520,
            dtiRatio: 48m,
            yearsEmployed: 0,
            loanType: "PersonalLoan",
            ltvRatio: 0m);

        var result = JsonSerializer.Deserialize<JsonElement>(json);
        var riskLevel = result.GetProperty("riskLevel").GetString();
        riskLevel.Should().BeOneOf("High", "VeryHigh");
    }

    [Fact]
    public void EvaluateLoanToValue_AboveMaxLtv_ReturnsFail()
    {
        var json = _plugin.EvaluateLoanToValue(500_000m, 480_000m, "Mortgage");
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        result.GetProperty("pass").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void EvaluateLoanToValue_NoCollateral_ReturnsPass()
    {
        var json = _plugin.EvaluateLoanToValue(20_000m, 0m, "PersonalLoan");
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        result.GetProperty("pass").GetBoolean().Should().BeTrue();
    }

    [Theory]
    [InlineData(10, "Mortgage", 60)]
    [InlineData(15, "PersonalLoan", 75)]
    public void DetermineInterestRate_HigherRisk_ReturnsHigherRate(int lowRisk, string loanType, int higherRisk)
    {
        var lowJson  = _plugin.DetermineInterestRate(lowRisk, loanType, 120);
        var highJson = _plugin.DetermineInterestRate(higherRisk, loanType, 120);

        var lowRate  = JsonSerializer.Deserialize<JsonElement>(lowJson).GetProperty("suggestedRate").GetDecimal();
        var highRate = JsonSerializer.Deserialize<JsonElement>(highJson).GetProperty("suggestedRate").GetDecimal();

        highRate.Should().BeGreaterThanOrEqualTo(lowRate);
    }
}
