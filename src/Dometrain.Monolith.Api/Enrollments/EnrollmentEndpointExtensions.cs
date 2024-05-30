namespace Dometrain.Monolith.Api.Enrollments;

public static class EnrollmentEndpointExtensions
{
    public static WebApplication MapEnrollmentEndpoints(this WebApplication app)
    {
        app.MapGet("/enrollments", EnrollmentEndpoints.Get)
            .RequireAuthorization();
        
        app.MapPut("/enrollments/{courseId:guid}", EnrollmentEndpoints.Enroll)
            .RequireAuthorization("Admin", "ApiAdmin");
        
        app.MapDelete("/enrollments/{courseId:guid}", EnrollmentEndpoints.UnEnroll)
            .RequireAuthorization("Admin", "ApiAdmin");
        
        return app;
    }
}
