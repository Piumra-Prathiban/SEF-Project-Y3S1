using System.ComponentModel.DataAnnotations;
using SEF_Project.Api.DTOs.Orders;
using SEF_Project.Api.Models.Enums;

namespace SEF_Project.Api.Tests;

public class OrderRequestValidationTests
{
    private static IList<ValidationResult> Validate(object model)
    {
        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(
            model,
            context,
            results,
            validateAllProperties: true);

        return results;
    }

    private static CreateOrderAddressRequest ValidAddress() =>
        new()
        {
            FullName = "Test Customer",
            Line1 = "12 Main Street",
            City = "Colombo",
            PostalCode = "00100"
        };

    [Fact]
    public void CreateOrderItemRequest_ShouldRejectEmptyVariantId()
    {
        var request = new CreateOrderItemRequest
        {
            ProductVariantId = Guid.Empty,
            Quantity = 1
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            r => r.MemberNames.Contains(
                nameof(CreateOrderItemRequest.ProductVariantId)));
    }

    [Fact]
    public void CreateOrderItemRequest_ShouldRejectZeroQuantity()
    {
        var request = new CreateOrderItemRequest
        {
            ProductVariantId = Guid.NewGuid(),
            Quantity = 0
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            r => r.MemberNames.Contains(
                nameof(CreateOrderItemRequest.Quantity)));
    }

    [Fact]
    public void CreateOrderRequest_ShouldRejectEmptyItems()
    {
        var request = new CreateOrderRequest
        {
            Items = new List<CreateOrderItemRequest>(),
            DeliveryAddress = ValidAddress(),
            PaymentMethod = PaymentMethod.Card
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            r => r.MemberNames.Contains(
                nameof(CreateOrderRequest.Items)));
    }

    [Fact]
    public void CreateOrderRequest_ShouldRejectMissingAddress()
    {
        var request = new CreateOrderRequest
        {
            Items = new List<CreateOrderItemRequest>
            {
                new() { ProductVariantId = Guid.NewGuid(), Quantity = 1 }
            },
            DeliveryAddress = null!,
            PaymentMethod = PaymentMethod.Card
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            r => r.MemberNames.Contains(
                nameof(CreateOrderRequest.DeliveryAddress)));
    }

    [Fact]
    public void UpdateOrderStatusRequest_ShouldRejectUndefinedStatus()
    {
        var request = new UpdateOrderStatusRequest
        {
            Status = (OrderStatus)999
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            r => r.MemberNames.Contains(
                nameof(UpdateOrderStatusRequest.Status)));
    }

    [Fact]
    public void UpdatePaymentStatusRequest_ShouldRejectUndefinedStatus()
    {
        var request = new UpdatePaymentStatusRequest
        {
            Status = (PaymentStatus)999
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            r => r.MemberNames.Contains(
                nameof(UpdatePaymentStatusRequest.Status)));
    }

    [Fact]
    public void CreatePaymentRequest_ShouldRejectNegativeAmount()
    {
        var request = new CreatePaymentRequest
        {
            Method = PaymentMethod.Card,
            Amount = -5m
        };

        var results = Validate(request);

        Assert.Contains(
            results,
            r => r.MemberNames.Contains(
                nameof(CreatePaymentRequest.Amount)));
    }
}
