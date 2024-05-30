namespace Dometrain.Monolith.Api.Courses;

public static class CourseEndpointExtensions
{
    public static WebApplication MapCourseEndpoints(this WebApplication app)
    {
        app.MapPost("/courses", CourseEndpoints.Create)
            .RequireAuthorization("Admin", "ApiAdmin");

        app.MapGet("/courses/{idOrSlug}", CourseEndpoints.Get)
            .AllowAnonymous();
        
        app.MapGet("/courses", CourseEndpoints.GetAll)
            .AllowAnonymous();
        
        app.MapPut("/courses/{id:guid}", CourseEndpoints.Update)
            .RequireAuthorization("Admin", "ApiAdmin");
        
        app.MapDelete("/courses/{id:guid}", CourseEndpoints.Delete)
            .RequireAuthorization("Admin", "ApiAdmin");
        
        return app;
    }
}
