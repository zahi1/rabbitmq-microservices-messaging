namespace Servers
{
    using System.Text;
    using RabbitMQ.Client;
    using RabbitMQ.Client.Events;
    using NLog;
    using Newtonsoft.Json;
    using Services;

    /// <summary>
    /// Gas Pressure Service for handling RPC calls related to gas container state management.
    /// </summary>
    class GasPressureService
    {
        /// <summary>
        /// Name of the RabbitMQ message exchange for the Gas Pressure service.
        /// </summary>
        private static readonly string ExchangeName = "GasPressure.Exchange";

        /// <summary>
        /// Name of the server queue for handling incoming requests.
        /// </summary>
        private static readonly string ServerQueueName = "GasPressure.Service";

        /// <summary>
        /// Logger instance for this class.
        /// </summary>
        private readonly Logger log = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Connection to the RabbitMQ message broker.
        /// </summary>
        private IConnection rmqConn;

        /// <summary>
        /// Communication channel with RabbitMQ.
        /// </summary>
        private IModel rmqChann;

        /// <summary>
        /// Logic handling gas pressure operations.
        /// </summary>
        private GasPressureLogic logic = new GasPressureLogic();

        /// <summary>
        /// Initializes a new instance of the <see cref="GasPressureService"/> class and sets up RabbitMQ connection.
        /// </summary>
        public GasPressureService()
        {
            // Connect to RabbitMQ broker
            var rmqConnFact = new ConnectionFactory();
            rmqConn = rmqConnFact.CreateConnection();

            // Configure exchange and queue
            rmqChann = rmqConn.CreateModel();
            rmqChann.ExchangeDeclare(exchange: ExchangeName, type: ExchangeType.Direct);
            rmqChann.QueueDeclare(queue: ServerQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            rmqChann.QueueBind(queue: ServerQueueName, exchange: ExchangeName, routingKey: ServerQueueName);

            // Set up message consumer
            var rmqConsumer = new EventingBasicConsumer(rmqChann);
            rmqConsumer.Received += (consumer, delivery) => OnMessageReceived(((EventingBasicConsumer)consumer).Model, delivery);
            rmqChann.BasicConsume(queue: ServerQueueName, autoAck: true, consumer: rmqConsumer);
        }

        /// <summary>
        /// Processes received messages and performs the requested action on the gas pressure logic.
        /// </summary>
        /// <param name="channel">Communication channel.</param>
        /// <param name="msgIn">Received message.</param>
        private void OnMessageReceived(IModel channel, BasicDeliverEventArgs msgIn)
        {
            try
            {
                // Deserialize the request
                var request = JsonConvert.DeserializeObject<RPCMessage>(Encoding.UTF8.GetString(msgIn.Body.ToArray()));
                RPCMessage response = null;

                // Process the call based on action type
                switch (request.Action)
                {
                    case $"Call_{nameof(logic.GetPressure)}":
                        {
                            var result = logic.GetPressure();
                            response = new RPCMessage
                            {
                                Action = $"Result_{nameof(logic.GetPressure)}",
                                Data = JsonConvert.SerializeObject(new { Value = result })
                            };
                            break;
                        }
                    case $"Call_{nameof(logic.IsDestroyed)}":
                        {
                            var result = logic.IsDestroyed();
                            response = new RPCMessage
                            {
                                Action = $"Result_{nameof(logic.IsDestroyed)}",
                                Data = JsonConvert.SerializeObject(new { Value = result })
                            };
                            break;
                        }
                    case $"Call_{nameof(logic.IncreaseMass)}":
                        {
                            var massIncrease = JsonConvert.DeserializeObject<dynamic>(request.Data).Mass;
                            logic.IncreaseMass((double)massIncrease);
                            response = new RPCMessage
                            {
                                Action = $"Result_{nameof(logic.IncreaseMass)}",
                                Data = JsonConvert.SerializeObject(new { Success = true })
                            };
                            break;
                        }
                    case $"Call_{nameof(logic.DecreaseMass)}":
                        {
                            var massDecrease = JsonConvert.DeserializeObject<dynamic>(request.Data).Mass;
                            logic.DecreaseMass((double)massDecrease);
                            response = new RPCMessage
                            {
                                Action = $"Result_{nameof(logic.DecreaseMass)}",
                                Data = JsonConvert.SerializeObject(new { Success = true })
                            };
                            break;
                        }
                    default:
                        {
                            log.Info($"Unsupported RPC action '{request.Action}'. Ignoring.");
                            break;
                        }
                }

                // Send a response if defined
                if (response != null)
                {
                    var msgOutProps = channel.CreateBasicProperties();
                    msgOutProps.CorrelationId = msgIn.BasicProperties.CorrelationId;

                    channel.BasicPublish(
                        exchange: ExchangeName,
                        routingKey: msgIn.BasicProperties.ReplyTo,
                        basicProperties: msgOutProps,
                        body: Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response))
                    );
                }
            }
            catch (Exception e)
            {
                log.Error(e, "Unhandled exception while processing a message. The message was not processed.");
            }
        }
    }
}
