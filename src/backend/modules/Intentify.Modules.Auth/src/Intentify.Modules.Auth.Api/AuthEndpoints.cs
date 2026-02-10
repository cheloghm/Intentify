using Intentify.Modules.Auth.Application;
using Intentify.Shared.Web;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;

namespace Intentify.Modules.Auth.Api;

internal static class AuthEndpoints
{
    public static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        RegisterUserHandler handler)
    {
        var result = await handler.HandleAsync(new RegisterUserCommand(
            request.DisplayName,
            request.Email,
            request.Password));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Ok(new LoginResponse(result.Value!.AccessToken))
        };
    }

    public static async Task<IResult> LoginAsync(
        LoginRequest request,
        LoginUserHandler handler)
    {
        var result = await handler.HandleAsync(new LoginUserCommand(request.Email, request.Password));

        return result.Status switch
        {
            Intentify.Shared.Validation.OperationStatus.ValidationFailed => Results.BadRequest(
                ProblemDetailsHelpers.CreateValidationProblemDetails(result.Errors!.Errors)),
            Intentify.Shared.Validation.OperationStatus.Unauthorized => Results.Unauthorized(),
            Intentify.Shared.Validation.OperationStatus.Error => Results.Problem(
                statusCode: StatusCodes.Status500InternalServerError),
            _ => Results.Ok(new LoginResponse(result.Value!.AccessToken))
        };
    }

    public static async Task<IResult> GetCurrentUser(HttpContext context, IMongoDatabase database)
    {
        var response = await CurrentUserResponseFactory.CreateAsync(context, database);
        return Results.Ok(response);
    }

}
