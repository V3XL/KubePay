using Stripe;
using KubePay.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddScoped<StripeWebhookHandler>();

// Configure Stripe with environment variable
StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_API_KEY");
var webhookSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/stripe/webhook", async (
    HttpRequest request,
    ILogger<Program> logger,
    StripeWebhookHandler webhookHandler) =>
{
    var json = await new StreamReader(request.Body).ReadToEndAsync();
    var clientIp = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    
    try
    {
        var stripeEvent = EventUtility.ConstructEvent(
            json,
            request.Headers["Stripe-Signature"],
            webhookSecret,
            throwOnApiVersionMismatch: false
        );

        logger.LogInformation(
            "[{Timestamp}] [IP: {IpAddress}] [Event: {EventType}] [ID: {EventId}] Webhook received",
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            clientIp,
            stripeEvent.Type,
            stripeEvent.Id
        );

        await webhookHandler.HandleEventAsync(stripeEvent);
        
        logger.LogInformation(
            "[{Timestamp}] [IP: {IpAddress}] [Event: {EventType}] [ID: {EventId}] Webhook processed successfully",
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            clientIp,
            stripeEvent.Type,
            stripeEvent.Id
        );
        
        return Results.Ok();
    }
    catch (StripeException ex)
    {
        logger.LogError(
            "[{Timestamp}] [IP: {IpAddress}] [Error: Stripe] {Message}",
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            clientIp,
            ex.Message
        );
        return Results.BadRequest();
    }
    catch (Exception ex)
    {
        logger.LogError(
            "[{Timestamp}] [IP: {IpAddress}] [Error: Unexpected] {Message}",
            DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            clientIp,
            ex.Message
        );
        return Results.StatusCode(500);
    }
});

app.Run();