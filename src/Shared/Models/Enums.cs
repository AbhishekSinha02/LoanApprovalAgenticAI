namespace LoanApproval.Shared.Models;

public enum DecisionStatus
{
    Approved,
    ConditionallyApproved,
    Rejected,
    PendingManualReview
}

public enum LoanType
{
    Mortgage,
    PersonalLoan,
    AutoLoan,
    BusinessLoan,
    StudentLoan,
    HomeEquityLoan
}

public enum EmploymentStatus
{
    FullTime,
    PartTime,
    SelfEmployed,
    Unemployed,
    Retired,
    Student
}

public enum RiskLevel
{
    Low,
    Medium,
    High,
    VeryHigh
}

public enum DocumentType
{
    Passport,
    DriversLicense,
    PayStub,
    BankStatement,
    TaxReturn,
    PropertyDeed,
    VehicleTitle,
    BusinessLicense,
    SocialSecurityCard,
    UtilityBill
}
