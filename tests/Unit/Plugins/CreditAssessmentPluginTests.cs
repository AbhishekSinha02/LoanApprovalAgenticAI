using System.Text.Json;
using FluentAssertions;
using LoanApproval.Agents.Plugins;

namespace LoanApproval.Tests.Unit.Plugins;

public class CreditAssessmentPluginTests
{
    private readonly CreditAssessmentPlugin _plugin = new();

    [Theory]
    [InlineData(850, "Exceptional")]
    [InlineData(760, "Very Good")]
    [InlineData(700, "Good")]
    [InlineData(620, "Fair")]
    [InlineData(520, "Poor")]
    [InlineData(400, "Very Poor")]
    public void EvaluateCreditScore_ReturnsCorrectCategory(int score, string expectedCategory)
    {
        var json = _plugin.EvaluateCreditScore(score);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        result.GetProperty("category").GetString().Should().Be(expectedCategory);
    }

    [Fact]
    public void EvaluateCreditScore_BelowMinimum_SetsMetMinimumFalse()
    {
        var json = _plugin.EvaluateCreditScore(400);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        result.GetProperty("meetsMinimum").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void CalculateMonthlyPayment_PositiveRate_ReturnsCorrectPayment()
    {
        // $100,000 at 6% over 360 months ≈ $599.55
        var json = _plugin.CalculateMonthlyPayment(100_000m, 6.0m, 360);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        var payment = result.GetProperty("monthlyPayment").GetDecimal();
        payment.Should().BeApproximately(599.55m, 1.0m);
    }

    [Fact]
    public void CalculateMonthlyPayment_ZeroRate_ReturnsFlatPayment()
    {
        var json = _plugin.CalculateMonthlyPayment(12_000m, 0m, 12);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        result.GetProperty("monthlyPayment").GetDecimal().Should().Be(1_000m);
    }

    [Theory]
    [InlineData(5000, 500, 200, 14, true)]   // DTI = 14% — excellent
    [InlineData(5000, 1500, 500, 40, true)]  // DTI = 40% — acceptable
    [InlineData(5000, 2000, 500, 50, false)] // DTI = 50% — too high
    public void CalculateDebtToIncomeRatio_ReturnsCorrectPassAndRatio(
        decimal income, decimal existingDebt, decimal newPayment, decimal expectedDti, bool expectedPass)
    {
        var json = _plugin.CalculateDebtToIncomeRatio(income, existingDebt, newPayment);
        var result = JsonSerializer.Deserialize<JsonElement>(json);

        result.GetProperty("dtiRatio").GetDecimal().Should().BeApproximately(expectedDti, 0.5m);
        result.GetProperty("pass").GetBoolean().Should().Be(expectedPass);
    }
}
