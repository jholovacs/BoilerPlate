# Service Bus Abstractions

This project provides abstractions and interfaces for service bus messaging, supporting both **topics** (pub/sub) and **queues** (point-to-point) messaging patterns.

## Table of Contents

- [Overview](#overview)
- [Message Interface](#message-interface)
- [Factory Pattern](#factory-pattern)
- [Null Implementations](#null-implementations)
- [Topic Publisher](#topic-publisher)
- [Topic Subscriber](#topic-subscriber)
- [Queue Publisher](#queue-publisher)
- [Queue Subscriber](#queue-subscriber)
- [Name Resolution](#name-resolution)
- [Failure Handling](#failure-handling)
- [Usage Examples](#usage-examples)

## Overview

The service bus abstractions provide a type-safe, generic interface for messaging. The key concepts are:

- **Topics**: Publish/subscribe pattern where multiple subscribers can receive the same message
- **Queues**: Point-to-point pattern where only one consumer processes each message
- **Message Types**: Both topics and queues are determined by the message type (generic type parameter)
- **IMessage Interface**: All messages must implement `IMessage` for tracking and failure handling

## Project Structure

The project is organized into the following directories:

```
BoilerPlate.ServiceBus.Abstractions/
├── Interfaces/              # All interface definitions (I*)
│   ├── IMessage.cs
│   ├── ITopicPublisher.cs
│   ├── ITopicSubscriber.cs
│   ├── ITopicSubscriberFactory.cs
│   ├── IQueuePublisher.cs
│   ├── IQueueSubscriber.cs
│   ├── IQueueSubscriberFactory.cs
│   ├── ITopicNameResolver.cs
│   └── IQueueNameResolver.cs
├── Resolvers/               # Name resolver implementations
│   ├── DefaultTopicNameResolver.cs
│   └── DefaultQueueNameResolver.cs
├── Implementations/
│   └── Null/                # Null/no-op implementations
│       ├── NullTopicPublisher.cs
│       ├── NullTopicSubscriber.cs
│       ├── NullTopicSubscriberFactory.cs
│       ├── NullQueuePublisher.cs
│       ├── NullQueueSubscriber.cs
│       └── NullQueueSubscriberFactory.cs
├── Helpers/                 # Utility classes
│   ├── MessageFailureHandler.cs
│   └── MessageProcessingResult.cs
└── Extensions/              # Extension methods
    └── ServiceCollectionExtensions.cs
```

## Message Interface

All messages must implement the `IMessage` interface, which provides common properties for message tracking and processing:

```csharp
public interface IMessage
{
    string? TraceId { get; set; }          // For distributed tracing
    string? ReferenceId { get; set; }      // For linking related messages
    DateTime CreatedTimestamp { get; set; } // When the message was created
    int FailureCount { get; set; }         // Number of processing failures
}
```

### Example Message Implementation

```csharp
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

## Factory Pattern

The service bus provides factory interfaces for creating subscribers on demand. This is useful when you need to subscribe to multiple message types, or when message types are determined at runtime.

**Note:** Publishers are non-generic and can be injected directly. Only subscribers use factories since they remain generic.

### Factory Interfaces

- `ITopicSubscriberFactory` - Creates topic subscribers
- `IQueueSubscriberFactory` - Creates queue subscribers

### Using Subscriber Factories

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

### Benefits of Subscriber Factories

1. **Single Dependency**: Inject one factory instead of multiple subscribers
2. **Dynamic Message Types**: Create subscribers for message types determined at runtime
3. **Reduced Constructor Parameters**: Avoid injecting many generic types in your services
4. **Lazy Creation**: Subscribers are created only when needed

### When to Use Subscriber Factories vs Direct Injection

**Use Subscriber Factories when:**
- You need to subscribe to multiple message types
- Message types are determined at runtime
- You want to reduce constructor parameters
- You have a service that handles multiple event types

**Use Direct Injection when:**
- You only subscribe to one message type
- Message type is known at compile time
- You prefer explicit dependencies

## Null Implementations

The abstractions project includes null/no-op implementations that can be used when you don't want to use an actual messaging service (like RabbitMQ). These implementations do nothing - they accept messages but don't send them anywhere.

### Available Null Implementations

- `NullTopicPublisher<TMessage>` - No-op topic publisher
- `NullTopicSubscriber<TMessage>` - No-op topic subscriber
- `NullQueuePublisher<TMessage>` - No-op queue publisher
- `NullQueueSubscriber<TMessage>` - No-op queue subscriber
- `NullTopicPublisherFactory` - Factory that creates null topic publishers
- `NullTopicSubscriberFactory` - Factory that creates null topic subscribers
- `NullQueuePublisherFactory` - Factory that creates null queue publishers
- `NullQueueSubscriberFactory` - Factory that creates null queue subscribers

### When to Use Null Implementations

**Use null implementations when:**
- Developing locally without a messaging service
- Testing code that uses service bus abstractions
- You want to disable messaging temporarily
- You're building a service that optionally supports messaging
- You want to avoid external dependencies during development

### Registering Null Implementations

#### Option 1: Using Extension Method (Recommended)

The easiest way is to use the `AddNullServiceBus()` extension method:

```csharp
using BoilerPlate.ServiceBus.Abstractions.Extensions;

// Register all null implementations at once
services.AddNullServiceBus();
```

#### Option 2: Manual Registration

You can also register null implementations manually:

```csharp
// Register null publishers (non-generic)
services.AddSingleton<ITopicPublisher, NullTopicPublisher>();
services.AddSingleton<IQueuePublisher, NullQueuePublisher>();

// Register null subscribers (generic)
services.AddScoped(typeof(ITopicSubscriber<>), typeof(NullTopicSubscriber<>));
services.AddScoped(typeof(IQueueSubscriber<>), typeof(NullQueueSubscriber<>));

// Register null subscriber factories
services.AddSingleton<ITopicSubscriberFactory, NullTopicSubscriberFactory>();
services.AddSingleton<IQueueSubscriberFactory, NullQueueSubscriberFactory>();
```

### Example: Conditional Registration

You can conditionally register null implementations based on configuration:

```csharp
using BoilerPlate.ServiceBus.Abstractions.Extensions;
using BoilerPlate.ServiceBus.RabbitMq.Extensions;

var useMessaging = configuration.GetValue<bool>("UseMessaging", false);

if (useMessaging)
{
    // Register RabbitMQ implementation
    services.AddRabbitMqServiceBus(configuration);
}
else
{
    // Register null implementations (no-op)
    services.AddNullServiceBus();
}
```

### Behavior

- **Publishers**: Accept messages but do nothing (no-op)
- **Subscribers**: Accept subscriptions but never receive messages (no-op)
- **All methods**: Return `Task.CompletedTask` immediately
- **No exceptions**: All operations succeed but have no effect

This allows your code to work without modification whether messaging is enabled or disabled.

## Topic Publisher

The `ITopicPublisher` interface allows publishing messages to topics. The topic name is automatically determined by the message type. Publishers are non-generic and use generic methods.

### Basic Usage

```csharp
public class UserService
{
    private readonly ITopicPublisher _topicPublisher;
    
    public UserService(ITopicPublisher topicPublisher)
    {
        _topicPublisher = topicPublisher;
    }
    
    public async Task CreateUserAsync(User user)
    {
        // Create the message
        var message = new UserCreatedEvent
        {
            UserId = user.Id,
            UserName = user.Name,
            Email = user.Email,
            TraceId = Activity.Current?.Id, // Use current trace context
            ReferenceId = user.Id.ToString(),
            CreatedTimestamp = DateTime.UtcNow,
            FailureCount = 0
        };
        
        // Publish to topic using generic method
        await _topicPublisher.PublishAsync(message);
    }
    
    public async Task CreateOrderAsync(Order order)
    {
        var message = new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TraceId = Activity.Current?.Id,
            ReferenceId = order.Id.ToString(),
            CreatedTimestamp = DateTime.UtcNow,
            FailureCount = 0
        };
        
        // Same publisher instance, different message type
        await _topicPublisher.PublishAsync(message);
    }
}
```

### Publishing with Metadata

You can include additional metadata (headers, properties) when publishing:

```csharp
var metadata = new Dictionary<string, object>
{
    { "Source", "UserService" },
    { "Version", "1.0" },
    { "Priority", "High" }
};

await _publisher.PublishAsync(message, metadata);
```

## Topic Subscriber

The `ITopicSubscriber<TMessage>` interface allows subscribing to messages from a topic. Multiple subscribers can receive the same message (pub/sub pattern).

### Basic Subscription (Direct Injection)

```csharp
public class UserEventHandler
{
    private readonly ITopicSubscriber<UserCreatedEvent> _subscriber;
    
    public UserEventHandler(ITopicSubscriber<UserCreatedEvent> subscriber)
    {
        _subscriber = subscriber;
    }
    
    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        await _subscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                // Process the message
                Console.WriteLine($"User created: {message.UserId}");
                await SendWelcomeEmailAsync(message.Email);
            },
            cancellationToken);
    }
}
```

### Basic Subscription (Factory Pattern)

```csharp
public class UserEventHandler
{
    private readonly ITopicSubscriberFactory _subscriberFactory;
    
    public UserEventHandler(ITopicSubscriberFactory subscriberFactory)
    {
        _subscriberFactory = subscriberFactory;
    }
    
    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        var subscriber = _subscriberFactory.CreateSubscriber<UserCreatedEvent>();
        
        await subscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                // Process the message
                Console.WriteLine($"User created: {message.UserId}");
                await SendWelcomeEmailAsync(message.Email);
            },
            cancellationToken);
    }
}
```

### Subscription with Failure Handling

For production use, always use the overload with failure handling:

```csharp
await _subscriber.SubscribeAsync(
    async (message, metadata, ct) =>
    {
        // Process the message
        // If this throws an exception, FailureCount will be incremented
        await ProcessUserCreatedEvent(message);
    },
    maxFailureCount: 5, // Allow up to 5 failures before permanent failure
    onPermanentFailure: async (message, exception, metadata, ct) =>
    {
        // Handle permanent failure
        // This is called when FailureCount exceeds maxFailureCount
        await _deadLetterService.SendAsync(message, exception);
        await _notificationService.NotifyAdminAsync(
            $"UserCreatedEvent permanently failed: {message.TraceId}");
    },
    cancellationToken);
```

## Queue Publisher

The `IQueuePublisher` interface allows publishing messages to queues. The queue name is automatically determined by the message type. Publishers are non-generic and use generic methods.

### Basic Usage

```csharp
public class OrderService
{
    private readonly IQueuePublisher _queuePublisher;
    
    public OrderService(IQueuePublisher queuePublisher)
    {
        _queuePublisher = queuePublisher;
    }
    
    public async Task QueueOrderProcessingAsync(Order order)
    {
        var message = new ProcessOrderCommand
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Items = order.Items,
            TraceId = Activity.Current?.Id,
            ReferenceId = order.Id.ToString(),
            CreatedTimestamp = DateTime.UtcNow,
            FailureCount = 0
        };
        
        // Publish to queue using generic method
        await _queuePublisher.PublishAsync(message);
    }
}
```

### Publishing with Metadata

```csharp
var metadata = new Dictionary<string, object>
{
    { "RetryPolicy", "ExponentialBackoff" },
    { "MaxRetries", "3" }
};

await _queuePublisher.PublishAsync(message, metadata);
```

## Queue Subscriber

The `IQueueSubscriber<TMessage>` interface allows subscribing to messages from a queue. Only one consumer processes each message (point-to-point pattern).

### Basic Subscription (Direct Injection)

```csharp
public class OrderProcessor
{
    private readonly IQueueSubscriber<ProcessOrderCommand> _queueSubscriber;
    
    public OrderProcessor(IQueueSubscriber<ProcessOrderCommand> queueSubscriber)
    {
        _queueSubscriber = queueSubscriber;
    }
    
    public async Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        await _queueSubscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                // Process the order
                await ProcessOrderAsync(message.OrderId);
            },
            cancellationToken);
    }
}
```

### Basic Subscription (Factory Pattern)

```csharp
public class OrderProcessor
{
    private readonly IQueueSubscriberFactory _queueSubscriberFactory;
    
    public OrderProcessor(IQueueSubscriberFactory queueSubscriberFactory)
    {
        _queueSubscriberFactory = queueSubscriberFactory;
    }
    
    public async Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        var subscriber = _queueSubscriberFactory.CreateSubscriber<ProcessOrderCommand>();
        
        await subscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                // Process the order
                await ProcessOrderAsync(message.OrderId);
            },
            cancellationToken);
    }
}
```

### Subscription with Failure Handling

Always use failure handling in production:

```csharp
await _queueSubscriber.SubscribeAsync(
    async (message, metadata, ct) =>
    {
        // Process the message
        await ProcessOrderAsync(message.OrderId);
    },
    maxFailureCount: 3, // Allow up to 3 failures
    onPermanentFailure: async (message, exception, metadata, ct) =>
    {
        // Handle permanent failure
        await _deadLetterQueue.SendAsync(message, exception);
        await _alertingService.SendAlertAsync(
            $"Order processing failed permanently: {message.ReferenceId}",
            exception);
    },
    cancellationToken);
```

## Name Resolution

Topic and queue names are automatically resolved from message types. You can customize the naming strategy using the name resolver interfaces.

### Default Naming Strategy

By default, message type names are converted to kebab-case:
- `UserCreatedEvent` → `"user-created-event"`
- `ProcessOrderCommand` → `"process-order-command"`

### Custom Naming Strategy

```csharp
// Use full type name with namespace
var topicResolver = new DefaultTopicNameResolver(DefaultTopicNameResolver.FullTypeNameStrategy);
// Result: "myapp.events.user-created-event"

// Use type name as-is
var queueResolver = new DefaultQueueNameResolver(DefaultQueueNameResolver.TypeNameAsIsStrategy);
// Result: "ProcessOrderCommand"

// Custom strategy
var customResolver = new DefaultTopicNameResolver(type => 
    $"events.{type.Name.ToLowerInvariant()}");
// Result: "events.usercreatedevent"
```

## Failure Handling

The subscriber implementations automatically handle failures:

1. **Exception Capture**: Any exception thrown by the handler is caught
2. **Failure Count Increment**: The message's `FailureCount` property is incremented
3. **Retry Logic**: If `FailureCount` is within limits, the message is retried (implementation-specific)
4. **Permanent Failure**: If `FailureCount` exceeds `maxFailureCount`:
   - Error is logged with full details
   - `onPermanentFailure` callback is invoked (if provided)
   - Message is destroyed (removed from queue/topic)

### Failure Handling Behavior

```csharp
// Handler throws exception on first attempt
// FailureCount: 0 → 1
// Result: Message retried (if FailureCount < maxFailureCount)

// Handler throws exception on second attempt
// FailureCount: 1 → 2
// Result: Message retried (if FailureCount < maxFailureCount)

// Handler throws exception on third attempt (maxFailureCount = 3)
// FailureCount: 2 → 3
// Result: Still retried (3 <= 3)

// Handler throws exception on fourth attempt
// FailureCount: 3 → 4
// Result: Permanent failure
//   - Error logged
//   - onPermanentFailure callback invoked
//   - Message destroyed
```

### Success Resets Failure Count

If a message is successfully processed after previous failures, the `FailureCount` is reset to 0:

```csharp
// Attempt 1: Failure (FailureCount = 1)
// Attempt 2: Failure (FailureCount = 2)
// Attempt 3: Success (FailureCount = 0) ✅
```

## Usage Examples

### Complete Example: User Registration with Event Publishing

```csharp
// 1. Define the message
public class UserRegisteredEvent : IMessage
{
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
    
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
}

// 2. Publisher service
public class UserRegistrationService
{
    private readonly ITopicPublisher<UserRegisteredEvent> _publisher;
    
    public UserRegistrationService(ITopicPublisher<UserRegisteredEvent> publisher)
    {
        _publisher = publisher;
    }
    
    public async Task RegisterUserAsync(string email, string password)
    {
        var user = await CreateUserAsync(email, password);
        
        var message = new UserRegisteredEvent
        {
            UserId = user.Id,
            Email = email,
            TraceId = Activity.Current?.Id,
            ReferenceId = user.Id.ToString(),
            CreatedTimestamp = DateTime.UtcNow,
            FailureCount = 0
        };
        
        await _publisher.PublishAsync(message);
    }
}

// 3. Subscriber service (Email Service)
public class EmailService
{
    private readonly ITopicSubscriber<UserRegisteredEvent> _subscriber;
    
    public EmailService(ITopicSubscriber<UserRegisteredEvent> subscriber)
    {
        _subscriber = subscriber;
    }
    
    public async Task StartListeningAsync()
    {
        await _subscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                await SendWelcomeEmailAsync(message.Email);
            },
            maxFailureCount: 3,
            onPermanentFailure: async (message, ex, metadata, ct) =>
            {
                await _logger.LogErrorAsync($"Failed to send welcome email: {ex.Message}");
            });
    }
}

// 4. Another subscriber (Analytics Service)
public class AnalyticsService
{
    private readonly ITopicSubscriber<UserRegisteredEvent> _subscriber;
    
    public AnalyticsService(ITopicSubscriber<UserRegisteredEvent> subscriber)
    {
        _subscriber = subscriber;
    }
    
    public async Task StartListeningAsync()
    {
        await _subscriber.SubscribeAsync(
            async (message, metadata, ct) =>
            {
                await TrackUserRegistrationAsync(message.UserId);
            },
            maxFailureCount: 5);
    }
}
```

### Complete Example: Order Processing with Queue

```csharp
// 1. Define the command message
public class ProcessOrderCommand : IMessage
{
    public string? TraceId { get; set; }
    public string? ReferenceId { get; set; }
    public DateTime CreatedTimestamp { get; set; }
    public int FailureCount { get; set; }
    
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public List<OrderItem> Items { get; set; } = new();
}

// 2. Publisher (Order Service)
public class OrderService
{
    private readonly IQueuePublisher<ProcessOrderCommand> _queuePublisher;
    
    public OrderService(IQueuePublisher<ProcessOrderCommand> queuePublisher)
    {
        _queuePublisher = queuePublisher;
    }
    
    public async Task CreateOrderAsync(Order order)
    {
        // Save order to database
        await _repository.SaveAsync(order);
        
        // Queue processing
        var command = new ProcessOrderCommand
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            Items = order.Items,
            TraceId = Activity.Current?.Id,
            ReferenceId = order.Id.ToString(),
            CreatedTimestamp = DateTime.UtcNow,
            FailureCount = 0
        };
        
        await _queuePublisher.PublishAsync(command);
    }
}

// 3. Subscriber (Order Processor)
public class OrderProcessor
{
    private readonly IQueueSubscriber<ProcessOrderCommand> _queueSubscriber;
    private readonly IOrderRepository _repository;
    private readonly IPaymentService _paymentService;
    private readonly IShippingService _shippingService;
    
    public OrderProcessor(
        IQueueSubscriber<ProcessOrderCommand> queueSubscriber,
        IOrderRepository repository,
        IPaymentService paymentService,
        IShippingService shippingService)
    {
        _queueSubscriber = queueSubscriber;
        _repository = repository;
        _paymentService = paymentService;
        _shippingService = shippingService;
    }
    
    public async Task StartProcessingAsync()
    {
        await _queueSubscriber.SubscribeAsync(
            async (command, metadata, ct) =>
            {
                var order = await _repository.GetByIdAsync(command.OrderId);
                
                // Process payment
                await _paymentService.ProcessPaymentAsync(order);
                
                // Create shipping label
                await _shippingService.CreateShippingLabelAsync(order);
                
                // Update order status
                order.Status = OrderStatus.Processed;
                await _repository.UpdateAsync(order);
            },
            maxFailureCount: 3,
            onPermanentFailure: async (command, exception, metadata, ct) =>
            {
                // Move to dead letter queue
                await _deadLetterQueue.SendAsync(command, exception);
                
                // Notify operations team
                await _alertingService.SendAlertAsync(
                    $"Order {command.OrderId} processing failed permanently",
                    exception);
            });
    }
}
```

## Best Practices

1. **Always implement IMessage**: All messages must implement `IMessage` for proper tracking
2. **Set TraceId**: Use `Activity.Current?.Id` or generate a new trace ID for distributed tracing
3. **Use Failure Handling**: Always use the `SubscribeAsync` overload with `maxFailureCount` in production
4. **Handle Permanent Failures**: Implement `onPermanentFailure` callback to handle messages that exceed retry limits
5. **Set CreatedTimestamp**: Always set `CreatedTimestamp` to `DateTime.UtcNow` when creating messages
6. **Initialize FailureCount**: Always initialize `FailureCount` to 0 for new messages
7. **Use Metadata Sparingly**: Only include essential metadata; avoid large payloads
8. **Topics vs Queues**: Use topics for events (multiple subscribers), queues for commands (single processor)

## Implementation Notes

These are abstractions - you'll need to implement them in a concrete messaging provider project (e.g., `BoilerPlate.ServiceBus.RabbitMq`). The implementation should:

- Use `MessageFailureHandler.ProcessWithFailureHandlingAsync` for failure handling
- Implement name resolution using `ITopicNameResolver` and `IQueueNameResolver`
- Handle message serialization/deserialization
- Manage connections and channels to the messaging broker
- Implement retry logic when `FailureCount` is within limits
- Destroy messages when `FailureCount` exceeds `maxFailureCount`
