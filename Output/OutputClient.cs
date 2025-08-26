using System;
using System.Text;
using System.Threading;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Newtonsoft.Json;
using NLog;
using Services;

namespace Clients
{
    /// <summary>
    /// RPC-style client for interacting with the Gas Pressure service over RabbitMQ for the Output system.
    /// Manages pressure by removing mass from the gas container when necessary.
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
        private static readonly string ClientQueueNamePrefix = "GasPressure.OutputClient_";

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
            log.Info($"Initializing Output GasPressureClient with queue '{ClientQueueName}'.");

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
                var consumer = new EventingBasicConsumer(rmqChann);
                consumer.Received += (channel, delivery) =>
                {
                    // Ensure thread synchronization
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
                            Thread.MemoryBarrier(); // Ensure synchronization
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
                rmqChann.BasicCancel(consumerTag);
            }

            return result;
        }

        /// <summary>
        /// Gets the current gas pressure from the server.
        /// </summary>
        public double GetPressure()
        {
            var result = Call(
                nameof(GetPressure),
                null,
                data => JsonConvert.DeserializeAnonymousType(data, new { Value = 0.0 }).Value
            );
            return result;
        }

        /// <summary>
        /// Checks if the gas container is destroyed.
        /// </summary>
        public bool IsDestroyed()
        {
            return Call(
                nameof(IsDestroyed),
                null,
                data => JsonConvert.DeserializeAnonymousType(data, new { Value = false }).Value
            );
        }

        /// <summary>
        /// Requests to increase the gas mass by a specified amount (not typically used in Output client but available if needed).
        /// </summary>
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
        /// Requests to decrease the gas mass by a specified amount when pressure is high.
        /// </summary>
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
}
