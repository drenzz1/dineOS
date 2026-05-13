# RabbitMQ Event Flow

The backend publishes `OrderCreated` events to RabbitMQ after a POS order is saved.
A background consumer processes the queue and broadcasts the existing SignalR
`OrderCreated` notification to restaurant clients.

## Local Runtime

Start RabbitMQ with the backend Docker stack:

```bash
cd backend
docker compose up rabbitmq -d
```

Management UI:

| Setting | Value |
|---|---|
| URL | http://localhost:15672 |
| Username | `dineos` |
| Password | `dineos_dev` |
| AMQP endpoint | `localhost:5672` |

## Topology

| Type | Name | Purpose |
|---|---|---|
| Exchange | `dineos.events` | Topic exchange for backend domain events |
| Routing key | `orders.created` | Published after a successful order create |
| Queue | `dineos.orders.created.notifications` | Consumer queue for SignalR order-created notifications |
| Dead-letter exchange | `dineos.events.dlx` | Receives messages that fail processing after retries |
| Dead-letter queue | `dineos.orders.created.notifications.dlq` | Inspect failed `OrderCreated` messages |

## Flow

1. `OrderService.CreateOrderAsync` saves the order and items.
2. It publishes an `OrderCreatedMessage` with message id `order-created-{orderId}`.
3. `RabbitMqOrderCreatedConsumer` receives the message with manual acknowledgement.
4. `OrderCreatedMessageHandler` records the message id in `ProcessedMessages`.
5. The handler broadcasts the SignalR `OrderCreated` event.
6. Duplicate message ids are skipped.

Processing failures are retried by republishing the same payload with
`x-delivery-attempt` incremented. After `RabbitMq:MaxRetryAttempts`, the message
is dead-lettered to `dineos.orders.created.notifications.dlq`.

## Configuration

The main keys live under `RabbitMq`:

```json
{
  "RabbitMq": {
    "Enabled": true,
    "HostName": "localhost",
    "Port": 5672,
    "UserName": "dineos",
    "Password": "dineos_dev"
  }
}
```

Integration tests disable the hosted consumer with `RabbitMq:Enabled=false`.
