using ClinicDesk.Api.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ValidationException = ClinicDesk.Api.Application.Exceptions.ValidationException;

namespace ClinicDesk.Api.Common;

public class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;

    public ProblemDetailsExceptionMiddleware(RequestDelegate next, ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, "Recurso no encontrado"),
                ConflictException => (StatusCodes.Status409Conflict, "Conflicto de estado"),
                ValidationException => (StatusCodes.Status400BadRequest, "Solicitud invalida"),
                ForbiddenException => (StatusCodes.Status403Forbidden, "Accion no permitida"),
                UnauthorizedDomainException => (StatusCodes.Status401Unauthorized, "No autorizado"),
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "El recurso cambio mientras se procesaba la solicitud"),
                _ => (StatusCodes.Status500InternalServerError, "Error interno del servidor")
            };

            if (status == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "Error no controlado procesando {Path}", context.Request.Path);
            }

            var problemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = status == StatusCodes.Status500InternalServerError ? "Ocurrio un error inesperado." : ex.Message,
                Instance = context.Request.Path
            };

            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json");
        }
    }
}
