using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SEF_Project.Api.Models.AgenticAI;

namespace SEF_Project.Api.Data.Configurations;

public class AgentWorkflowConfiguration : IEntityTypeConfiguration<AgentWorkflow>
{
    public void Configure(EntityTypeBuilder<AgentWorkflow> builder)
    {
        builder.ToTable("AgentWorkflows");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Objective).IsRequired().HasMaxLength(2000);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.PlanSummary).HasMaxLength(4000);
        builder.Property(e => e.FinalOutcome).HasMaxLength(4000);

        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.CreatedAt);
    }
}

public class AgentWorkflowStepConfiguration : IEntityTypeConfiguration<AgentWorkflowStep>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowStep> builder)
    {
        builder.ToTable("AgentWorkflowSteps");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.AgentName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Title).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Summary).HasMaxLength(4000);

        builder.HasIndex(e => new { e.WorkflowId, e.StepOrder }).IsUnique();

        builder.HasOne(e => e.Workflow)
            .WithMany(e => e.Steps)
            .HasForeignKey(e => e.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgentToolExecutionConfiguration : IEntityTypeConfiguration<AgentToolExecution>
{
    public void Configure(EntityTypeBuilder<AgentToolExecution> builder)
    {
        builder.ToTable("AgentToolExecutions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ToolName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ToolArgumentsJson).HasColumnType("jsonb");
        builder.Property(e => e.ToolResultJson).HasColumnType("jsonb");
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.ErrorMessage).HasMaxLength(2000);

        builder.HasIndex(e => e.StepId);

        builder.HasOne(e => e.Step)
            .WithMany(e => e.ToolExecutions)
            .HasForeignKey(e => e.StepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgentValidationResultConfiguration : IEntityTypeConfiguration<AgentValidationResult>
{
    public void Configure(EntityTypeBuilder<AgentValidationResult> builder)
    {
        builder.ToTable("AgentValidationResults");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ValidatorName).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Message).HasMaxLength(2000);
        builder.Property(e => e.Severity).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(e => e.StepId);

        builder.HasOne(e => e.Step)
            .WithMany(e => e.ValidationResults)
            .HasForeignKey(e => e.StepId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class AgentApprovalConfiguration : IEntityTypeConfiguration<AgentApproval>
{
    public void Configure(EntityTypeBuilder<AgentApproval> builder)
    {
        builder.ToTable("AgentApprovals");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.Comment).HasMaxLength(2000);

        builder.HasIndex(e => new { e.WorkflowId, e.Status });

        builder.HasOne(e => e.Workflow)
            .WithMany(e => e.Approvals)
            .HasForeignKey(e => e.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Step)
            .WithMany()
            .HasForeignKey(e => e.StepId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.ReviewedByUser)
            .WithMany()
            .HasForeignKey(e => e.ReviewedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AgentWorkflowErrorConfiguration : IEntityTypeConfiguration<AgentWorkflowError>
{
    public void Configure(EntityTypeBuilder<AgentWorkflowError> builder)
    {
        builder.ToTable("AgentWorkflowErrors");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.ErrorType).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Message).IsRequired().HasMaxLength(2000);

        builder.HasIndex(e => e.WorkflowId);

        builder.HasOne(e => e.Workflow)
            .WithMany(e => e.Errors)
            .HasForeignKey(e => e.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Step)
            .WithMany()
            .HasForeignKey(e => e.StepId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
