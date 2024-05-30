namespace Dometrain.Monolith.Api.Students;

public static class StudentEndpointExtensions
{
    public static WebApplication MapStudentEndpoints(this WebApplication app)
    {
        app.MapPost("/students", StudentEndpoints.Register)
            .AllowAnonymous();
        
        app.MapGet("/students/me", StudentEndpoints.GetMe)
            .RequireAuthorization();
        
        app.MapGet("/students/{idOrEmail}", StudentEndpoints.Get)
            .WithName(StudentEndpoints.GetStudentEndpointName)
            .RequireAuthorization("Admin", "ApiAdmin");
        
        app.MapGet("/students", StudentEndpoints.GetAll)
            .RequireAuthorization("Admin", "ApiAdmin");
        
        app.MapDelete("/students/{id:guid}", StudentEndpoints.Delete)
            .RequireAuthorization("Admin", "ApiAdmin");
        
        return app;
    }
}
