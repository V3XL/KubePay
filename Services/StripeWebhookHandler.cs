using Stripe;

namespace KubePay.Services;

public class StripeWebhookHandler
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly ILogger<StripeWebhookHandler> _logger;

    public StripeWebhookHandler(IHttpClientFactory clientFactory, ILogger<StripeWebhookHandler> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task HandleEventAsync(Event stripeEvent)
    {
        _logger.LogInformation($"Processing Stripe webhook: {stripeEvent.Type}");
        var client = _clientFactory.CreateClient();

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                await client.PostAsJsonAsync("api/provision", new 
                { 
                    customerEmail = session.CustomerEmail,
                    planId = session.Metadata["planId"]
                });
                break;

            case "customer.subscription.created":
                var createdSubscription = stripeEvent.Data.Object as Subscription;
                _logger.LogInformation($"Subscription created: {createdSubscription.Id}, Status: {createdSubscription.Status}");
                await client.PostAsJsonAsync("api/provision", new 
                { 
                    subscriptionId = createdSubscription.Id,
                    customerId = createdSubscription.CustomerId,
                    status = createdSubscription.Status,
                    priceId = createdSubscription.Items.Data[0].Price.Id
                });
                break;

            case "invoice.payment_succeeded":
                var invoice = stripeEvent.Data.Object as Invoice;
                await client.PostAsync("api/renew", null);
                break;

            case "invoice.payment_failed":
                await client.PostAsync("api/mark-failed", null);
                break;

            case "customer.subscription.updated":
                var subscription = stripeEvent.Data.Object as Subscription;
                await client.PostAsync("api/resize", null);
                break;

            case "customer.subscription.deleted":
                await client.PostAsync("api/deprovision", null);
                break;

            case "charge.refunded":
                await client.PostAsync("api/revoke", null);
                break;

            case "customer.updated":
                var customer = stripeEvent.Data.Object as Customer;
                await client.PostAsJsonAsync("api/update-customer", customer);
                break;

            default:
                _logger.LogWarning($"Unhandled event type: {stripeEvent.Type}");
                break;
        }
    }
}