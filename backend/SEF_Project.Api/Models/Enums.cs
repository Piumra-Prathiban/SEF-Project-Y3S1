namespace SEF_Project.Api.Models.Enums;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Preparing,
    Ready,
    Completed,
    Cancelled,
    Refunded
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded
}

public enum PaymentMethod
{
    Card,
    Cash,
    OnlineTransfer
}

public enum ShipmentStatus
{
    Pending,
    Shipped,
    Delivered,
    Cancelled
}

public enum InventoryTransactionType
{
    Adjustment,
    Receipt,
    Sale,
    Reservation,
    ReservationRelease,
    Transfer
}

public enum PromotionType
{
    PercentageDiscount,
    FixedAmountDiscount,
    BuyXGetY,
    FreeShipping
}

public enum CampaignStatus
{
    Draft,
    Scheduled,
    Active,
    Paused,
    Completed,
    Cancelled
}

public enum AgentWorkflowStatus
{
    Pending,
    Planning,
    InProgress,
    AwaitingApproval,
    Completed,
    Failed,
    Cancelled
}

public enum AgentStepStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}

public enum AgentToolStatus
{
    Running,
    Success,
    Failed
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
    RevisionRequested
}
