# RabbitMQ Service Bus Implementation

This project provides RabbitMQ implementations of the service bus abstractions for topics (pub/sub) and queues (point-to-point) messaging patterns.

## Table of Contents

- [Prerequisites](#prerequisites)
- [Installation](#installation)
- [Configuration](#configuration)
- [Setup](#setup)
- [Usage Examples](#usage-examples)
- [Features](#features)
- [Advanced Configuration](#advanced-configuration)
  - [Name Transformation and Sanitization](#name-transformation-and-sanitization)
  - [Custom Name Resolvers](#custom-name-resolvers)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## Prerequisites

- .NET 8.0 or later
- RabbitMQ server (local or remote)
- Understanding of messaging patterns (topics vs queues)

### Installing RabbitMQ

**Using Docker (Recommended):**
```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 \
  -e RABBITMQ_DEFAULT_USER=admin \
  -e RABBITMQ_DEFAULT_PASS=password \
  rabbitmq:3-management
```

**Using Homebrew (macOS):**
```bash
brew install rabbitmq
brew services start rabbitmq
```

**Using Chocolatey (Windows):**
```powershell
choco install rabbitmq
```

## Installation

### 1. Add Project Reference

Add a project reference to your application:

```xml
<ItemGroup>
  <ProjectReference Include="..\BoilerPlate.ServiceBus.RabbitMq\BoilerPlate.ServiceBus.RabbitMq.csproj" />
</ItemGroup>
```

Or if using as a NuGet package:

```xml
<ItemGroup>
  <PackageReference Include="BoilerPlate.ServiceBus.RabbitMq" Version="1.0.0" />
</ItemGroup>
```

### 2. Add Required Abstractions

Ensure you also reference the abstractions project:

```xml
<ItemGroup>
  <ProjectReference Include="..\BoilerPlate.ServiceBus.Abstractions\BoilerPlate.ServiceBus.Abstractions.csproj" />
</ItemGroup>
```

## Configuration

### Connection String

The RabbitMQ connection string can be provided in two ways, with the environment variable taking precedence:

#### Option 1: Environment Variable (Recommended for Production)

**Windows (PowerShell):**
```powershell
$env:RABBITMQ_CONNECTION_STRING="amqp://admin:password@localhost:5672/"
```

**Windows (Command Prompt):**
```cmd
set RABBITMQ_CONNECTION_STRING=amqp://admin:password@localhost:5672/
```

**Linux/macOS:**
```bash
export RABBITMQ_CONNECTION_STRING="amqp://admin:password@localhost:5672/"
```

**Docker/Kubernetes:**
```yaml
env:
  - name: RABBITMQ_CONNECTION_STRING
    value: "amqp://admin:password@rabbitmq:5672/"
```

#### Option 2: Configuration File

Add to `appsettings.json`:

```json
{
  "RabbitMq": {
    "ConnectionString": "amqp://admin:password@localhost:5672/"
  }
}
```

Or `appsettings.Development.json`:

```json
{
  "RabbitMq": {
    "ConnectionString": "amqp://guest:guest@localhost:5672/"
  }
}
```

### Connection String Format

```
amqp://[username]:[password]@[host]:[port]/[vhost]
```

**Examples:**
- `amqp://guest:guest@localhost:5672/` - Default RabbitMQ setup (default vhost)
- `amqp://admin:password@rabbitmq.example.com:5672/production` - Remote server with custom vhost
- `amqp://user:pass@localhost:5672/my-vhost` - Local server with custom vhost
- `amqp://admin:password@rabbitmq:5672/` - Docker container name as host

**Note:** The vhost path is optional. Use `/` for the default vhost or omit it entirely.

## Setup

### 1. Register Services

In your `Program.cs`:

```csharp
using BoilerPlate.ServiceBus.RabbitMq.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add RabbitMQ service bus
builder.Services.AddRabbitMqServiceBus(builder.Configuration);

var app = builder.Build();
app.Run();
```

Or in `Startup.cs` (for .NET 6+ minimal hosting or older frameworks):

```csharp
using BoilerPlate.ServiceBus.RabbitMq.Extensions;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Add RabbitMQ service bus
        services.AddRabbitMqServiceBus(Configuration);
    }
}
```

### 2. Verify Configuration

The service registration will:
- Read connection string from `RABBITMQ_CONNECTION_STRING` environment variable (if set)
- Fall back to `RabbitMq:ConnectionString` from configuration
- Throw an exception if neither is provided

### 3. Test Connection

You can verify the connection is working by checking the logs on application startup. The connection manager will attempt to connect when first used.

## Factory Pattern

The RabbitMQ implementation includes factory interfaces for creating publishers and subscribers on demand. This is useful when you need to work with multiple message types or when message types are determined at runtime.

### Available Subscriber Factories

- `ITopicSubscriberFactory` - Creates topic subscribers
- `IQueueSubscriberFactory` - Creates queue subscribers

**Note:** Publishers are non-generic and can be injected directly. Only subscribers use factories since they remain generic.

### Using Subscriber Factories

Subscriber factories are automatically registered when you call `AddRabbitMqServiceBus()`. You can inject them into your services:

```csharp
public class EventHandler
{
    private readonly ITopicSubscriberFactory _subscriberFactory;
    
    public EventHandler(ITopicSubscriberFactory subscriberFactory)
    {
        _subscriberFactory = subscriberFactory;
    }
    
    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        // Subscribe to UserCreatedEvent
        var userCreatedSubscriber = _subscriberFactory.CreateSubscriber<UserCreatedEvent>();
        await userCreatedSubscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                await HandleUserCreatedAsync(message);
            },
            cancellationToken);
        
        // Subscribe to OrderCreatedEvent
        var orderCreatedSubscriber = _subscriberFactory.CreateSubscriber<OrderCreatedEvent>();
        await orderCreatedSubscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                await HandleOrderCreatedAsync(message);
            },
            cancellationToken);
    }
}
```

### Benefits

- **Single Dependency**: Inject one factory instead of multiple subscribers
- **Dynamic Message Types**: Create subscribers for message types determined at runtime
- **Reduced Constructor Parameters**: Avoid injecting many generic types
- **Lazy Creation**: Subscribers are created only when needed

## Features

### Topics (Pub/Sub)

- **Exchange Type**: Topic exchange
- **Routing**: Uses `#` routing key to match all bindings
- **Queue**: Each subscriber gets a unique, auto-deleted queue
- **Durability**: Exchange is durable, queues are not (unique per consumer)

### Queues (Point-to-Point)

- **Queue Type**: Standard durable queue
- **Delivery**: Only one consumer processes each message
- **QoS**: Prefetch count set to 1 for fair distribution
- **Durability**: Queue is durable and persists across restarts

### Failure Handling

- Automatically catches exceptions from message handlers
- Increments `FailureCount` on each failure
- Retries messages when `FailureCount` is within limits
- Permanently fails and destroys messages when limit exceeded
- Logs all failures with full context

### Message Serialization

- Uses `System.Text.Json` for serialization
- Camel case property naming
- IMessage properties stored in headers for reliability
- Metadata passed as additional headers

## Usage Examples

### Complete Example: User Registration with Event Publishing (Topic)

#### 1. Define the Message

```csharp
using BoilerPlate.ServiceBus.Abstractions;

public class UserCreatedEvent : IMessage
{
    // IMessage properties
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
    
    // Message-specific properties
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
```

#### 2. Publisher Service (Direct Injection)

```csharp
using BoilerPlate.ServiceBus.Abstractions;
using System.Diagnostics;

public class UserRegistrationService
{
    private readonly ITopicPublisher<UserCreatedEvent> _publisher;
    private readonly ILogger<UserRegistrationService> _logger;
    
    public UserRegistrationService(
        ITopicPublisher<UserCreatedEvent> publisher,
        ILogger<UserRegistrationService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }
    
    public async Task RegisterUserAsync(string email, string password)
    {
        // Create user in database
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            Name = email.Split('@')[0]
        };
        
        await _userRepository.CreateAsync(user);
        
        // Publish event to topic (multiple subscribers can receive this)
        var message = new UserCreatedEvent
        {
            UserId = user.Id,
            UserName = user.Name,
            Email = user.Email,
            TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
            ReferenceId = user.Id.ToString(),
            CreatedTimestamp = DateTime.UtcNow,
            FailureCount = 0
        };
        
        await _publisher.PublishAsync(message);
        _logger.LogInformation("Published UserCreatedEvent for user {UserId}", user.Id);
    }
}
```


#### 3. Subscriber Services (Multiple Subscribers Can Receive Same Message)

**Email Service (Direct Injection):**
```csharp
using BoilerPlate.ServiceBus.Abstractions;

public class EmailService : IHostedService
{
    private readonly ITopicSubscriber<UserCreatedEvent> _subscriber;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailService> _logger;
    
    public EmailService(
        ITopicSubscriber<UserCreatedEvent> subscriber,
        IEmailSender emailSender,
        ILogger<EmailService> logger)
    {
        _subscriber = subscriber;
        _emailSender = emailSender;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _subscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                _logger.LogInformation("Sending welcome email to {Email}", message.Email);
                await _emailSender.SendWelcomeEmailAsync(message.Email, message.UserName);
            },
            maxFailureCount: 3,
            onPermanentFailure: async (message, ex, metadata, ct) =>
            {
                _logger.LogError(
                    ex,
                    "Permanently failed to send welcome email. UserId: {UserId}, Email: {Email}",
                    message.UserId,
                    message.Email);
                // Could send to dead letter queue or alerting system
            },
            cancellationToken);
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _subscriber.UnsubscribeAsync(cancellationToken);
    }
}
```

**Email Service (Factory Pattern):**
```csharp
using BoilerPlate.ServiceBus.Abstractions;

public class EmailService : IHostedService
{
    private readonly ITopicSubscriberFactory _subscriberFactory;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<EmailService> _logger;
    private ITopicSubscriber<UserCreatedEvent>? _subscriber;
    
    public EmailService(
        ITopicSubscriberFactory subscriberFactory,
        IEmailSender emailSender,
        ILogger<EmailService> logger)
    {
        _subscriberFactory = subscriberFactory;
        _emailSender = emailSender;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _subscriber = _subscriberFactory.CreateSubscriber<UserCreatedEvent>();
        
        await _subscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                _logger.LogInformation("Sending welcome email to {Email}", message.Email);
                await _emailSender.SendWelcomeEmailAsync(message.Email, message.UserName);
            },
            maxFailureCount: 3,
            onPermanentFailure: async (message, ex, metadata, ct) =>
            {
                _logger.LogError(
                    ex,
                    "Permanently failed to send welcome email. UserId: {UserId}, Email: {Email}",
                    message.UserId,
                    message.Email);
            },
            cancellationToken);
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _subscriber?.UnsubscribeAsync(cancellationToken) ?? Task.CompletedTask;
    }
}
```

**Analytics Service (Another Subscriber to Same Topic):**
```csharp
public class AnalyticsService : IHostedService
{
    private readonly ITopicSubscriber<UserCreatedEvent> _subscriber;
    private readonly IAnalyticsRepository _analytics;
    
    public AnalyticsService(
        ITopicSubscriber<UserCreatedEvent> subscriber,
        IAnalyticsRepository analytics)
    {
        _subscriber = subscriber;
        _analytics = analytics;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _subscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                await _analytics.TrackUserRegistrationAsync(message.UserId, message.CreatedTimestamp);
            },
            maxFailureCount: 5, // More retries for analytics (less critical)
            cancellationToken: cancellationToken);
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _subscriber.UnsubscribeAsync(cancellationToken);
    }
}
```

### Complete Example: Order Processing with Queue

#### 1. Define the Command Message

```csharp
public class ProcessOrderCommand : IMessage
{
    // IMessage properties
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
    
    // Command-specific properties
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public decimal TotalAmount { get; set; }
}
```

#### 2. Publisher Service

```csharp
public class OrderService
{
    private readonly IQueuePublisher<ProcessOrderCommand> _queuePublisher;
    private readonly IOrderRepository _repository;
    
    public OrderService(
        IQueuePublisher<ProcessOrderCommand> queuePublisher,
        IOrderRepository repository)
    {
        _queuePublisher = queuePublisher;
        _repository = repository;
    }
    
    public async Task CreateOrderAsync(Order order)
    {
        // Save order to database
        await _repository.SaveAsync(order);
        
        // Queue processing command (only one processor will handle this)
        var command = new ProcessOrderCommand
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Items = order.Items,
            TotalAmount = order.TotalAmount,
            TraceId = Activity.Current?.Id ?? ActivityTraceId.CreateRandom().ToString(),
            ReferenceId = order.Id.ToString(),
            CreatedTimestamp = DateTime.UtcNow,
            FailureCount = 0
        };
        
        // Optionally add metadata
        var metadata = new Dictionary<string, object>
        {
            { "Priority", "Normal" },
            { "Source", "OrderService" }
        };
        
        await _queuePublisher.PublishAsync(command, metadata);
    }
}
```

#### 3. Subscriber Service (Only One Processor Per Message)

```csharp
public class OrderProcessor : IHostedService
{
    private readonly IQueueSubscriber<ProcessOrderCommand> _queueSubscriber;
    private readonly IOrderRepository _repository;
    private readonly IPaymentService _paymentService;
    private readonly IShippingService _shippingService;
    private readonly ILogger<OrderProcessor> _logger;
    
    public OrderProcessor(
        IQueueSubscriber<ProcessOrderCommand> queueSubscriber,
        IOrderRepository repository,
        IPaymentService paymentService,
        IShippingService shippingService,
        ILogger<OrderProcessor> logger)
    {
        _queueSubscriber = queueSubscriber;
        _repository = repository;
        _paymentService = paymentService;
        _shippingService = shippingService;
        _logger = logger;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _queueSubscriber.SubscribeAsync(
            async (command, metadata, ct) =>
            {
                _logger.LogInformation("Processing order {OrderId}", command.OrderId);
                
                var order = await _repository.GetByIdAsync(command.OrderId);
                if (order == null)
                {
                    throw new InvalidOperationException($"Order {command.OrderId} not found");
                }
                
                // Process payment
                await _paymentService.ProcessPaymentAsync(order);
                
                // Create shipping label
                await _shippingService.CreateShippingLabelAsync(order);
                
                // Update order status
                order.Status = OrderStatus.Processed;
                await _repository.UpdateAsync(order);
                
                _logger.LogInformation("Order {OrderId} processed successfully", command.OrderId);
            },
            maxFailureCount: 3,
            onPermanentFailure: async (command, ex, metadata, ct) =>
            {
                _logger.LogError(
                    ex,
                    "Order processing permanently failed. OrderId: {OrderId}, TraceId: {TraceId}",
                    command.OrderId,
                    command.TraceId);
                
                // Update order status to failed
                var order = await _repository.GetByIdAsync(command.OrderId);
                if (order != null)
                {
                    order.Status = OrderStatus.Failed;
                    order.FailureReason = ex.Message;
                    await _repository.UpdateAsync(order);
                }
                
                // Could also send to dead letter queue or alert operations
            },
            cancellationToken);
    }
    
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return _queueSubscriber.UnsubscribeAsync(cancellationToken);
    }
}
```

### Registering Hosted Services

In `Program.cs`:

```csharp
// Register hosted services that subscribe to messages
builder.Services.AddHostedService<EmailService>();
builder.Services.AddHostedService<AnalyticsService>();
builder.Services.AddHostedService<OrderProcessor>();
```

## Advanced Configuration

### Name Transformation and Sanitization

Message type names are automatically transformed and sanitized to ensure they are valid RabbitMQ queue and exchange names. The transformation process follows these steps:

1. **Base Name Resolution**: The message type name is resolved using the configured naming strategy (default: kebab-case)
2. **Sanitization**: The name is sanitized to comply with RabbitMQ naming rules

#### RabbitMQ Naming Rules

RabbitMQ queue and exchange names must:
- Contain only: letters (a-z, A-Z), digits (0-9), hyphens (-), underscores (_), periods (.), and colons (:)
- Not exceed 255 bytes (UTF-8 encoded)
- Not start or end with periods, hyphens, or underscores
- Not be empty

#### Transformation Examples

The following examples show how message type names are transformed:

**Basic PascalCase to kebab-case:**
```
UserCreatedEvent          → user-created-event
ProcessOrderCommand       → process-order-command
SendEmailNotification     → send-email-notification
```

**Names with invalid characters:**
```
User/Created/Event        → user-created-event        (slashes replaced)
Order#123                 → order-123                 (hash replaced)
Message*Test              → message-test              (asterisk replaced)
User@Created              → user-created              (at sign replaced)
```

**Names with consecutive separators:**
```
User---Created            → user-created              (consecutive hyphens removed)
Order___Processed         → order-processed            (consecutive underscores removed)
```

**Names with leading/trailing invalid characters:**
```
.user-created.            → user-created              (leading/trailing periods removed)
-test-                    → test                      (leading/trailing hyphens removed)
_user_created_            → user-created              (leading/trailing underscores removed)
```

**Names with spaces:**
```
User Created Event        → user-created-event        (spaces replaced)
Process Order Command     → process-order-command      (spaces replaced)
```

**Full type names (with namespace):**
```
MyApp.Events.UserCreatedEvent     → myapp.events.user-created-event
MyApp.Commands.ProcessOrder        → myapp.commands.process-order
```

**Long names (truncated to 255 bytes):**
```
VeryLongMessageTypeNameThatExceedsTheMaximumAllowedLengthForRabbitMQQueueAndExchangeNames...
→ truncated to 255 bytes (UTF-8 aware)
```

**Empty or invalid names:**
```
""                        → default                   (empty names defaulted)
"   "                     → default                   (whitespace-only defaulted)
```

#### Custom Name Resolvers

You can customize how topic and queue names are resolved:

```csharp
// Custom topic name resolver
services.AddSingleton<ITopicNameResolver>(sp =>
{
    var baseResolver = new DefaultTopicNameResolver(
        type => $"events.{type.Name.ToLowerInvariant()}");
    return new RabbitMqTopicNameResolver(baseResolver);
});

// Custom queue name resolver
services.AddSingleton<IQueueNameResolver>(sp =>
{
    var baseResolver = new DefaultQueueNameResolver(
        DefaultQueueNameResolver.FullTypeNameStrategy);
    return new RabbitMqQueueNameResolver(baseResolver);
});
```

**Note:** Even with custom resolvers, names are still sanitized to ensure RabbitMQ compatibility. The sanitization happens automatically in the RabbitMQ-specific resolvers.

### Publishing with Metadata

Both topic and queue publishers support metadata:

```csharp
var metadata = new Dictionary<string, object>
{
    { "Source", "UserService" },
    { "Version", "1.0" },
    { "Priority", "High" },
    { "RetryPolicy", "ExponentialBackoff" }
};

await _publisher.PublishAsync(message, metadata);
```

The metadata is stored in RabbitMQ message headers and passed to subscribers.

## Connection Management

The `RabbitMqConnectionManager` handles connection lifecycle:

- **Singleton**: One connection manager per application instance
- **Auto-Recovery**: Automatic connection recovery enabled (reconnects after network failures)
- **Connection Pooling**: Reuses a single connection across all publishers/subscribers
- **Channel Management**: Creates channels as needed (one per operation, disposed after use)
- **Thread-Safe**: Safe to use from multiple threads concurrently

### Connection Lifecycle

1. Connection is created lazily on first use
2. Connection is reused for all subsequent operations
3. Automatic recovery handles network interruptions
4. Connection is disposed when application shuts down

## Best Practices

1. **Use Environment Variables**: Store connection strings in environment variables for security
2. **Handle Permanent Failures**: Always implement `onPermanentFailure` callback
3. **Set Appropriate Retry Limits**: Choose `maxFailureCount` based on message importance
4. **Monitor Logs**: Watch for permanent failure logs to identify issues
5. **Use Topics for Events**: Use topics when multiple subscribers need the same message
6. **Use Queues for Commands**: Use queues when only one processor should handle each message
7. **Set TraceId**: Always set `TraceId` for distributed tracing
8. **Initialize Timestamps**: Always set `CreatedTimestamp` to `DateTime.UtcNow`

## Troubleshooting

### Connection Issues

If you see connection errors:
1. Verify RabbitMQ is running: `docker ps` or check service status
2. Check connection string format
3. Verify credentials and permissions
4. Check network connectivity

### Message Not Received

1. Verify queue/exchange exists in RabbitMQ management UI
2. Check consumer is subscribed and running
3. Verify message type matches subscriber type
4. Check logs for deserialization errors

### Permanent Failures

1. Check application logs for error details
2. Review `onPermanentFailure` callback implementation
3. Verify message structure matches expected type
4. Check for infrastructure issues (database, external APIs)

## RabbitMQ Management

Access RabbitMQ Management UI (if enabled):
- URL: `http://localhost:15672`
- Default credentials: `guest` / `guest`

Useful for:
- Viewing queues and exchanges
- Monitoring message rates
- Inspecting message contents
- Managing connections
