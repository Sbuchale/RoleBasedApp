namespace RoleBasedApp.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        private readonly ILogger<RequestLoggingMiddleware> _logger;

        public RequestLoggingMiddleware(
            RequestDelegate next,
            ILogger<RequestLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                await _next(context);
            }
            finally
            {
                timer.Stop();

                _logger.LogInformation(
                    "Method={Method}, Path={Path}, Status={Status}, " +
                    "ElapsedMs={ElapsedMs}, TraceId={TraceId}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    timer.ElapsedMilliseconds,
                    context.TraceIdentifier);
            }
        }
    }
}
