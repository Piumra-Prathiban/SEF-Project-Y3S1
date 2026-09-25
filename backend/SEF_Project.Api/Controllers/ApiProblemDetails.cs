using Microsoft.AspNetCore.Mvc;

namespace SEF_Project.Api.Controllers;

internal static class ApiProblemDetails
{
    public static ProblemDetails NotFound(ControllerBase controller, string detail) =>
        new()
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = detail,
            Instance = controller.HttpContext?.Request.Path.Value ?? string.Empty
        };
}
