using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SEF_Project.Api.DTOs.AgenticAI;

public class InventoryAnalysisRequestDto
{
    public Guid? WorkflowId { get; set; }

    [Required]
    [StringLength(1000)]
    public string Objective { get; set; } = string.Empty;

    public List<Guid> ProductIds { get; set; } = new();

    public List<Guid> VariantIds { get; set; } = new();

    [StringLength(2000)]
    public string? InventoryContext { get; set; }
}

public class InventoryAnalysisResponseDto
{
    public Guid WorkflowId { get; set; }

    public Guid StepId { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public List<InventoryRecommendationDto> Recommendations { get; set; } =
        new();

    public List<InventoryAgentToolExecutionDto> ToolExecutions { get; set; } =
        new();
}

public class InventoryRecommendationDto
{
    [JsonPropertyName("variantId")]
    public Guid VariantId { get; set; }

    [JsonPropertyName("sku")]
    public string Sku { get; set; } = string.Empty;

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonPropertyName("currentStock")]
    public int CurrentStock { get; set; }

    [JsonPropertyName("reorderLevel")]
    public int ReorderLevel { get; set; }

    [JsonPropertyName("recommendedAction")]
    public string RecommendedAction { get; set; } = string.Empty;

    [JsonPropertyName("recommendedQuantity")]
    public int RecommendedQuantity { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;
}

public class InventoryAgentWorkflowResponseDto
{
    public Guid WorkflowId { get; set; }

    public string Objective { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Plan { get; set; }

    public string? FinalOutcome { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public List<InventoryAgentWorkflowStepDto> Steps { get; set; } = new();

    public List<InventoryAgentApprovalDto> Approvals { get; set; } = new();

    public List<string> Errors { get; set; } = new();
}

public class InventoryAgentWorkflowSummaryDto
{
    public Guid WorkflowId { get; set; }

    public string Objective { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

public class InventoryAgentWorkflowStepDto
{
    public Guid Id { get; set; }

    public int StepOrder { get; set; }

    public string AgentName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? Summary { get; set; }

    public InventoryAgentOutputDto? Result { get; set; }

    public List<InventoryAgentToolExecutionDto> ToolExecutions { get; set; } =
        new();

    public List<string> ValidationSummaries { get; set; } = new();
}

public class InventoryAgentApprovalDto
{
    public string Status { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public int? ReviewedByUserId { get; set; }

    public string? Comment { get; set; }
}

public class InventoryAgentApprovalRequestDto
{
    [StringLength(1000)]
    public string? Comment { get; set; }
}

public class InventoryAgentRevisionRequestDto
{
    [Required]
    [StringLength(1000)]
    public string Comment { get; set; } = string.Empty;
}

public class InventoryAgentOutputDto
{
    [JsonPropertyName("recommendations")]
    public List<InventoryRecommendationDto> Recommendations { get; set; } =
        new();
}

public class InventoryAgentToolExecutionDto
{
    public string ToolName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;
}
