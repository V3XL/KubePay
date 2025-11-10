using Stripe;
using KubePay.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
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
    try
    {
        var stripeEvent = EventUtility.ConstructEvent(
            json,
            request.Headers["Stripe-Signature"],
            webhookSecret
        );

        await webhookHandler.HandleEventAsync(stripeEvent);
        return Results.Ok();
    }
    catch (StripeException ex)
    {
        logger.LogError(ex, "Error processing Stripe webhook");
        return Results.BadRequest();
    }
});

app.Run();
