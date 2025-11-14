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
        var client = _clientFactory.CreateClient();
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
                _logger.LogInformation(
                    "[{Timestamp}] [Handler: checkout.session.completed] Customer: {Email}, Session: {SessionId}",
                    timestamp,
                    session?.CustomerEmail,
                    session?.Id
                );
                await client.PostAsJsonAsync("api/provision", new 
                { 
                    customerEmail = session.CustomerEmail,
                    planId = session.Metadata["planId"]
                });
                break;

            case "customer.subscription.created":
                var createdSubscription = stripeEvent.Data.Object as Subscription;
                _logger.LogInformation(
                    "[{Timestamp}] [Handler: subscription.created] Subscription: {SubId}, Customer: {CustomerId}, Status: {Status}, Price: {PriceId}",
                    timestamp,
                    createdSubscription?.Id,
                    createdSubscription?.CustomerId,
                    createdSubscription?.Status,
                    createdSubscription?.Items.Data[0].Price.Id
                );
                // Add provisioning logic here when backend is ready
                break;

            case "invoice.payment_succeeded":
                var invoice = stripeEvent.Data.Object as Invoice;
                _logger.LogInformation(
                    "[{Timestamp}] [Handler: invoice.payment_succeeded] Invoice: {InvoiceId}, Customer: {CustomerId}",
                    timestamp,
                    invoice?.Id,
                    invoice?.CustomerId
                );
                await client.PostAsync("api/renew", null);
                break;

            case "invoice.payment_failed":
                var failedInvoice = stripeEvent.Data.Object as Invoice;
                _logger.LogWarning(
                    "[{Timestamp}] [Handler: invoice.payment_failed] Invoice: {InvoiceId}, Customer: {CustomerId}",
                    timestamp,
                    failedInvoice?.Id,
                    failedInvoice?.CustomerId
                );
                await client.PostAsync("api/mark-failed", null);
                break;

            case "customer.subscription.updated":
                var subscription = stripeEvent.Data.Object as Subscription;
                _logger.LogInformation(
                    "[{Timestamp}] [Handler: subscription.updated] Subscription: {SubId}, Status: {Status}",
                    timestamp,
                    subscription?.Id,
                    subscription?.Status
                );
                await client.PostAsync("api/resize", null);
                break;

            case "customer.subscription.deleted":
                var deletedSubscription = stripeEvent.Data.Object as Subscription;
                _logger.LogInformation(
                    "[{Timestamp}] [Handler: subscription.deleted] Subscription: {SubId}, Customer: {CustomerId}",
                    timestamp,
                    deletedSubscription?.Id,
                    deletedSubscription?.CustomerId
                );
                await client.PostAsync("api/deprovision", null);
                break;

            case "charge.refunded":
                var charge = stripeEvent.Data.Object as Charge;
                _logger.LogInformation(
                    "[{Timestamp}] [Handler: charge.refunded] Charge: {ChargeId}, Amount: {Amount}",
                    timestamp,
                    charge?.Id,
                    charge?.Amount
                );
                await client.PostAsync("api/revoke", null);
                break;

            case "customer.updated":
                var customer = stripeEvent.Data.Object as Customer;
                _logger.LogInformation(
                    "[{Timestamp}] [Handler: customer.updated] Customer: {CustomerId}, Email: {Email}",
                    timestamp,
                    customer?.Id,
                    customer?.Email
                );
                await client.PostAsJsonAsync("api/update-customer", customer);
                break;

            default:
                _logger.LogWarning(
                    "[{Timestamp}] [Handler: unhandled] Event type '{EventType}' not handled",
                    timestamp,
                    stripeEvent.Type
                );
                break;
        }
    }
}