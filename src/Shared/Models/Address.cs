namespace LoanApproval.Shared.Models;

public record Address
{
    public required string Street { get; init; }
    public string? Unit { get; init; }
    public required string City { get; init; }
    public required string State { get; init; }
    public required string ZipCode { get; init; }
    public string Country { get; init; } = "US";

    public override string ToString() =>
        Unit is null
            ? $"{Street}, {City}, {State} {ZipCode}"
            : $"{Street} {Unit}, {City}, {State} {ZipCode}";
}
