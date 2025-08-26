using System;
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;
using NLog;
using Services;

/// <summary>
/// RPC-style client for interacting with the Gas Pressure service over RabbitMQ.
/// Handles operations such as getting pressure, checking if destroyed, and adjusting mass.
/// </summary>
class GasPressureClient : IGasPressureService
{
    /// <summary>
    /// Name of the RabbitMQ message exchange for the Gas Pressure service.
    /// </summary>
    private static readonly string ExchangeName = "GasPressure.Exchange";

    /// <summary>
    /// Name of the server queue for Gas Pressure service requests.
    /// </summary>
    private static readonly string ServerQueueName = "GasPressure.Service";

    /// <summary>
    /// Prefix for client-specific queue names.
    /// </summary>
    private static readonly string ClientQueueNamePrefix = "GasPressure.InputClient_";

    /// <summary>
    /// Logger instance for this class.
    /// </summary>
    private readonly Logger log = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The name of the client-specific queue.
    /// </summary>
    private string ClientQueueName { get; }

    /// <summary>
    /// Connection to the RabbitMQ server.
    /// </summary>
    private IConnection rmqConn;

    /// <summary>
    /// Communication channel with RabbitMQ.
    /// </summary>
    private IModel rmqChann;

    /// <summary>
    /// Initializes a new instance of the <see cref="GasPressureClient"/> class.
    /// Connects to RabbitMQ and sets up the exchange and client queue.
    /// </summary>
    public GasPressureClient()
    {
        // Generate a unique client queue name for this instance
        ClientQueueName = ClientQueueNamePrefix + Guid.NewGuid();

        // Log the initialization for traceability
        log.Info($"Initializing GasPressureClient with queue '{ClientQueueName}'.");

        // Set up connection to RabbitMQ
        var rmqConnFact = new ConnectionFactory();
        rmqConn = rmqConnFact.CreateConnection();

        // Set up channel, declare exchange, and declare client-specific queue
        rmqChann = rmqConn.CreateModel();
        rmqChann.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Direct);
        rmqChann.QueueDeclare(queue: ClientQueueName, durable: false, exclusive: true, autoDelete: false, arguments: null);
        rmqChann.QueueBind(queue: ClientQueueName, exchange: ExchangeName, routingKey: ClientQueueName);
    }

    /// <summary>
    /// Generic RPC method to call remote methods on the server.
    /// </summary>
    /// <typeparam name="RESULT">The expected result type.</typeparam>
    /// <param name="methodName">Name of the method to invoke on the server.</param>
    /// <param name="requestDataProvider">Optional function to provide request data as a JSON string.</param>
    /// <param name="resultDataExtractor">Function to extract and parse the result from the server response.</param>
    /// <returns>The result from the server, if any.</returns>
    private RESULT Call<RESULT>(
        string methodName,
        Func<string> requestDataProvider,
        Func<string, RESULT> resultDataExtractor
    )
    {
        // Validate method name
        if (methodName == null)
            throw new ArgumentException("Argument 'methodName' is null.");

        // Initialize result and completion signal
        RESULT result = default;
        var isResultReady = false;
        var resultReadySignal = new AutoResetEvent(false);

        // Create the RPC request
        var request = new RPCMessage
        {
            Action = $"Call_{methodName}",
            Data = requestDataProvider != null ? requestDataProvider() : null
        };

        var requestProps = rmqChann.CreateBasicProperties();
        requestProps.CorrelationId = Guid.NewGuid().ToString();
        requestProps.ReplyTo = ClientQueueName;

        // Set up a response consumer if expecting a result
        string consumerTag = null;
        if (resultDataExtractor != null)
        {
            // Ensure memory barriers for thread-safe access
            Thread.MemoryBarrier();

            var consumer = new EventingBasicConsumer(rmqChann);
            consumer.Received += (channel, delivery) =>
            {
                // Ensure thread memory consistency
                Thread.MemoryBarrier();

                // Check if the response matches the correlation ID
                if (!isResultReady && delivery.BasicProperties.CorrelationId == requestProps.CorrelationId)
                {
                    var response = JsonConvert.DeserializeObject<RPCMessage>(Encoding.UTF8.GetString(delivery.Body.ToArray()));
                    if (response.Action == $"Result_{methodName}")
                    {
                        // Extract result and signal completion
                        result = resultDataExtractor(response.Data);
                        isResultReady = true;

                        // Ensure memory consistency for result retrieval
                        Thread.MemoryBarrier();

                        // Signal main thread that result is ready
                        resultReadySignal.Set();
                    }
                    else
                    {
                        log.Info($"Unexpected RPC action '{response.Action}' received. Ignoring.");
                    }
                }
            };
            consumerTag = rmqChann.BasicConsume(ClientQueueName, true, consumer);
        }

        // Send the request to the server
        rmqChann.BasicPublish(
            exchange: ExchangeName,
            routingKey: ServerQueueName,
            basicProperties: requestProps,
            body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request))
        );

        // Wait for the response if expecting a result
        if (resultDataExtractor != null)
        {
            resultReadySignal.WaitOne();

            // Ensure memory consistency before accessing the result
            Thread.MemoryBarrier();

            rmqChann.BasicCancel(consumerTag);
        }

        return result;
    }

    /// <summary>
    /// Gets the current gas pressure from the server.
    /// </summary>
    /// <returns>The current pressure value.</returns>
    public double GetPressure() =>
        Call(
            nameof(GetPressure),
            null,
            data => JsonConvert.DeserializeAnonymousType(data, new { Value = 0.0 }).Value
        );

    /// <summary>
    /// Checks if the gas container is destroyed.
    /// </summary>
    /// <returns>True if the container is destroyed; otherwise, false.</returns>
    public bool IsDestroyed() =>
        Call(
            nameof(IsDestroyed),
            null,
            data => JsonConvert.DeserializeAnonymousType(data, new { Value = false }).Value
        );

    /// <summary>
    /// Requests to increase the gas mass by a specified amount.
    /// </summary>
    /// <param name="mass">The amount of mass to add.</param>
    /// <returns>A result indicating success or failure of the mass adjustment.</returns>
    public MassAdjustmentResult IncreaseMass(double mass)
    {
        var result = Call(
            nameof(IncreaseMass),
            () => JsonConvert.SerializeObject(new { Mass = mass }),
            data => JsonConvert.DeserializeObject<MassAdjustmentResult>(data)
        );
        return result;
    }

    /// <summary>
    /// Requests to decrease the gas mass by a specified amount.
    /// </summary>
    /// <param name="mass">The amount of mass to remove.</param>
    /// <returns>A result indicating success or failure of the mass adjustment.</returns>
    public MassAdjustmentResult DecreaseMass(double mass)
    {
        var result = Call(
            nameof(DecreaseMass),
            () => JsonConvert.SerializeObject(new { Mass = mass }),
            data => JsonConvert.DeserializeObject<MassAdjustmentResult>(data)
        );
        return result;
    }
}
