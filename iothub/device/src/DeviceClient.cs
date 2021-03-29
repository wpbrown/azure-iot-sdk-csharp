// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Client.Transport;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace Microsoft.Azure.Devices.Client
{
    /// <summary>
    /// Contains methods that a device can use to send messages to and receive from the service.
    /// </summary>
    /// <threadsafety static="true" instance="true" />
    public class DeviceClient : IDisposable
    {
        /// <summary>
        /// Default operation timeout.
        /// </summary>
        public const uint DefaultOperationTimeoutInMilliseconds = 4 * 60 * 1000;

        private DeviceClient(InternalClient internalClient)
        {
            InternalClient = internalClient ?? throw new ArgumentNullException(nameof(internalClient));

            if (InternalClient.IotHubConnectionString?.ModuleId != null)
            {
                throw new ArgumentException("A module Id was specified in the connection string - please use ModuleClient for modules.");
            }

            if (Logging.IsEnabled)
            {
                Logging.Associate(this, this, internalClient, nameof(DeviceClient));
            }
        }

        /// <summary>
        /// Creates a disposable DeviceClient from the specified parameters, that uses AMQP transport protocol.
        /// </summary>
        /// <param name="hostname">The fully-qualified DNS host name of IoT Hub</param>
        /// <param name="authenticationMethod">The authentication method that is used</param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient Create(string hostname, IAuthenticationMethod authenticationMethod, ClientOptions options = default)
        {
            return Create(() => ClientFactory.Create(hostname, authenticationMethod, options));
        }

        /// <summary>
        /// Create a disposable, AMQP DeviceClient from the specified parameters
        /// </summary>
        /// <param name="hostname">The fully-qualified DNS host name of IoT Hub</param>
        /// <param name="gatewayHostname">The fully-qualified DNS host name of Gateway</param>
        /// <param name="authenticationMethod">The authentication method that is used</param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient Create(string hostname, string gatewayHostname, IAuthenticationMethod authenticationMethod, ClientOptions options = default)
        {
            return Create(() => ClientFactory.Create(hostname, gatewayHostname, authenticationMethod, options));
        }

        /// <summary>
        /// Creates a disposable DeviceClient from the specified parameters
        /// </summary>
        /// <param name="hostname">The fully-qualified DNS host name of IoT Hub</param>
        /// <param name="authenticationMethod">The authentication method that is used</param>
        /// <param name="transportType">The transportType used (HTTP1, AMQP or MQTT), <see cref="TransportType"/></param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient Create(string hostname, IAuthenticationMethod authenticationMethod, TransportType transportType, ClientOptions options = default)
        {
            return Create(() => ClientFactory.Create(hostname, authenticationMethod, transportType, options));
        }

        /// <summary>
        /// Create a disposable DeviceClient from the specified parameters
        /// </summary>
        /// <param name="hostname">The fully-qualified DNS host name of IoT Hub</param>
        /// <param name="gatewayHostname">The fully-qualified DNS host name of Gateway</param>
        /// <param name="authenticationMethod">The authentication method that is used</param>
        /// <param name="transportType">The transportType used (Http1, AMQP or MQTT), <see cref="TransportType"/></param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient Create(string hostname, string gatewayHostname, IAuthenticationMethod authenticationMethod, TransportType transportType, ClientOptions options = default)
        {
            return Create(() => ClientFactory.Create(hostname, gatewayHostname, authenticationMethod, transportType, options));
        }

        /// <summary>
        /// Creates a disposable DeviceClient from the specified parameters
        /// </summary>
        /// <param name="hostname">The fully-qualified DNS host name of IoT Hub</param>
        /// <param name="authenticationMethod">The authentication method that is used</param>
        /// <param name="transportSettings">Prioritized list of transportTypes and their settings</param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient Create(string hostname, IAuthenticationMethod authenticationMethod,
            ITransportSettings[] transportSettings, ClientOptions options = default)
        {
            return Create(() => ClientFactory.Create(hostname, authenticationMethod, transportSettings, options));
        }

        /// <summary>
        /// Creates a disposable DeviceClient from the specified parameters
        /// </summary>
        /// <param name="hostname">The fully-qualified DNS host name of IoT Hub</param>
        /// <param name="gatewayHostname">The fully-qualified DNS host name of Gateway</param>
        /// <param name="authenticationMethod">The authentication method that is used</param>
        /// <param name="transportSettings">Prioritized list of transportTypes and their settings</param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient Create(string hostname, string gatewayHostname, IAuthenticationMethod authenticationMethod,
            ITransportSettings[] transportSettings, ClientOptions options = default)
        {
            return Create(() => ClientFactory.Create(hostname, gatewayHostname, authenticationMethod, transportSettings, options));
        }

        /// <summary>
        /// Creates a disposable DeviceClient using AMQP transport from the specified connection string
        /// </summary>
        /// <param name="connectionString">Connection string for the IoT hub (including DeviceId)</param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient CreateFromConnectionString(string connectionString, ClientOptions options = default)
        {
            return Create(() => ClientFactory.CreateFromConnectionString(connectionString, options));
        }

        /// <summary>
        /// Creates a disposable DeviceClient using AMQP transport from the specified connection string
        /// </summary>
        /// <param name="connectionString">IoT Hub-Scope Connection string for the IoT hub (without DeviceId)</param>
        /// <param name="deviceId">Id of the Device</param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient CreateFromConnectionString(string connectionString, string deviceId, ClientOptions options = default)
        {
            return Create(() => ClientFactory.CreateFromConnectionString(connectionString, deviceId, options));
        }

        /// <summary>
        /// Creates a disposable DeviceClient from the specified connection string using the specified transport type
        /// </summary>
        /// <param name="connectionString">Connection string for the IoT hub (including DeviceId)</param>
        /// <param name="transportType">Specifies whether Http1, AMQP or MQTT transport is used, <see cref="TransportType"/></param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient CreateFromConnectionString(string connectionString, TransportType transportType, ClientOptions options = default)
        {
            return Create(() => ClientFactory.CreateFromConnectionString(connectionString, transportType, options));
        }

        /// <summary>
        /// Creates a disposable DeviceClient from the specified connection string using the specified transport type
        /// </summary>
        /// <param name="connectionString">IoT Hub-Scope Connection string for the IoT hub (without DeviceId)</param>
        /// <param name="deviceId">Id of the device</param>
        /// <param name="transportType">The transportType used (Http1, AMQP or MQTT), <see cref="TransportType"/></param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient CreateFromConnectionString(string connectionString, string deviceId, TransportType transportType, ClientOptions options = default)
        {
            return Create(() => ClientFactory.CreateFromConnectionString(connectionString, deviceId, transportType, options));
        }

        /// <summary>
        /// Create a disposable DeviceClient from the specified connection string using a prioritized list of transports
        /// </summary>
        /// <param name="connectionString">Connection string for the IoT hub (with DeviceId)</param>
        /// <param name="transportSettings">Prioritized list of transports and their settings</param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient CreateFromConnectionString(string connectionString,
            ITransportSettings[] transportSettings, ClientOptions options = default)
        {
            return Create(() => ClientFactory.CreateFromConnectionString(connectionString, transportSettings, options));
        }

        /// <summary>
        /// Creates a disposable DeviceClient from the specified connection string using the prioritized list of transports
        /// </summary>
        /// <param name="connectionString">Connection string for the IoT hub (without DeviceId)</param>
        /// <param name="deviceId">Id of the device</param>
        /// <param name="transportSettings">Prioritized list of transportTypes and their settings</param>
        /// <param name="options">The options that allow configuration of the device client instance during initialization.</param>
        /// <returns>A disposable DeviceClient instance</returns>
        public static DeviceClient CreateFromConnectionString(string connectionString, string deviceId,
            ITransportSettings[] transportSettings, ClientOptions options = default)
        {
            return Create(() => ClientFactory.CreateFromConnectionString(connectionString, deviceId, transportSettings, options));
        }

        private static DeviceClient Create(Func<InternalClient> internalClientCreator)
        {
            return new DeviceClient(internalClientCreator());
        }

        internal IDelegatingHandler InnerHandler
        {
            get => InternalClient.InnerHandler;
            set => InternalClient.InnerHandler = value;
        }

        internal InternalClient InternalClient { get; private set; }

        /// <summary>
        /// Diagnostic sampling percentage value, [0-100];
        /// 0 means no message will carry on diagnostics info
        /// </summary>
        public int DiagnosticSamplingPercentage
        {
            get => InternalClient.DiagnosticSamplingPercentage;
            set => InternalClient.DiagnosticSamplingPercentage = value;
        }

        /// <summary>
        /// Stores the timeout used in the operation retries. Note that this value is ignored for operations
        /// where a cancellation token is provided. For example, <see cref="SendEventAsync(Message)"/> will use this timeout, but
        /// <see cref="SendEventAsync(Message, CancellationToken)"/> will not. The latter operation will only be canceled by the
        /// provided cancellation token.
        /// </summary>
        // Codes_SRS_DEVICECLIENT_28_002: [This property shall be defaulted to 240000 (4 minutes).]
        public uint OperationTimeoutInMilliseconds
        {
            get => InternalClient.OperationTimeoutInMilliseconds;
            set => InternalClient.OperationTimeoutInMilliseconds = value;
        }

        /// <summary>
        /// Stores custom product information that will be appended to the user agent string that is sent to IoT Hub.
        /// </summary>
        public string ProductInfo
        {
            get => InternalClient.ProductInfo;
            set => InternalClient.ProductInfo = value;
        }

        /// <summary>
        /// Stores the retry strategy used in the operation retries.
        /// </summary>
        // Codes_SRS_DEVICECLIENT_28_001: [This property shall be defaulted to the exponential retry strategy with back-off
        // parameters for calculating delay in between retries.]
        [Obsolete("This method has been deprecated.  Please use Microsoft.Azure.Devices.Client.SetRetryPolicy(IRetryPolicy retryPolicy) instead.")]
        public RetryPolicyType RetryPolicy
        {
            get => InternalClient.RetryPolicy;
            set => InternalClient.RetryPolicy = value;
        }

        /// <summary>
        /// Sets the retry policy used in the operation retries.
        /// The change will take effect after any in-progress operations.
        /// </summary>
        /// <param name="retryPolicy">The retry policy. The default is
        /// <c>new ExponentialBackoff(int.MaxValue, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(100));</c></param>
        // Codes_SRS_DEVICECLIENT_28_001: [This property shall be defaulted to the exponential retry strategy with back-off
        // parameters for calculating delay in between retries.]
        public void SetRetryPolicy(IRetryPolicy retryPolicy)
        {
            InternalClient.SetRetryPolicy(retryPolicy);
        }

        /// <summary>
        /// Explicitly open the DeviceClient instance.
        /// </summary>
        public Task OpenAsync() => InternalClient.OpenAsync();

        /// <summary>
        /// Explicitly open the DeviceClient instance.
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// </summary>
        public Task OpenAsync(CancellationToken cancellationToken) => InternalClient.OpenAsync(cancellationToken);

        /// <summary>
        /// Close the DeviceClient instance
        /// </summary>
        public Task CloseAsync() => InternalClient.CloseAsync();

        /// <summary>
        /// Close the DeviceClient instance
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns></returns>
        public Task CloseAsync(CancellationToken cancellationToken) => InternalClient.CloseAsync(cancellationToken);

        /// <summary>
        /// Receive a message from the device queue using the default timeout.
        /// After handling a received message, a client should call <see cref="CompleteAsync(Message)"/>,
        /// <see cref="AbandonAsync(Message)"/>, or <see cref="RejectAsync(Message)"/>, and then dispose the message.
        /// </summary>
        /// <remarks>
        /// You cannot Reject or Abandon messages over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <returns>The receive message or null if there was no message until the default timeout</returns>
        public Task<Message> ReceiveAsync() => InternalClient.ReceiveAsync();

        /// <summary>
        /// Receive a message from the device queue using the cancellation token.
        /// After handling a received message, a client should call <see cref="CompleteAsync(Message, CancellationToken)"/>,
        /// <see cref="AbandonAsync(Message, CancellationToken)"/>, or <see cref="RejectAsync(Message, CancellationToken)"/>, and then dispose the message.
        /// </summary>
        /// <remarks>
        /// You cannot Reject or Abandon messages over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The receive message or null if there was no message until CancellationToken Expired</returns>
        public Task<Message> ReceiveAsync(CancellationToken cancellationToken) => InternalClient.ReceiveAsync(cancellationToken);

        /// <summary>
        /// Receive a message from the device queue using the cancellation token.
        /// After handling a received message, a client should call <see cref="CompleteAsync(Message, CancellationToken)"/>,
        /// <see cref="AbandonAsync(Message, CancellationToken)"/>, or <see cref="RejectAsync(Message, CancellationToken)"/>, and then dispose the message.
        /// </summary>
        /// <remarks>
        /// You cannot Reject or Abandon messages over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <returns>The receive message or null if there was no message until the specified time has elapsed</returns>
        public Task<Message> ReceiveAsync(TimeSpan timeout) => InternalClient.ReceiveAsync(timeout);

        /// <summary>
        /// Sets a new delegate for receiving a message from the device queue using the default timeout.
        /// After handling a received message, a client should call <see cref="CompleteAsync(Message, CancellationToken)"/>,
        /// <see cref="AbandonAsync(Message, CancellationToken)"/>, or <see cref="RejectAsync(Message, CancellationToken)"/>, and then dispose the message.
        /// If a null delegate is passed, it will disable the callback triggered on receiving messages from the service.
        /// <param name="messageHandler">The delegate to be used when a could to device message is received by the client.</param>
        /// <param name="userContext">Generic parameter to be interpreted by the client code.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// </summary>
        public Task SetReceiveMessageHandlerAsync(ReceiveMessageCallback messageHandler, object userContext, CancellationToken cancellationToken = default) =>
            InternalClient.SetReceiveMessageHandlerAsync(messageHandler, userContext, cancellationToken);

        /// <summary>
        /// Deletes a received message from the device queue
        /// </summary>
        /// <param name="lockToken">The message lockToken.</param>
        /// <returns>The lock identifier for the previously received message</returns>
        public Task CompleteAsync(string lockToken) => InternalClient.CompleteAsync(lockToken);

        /// <summary>
        /// Deletes a received message from the device queue
        /// </summary>
        /// <param name="lockToken">The message lockToken.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>The lock identifier for the previously received message</returns>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        public Task CompleteAsync(string lockToken, CancellationToken cancellationToken) => InternalClient.CompleteAsync(lockToken, cancellationToken);

        /// <summary>
        /// Deletes a received message from the device queue
        /// </summary>
        /// <param name="message">The message.</param>
        /// <returns>The previously received message</returns>
        public Task CompleteAsync(Message message) => InternalClient.CompleteAsync(message);

        /// <summary>
        /// Deletes a received message from the device queue
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The previously received message</returns>
        public Task CompleteAsync(Message message, CancellationToken cancellationToken) => InternalClient.CompleteAsync(message, cancellationToken);

        /// <summary>
        /// Puts a received message back onto the device queue
        /// </summary>
        /// <remarks>
        /// You cannot Abandon a message over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <param name="lockToken">The message lockToken.</param>
        /// <returns>The previously received message</returns>
        public Task AbandonAsync(string lockToken) => InternalClient.AbandonAsync(lockToken);

        /// <summary>
        /// Puts a received message back onto the device queue
        /// </summary>
        /// <remarks>
        /// You cannot Abandon a message over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <param name="lockToken">The message lockToken.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The previously received message</returns>
        public Task AbandonAsync(string lockToken, CancellationToken cancellationToken) => InternalClient.AbandonAsync(lockToken, cancellationToken);

        /// <summary>
        /// Puts a received message back onto the device queue
        /// </summary>
        /// <remarks>
        /// You cannot Abandon a message over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <returns>The lock identifier for the previously received message</returns>
        public Task AbandonAsync(Message message) => InternalClient.AbandonAsync(message);

        /// <summary>
        /// Puts a received message back onto the device queue
        /// </summary>
        /// <remarks>
        /// You cannot Abandon a message over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The lock identifier for the previously received message</returns>
        public Task AbandonAsync(Message message, CancellationToken cancellationToken) => InternalClient.AbandonAsync(message, cancellationToken);

        /// <summary>
        /// Deletes a received message from the device queue and indicates to the server that the message could not be processed.
        /// </summary>
        /// <remarks>
        /// You cannot Reject a message over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <param name="lockToken">The message lockToken.</param>
        /// <returns>The previously received message</returns>
        public Task RejectAsync(string lockToken) => InternalClient.RejectAsync(lockToken);

        /// <summary>
        /// Deletes a received message from the device queue and indicates to the server that the message could not be processed.
        /// </summary>
        /// <remarks>
        /// You cannot Reject a message over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <param name="lockToken">The message lockToken.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The previously received message</returns>
        public Task RejectAsync(string lockToken, CancellationToken cancellationToken) => InternalClient.RejectAsync(lockToken, cancellationToken);

        /// <summary>
        /// Deletes a received message from the device queue and indicates to the server that the message could not be processed.
        /// </summary>
        /// <remarks>
        /// You cannot Reject a message over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <returns>The lock identifier for the previously received message</returns>
        public Task RejectAsync(Message message) => InternalClient.RejectAsync(message);

        /// <summary>
        /// Deletes a received message from the device queue and indicates to the server that the message could not be processed.
        /// </summary>
        /// <remarks>
        /// You cannot Reject a message over MQTT protocol.
        /// For more details, see https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-messages-c2d#the-cloud-to-device-message-life-cycle.
        /// </remarks>
        /// <param name="message">The message.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The lock identifier for the previously received message</returns>
        public Task RejectAsync(Message message, CancellationToken cancellationToken) => InternalClient.RejectAsync(message, cancellationToken);

        /// <summary>
        /// Sends an event to a hub
        /// </summary>
        /// <param name="message">The message to send. Should be disposed after sending.</param>
        /// <returns>The task to await</returns>
        public Task SendEventAsync(Message message) => InternalClient.SendEventAsync(message);

        /// <summary>
        /// Sends an event to a hub
        /// </summary>
        /// <param name="message">The message to send. Should be disposed after sending.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The task to await</returns>
        public Task SendEventAsync(Message message, CancellationToken cancellationToken) => InternalClient.SendEventAsync(message, cancellationToken);

        /// <summary>
        /// Sends a batch of events to IoT hub. Use AMQP or HTTPs for a true batch operation. MQTT will just send the messages one after the other.
        /// </summary>
        /// <param name="messages">A list of one or more messages to send. The messages should be disposed after sending.</param>
        /// <returns>The task to await</returns>
        public Task SendEventBatchAsync(IEnumerable<Message> messages) => InternalClient.SendEventBatchAsync(messages);

        /// <summary>
        /// Sends a batch of events to IoT hub. Use AMQP or HTTPs for a true batch operation. MQTT will just send the messages one after the other.
        /// </summary>
        /// <param name="messages">An IEnumerable set of Message objects.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The task to await</returns>
        public Task SendEventBatchAsync(IEnumerable<Message> messages, CancellationToken cancellationToken) => InternalClient.SendEventBatchAsync(messages, cancellationToken);

        /// <summary>
        /// Uploads a stream to a block blob in a storage account associated with the IoTHub for that device.
        /// If the blob already exists, it will be overwritten.
        /// </summary>
        /// <param name="blobName">The name of the blob to upload.</param>
        /// <param name="source">A stream with blob contents. Should be disposed after upload completes.</param>
        /// <returns>AsncTask</returns>
        [Obsolete("This API has been split into three APIs: GetFileUploadSasUri, uploading to blob directly using the Azure Storage SDK, and CompleteFileUploadAsync")]
        public Task UploadToBlobAsync(string blobName, Stream source) => InternalClient.UploadToBlobAsync(blobName, source);

        /// <summary>
        /// Uploads a stream to a block blob in a storage account associated with the IoTHub for that device.
        /// If the blob already exists, it will be overwritten.
        /// </summary>
        /// <param name="blobName">The name of the blob to upload</param>
        /// <param name="source">A stream with blob contents.. Should be disposed after upload completes.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The task to await</returns>
        [Obsolete("This API has been split into three APIs: GetFileUploadSasUri, uploading to blob directly using the Azure Storage SDK, and CompleteFileUploadAsync")]
        public Task UploadToBlobAsync(string blobName, Stream source, CancellationToken cancellationToken) =>
            InternalClient.UploadToBlobAsync(blobName, source, cancellationToken);

        /// <summary>
        /// Get a file upload SAS URI which the Azure Storage SDK can use to upload a file to blob for this device. See <see href="https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-file-upload#initialize-a-file-upload">this documentation for more details</see>
        /// </summary>
        /// <param name="request">The request details for getting the SAS URI, including the destination blob name.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The file upload details to be used with the Azure Storage SDK in order to upload a file from this device.</returns>
        public Task<FileUploadSasUriResponse> GetFileUploadSasUriAsync(FileUploadSasUriRequest request, CancellationToken cancellationToken = default) =>
            InternalClient.GetFileUploadSasUriAsync(request, cancellationToken);

        /// <summary>
        /// Notify IoT Hub that a device's file upload has finished. See <see href="https://docs.microsoft.com/en-us/azure/iot-hub/iot-hub-devguide-file-upload#notify-iot-hub-of-a-completed-file-upload">this documentation for more details</see>
        /// </summary>
        /// <param name="notification">The notification details, including if the file upload succeeded.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The task to await.</returns>
        public Task CompleteFileUploadAsync(FileUploadCompletionNotification notification, CancellationToken cancellationToken = default) =>
            InternalClient.CompleteFileUploadAsync(notification, cancellationToken);

        /// <summary>
        /// Sets a new delegate for the named method. If a delegate is already associated with
        /// the named method, it will be replaced with the new delegate.
        /// A method handler can be unset by passing a null MethodCallback.
        /// <param name="methodName">The name of the method to associate with the delegate.</param>
        /// <param name="methodHandler">The delegate to be used when a method with the given name is called by the cloud service.</param>
        /// <param name="userContext">generic parameter to be interpreted by the client code.</param>
        /// </summary>
        public Task SetMethodHandlerAsync(string methodName, MethodCallback methodHandler, object userContext) =>
            InternalClient.SetMethodHandlerAsync(methodName, methodHandler, userContext);

        /// <summary>
        /// Sets a new delegate for the named method. If a delegate is already associated with
        /// the named method, it will be replaced with the new delegate.
        /// A method handler can be unset by passing a null MethodCallback.
        /// <param name="methodName">The name of the method to associate with the delegate.</param>
        /// <param name="methodHandler">The delegate to be used when a method with the given name is called by the cloud service.</param>
        /// <param name="userContext">generic parameter to be interpreted by the client code.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// </summary>
        public Task SetMethodHandlerAsync(string methodName, MethodCallback methodHandler, object userContext, CancellationToken cancellationToken) =>
            InternalClient.SetMethodHandlerAsync(methodName, methodHandler, userContext, cancellationToken);

        /// <summary>
        /// Sets a new delegate that is called for a method that doesn't have a delegate registered for its name.
        /// If a default delegate is already registered it will replace with the new delegate.
        /// A method handler can be unset by passing a null MethodCallback.
        /// </summary>
        /// <param name="methodHandler">The delegate to be used when a method is called by the cloud service and there is no delegate registered for that method name.</param>
        /// <param name="userContext">Generic parameter to be interpreted by the client code.</param>
        public Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext) =>
            InternalClient.SetMethodDefaultHandlerAsync(methodHandler, userContext);

        /// <summary>
        /// Sets a new delegate that is called for a method that doesn't have a delegate registered for its name.
        /// If a default delegate is already registered it will replace with the new delegate.
        /// A method handler can be unset by passing a null MethodCallback.
        /// </summary>
        /// <param name="methodHandler">The delegate to be used when a method is called by the cloud service and there is no delegate registered for that method name.</param>
        /// <param name="userContext">Generic parameter to be interpreted by the client code.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        public Task SetMethodDefaultHandlerAsync(MethodCallback methodHandler, object userContext, CancellationToken cancellationToken) =>
            InternalClient.SetMethodDefaultHandlerAsync(methodHandler, userContext, cancellationToken);

        /// <summary>
        /// Sets a new delegate for the named method. If a delegate is already associated with
        /// the named method, it will be replaced with the new delegate.
        /// <param name="methodName">The name of the method to associate with the delegate.</param>
        /// <param name="methodHandler">The delegate to be used when a method with the given name is called by the cloud service.</param>
        /// <param name="userContext">generic parameter to be interpreted by the client code.</param>
        /// </summary>

        [Obsolete("Please use SetMethodHandlerAsync.")]
        public void SetMethodHandler(string methodName, MethodCallback methodHandler, object userContext) =>
            InternalClient.SetMethodHandler(methodName, methodHandler, userContext);

        /// <summary>
        /// Sets a new delegate for the connection status changed callback. If a delegate is already associated,
        /// it will be replaced with the new delegate. Note that this callback will never be called if the client is configured to use HTTP, as that protocol is stateless.
        /// <param name="statusChangesHandler">The name of the method to associate with the delegate.</param>
        /// </summary>
        public void SetConnectionStatusChangesHandler(ConnectionStatusChangesHandler statusChangesHandler) =>
            InternalClient.SetConnectionStatusChangesHandler(statusChangesHandler);

        /// <summary>
        /// Releases the unmanaged resources used by the DeviceClient and optionally disposes of the managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the DeviceClient and allows for any derived class to override and
        /// provide custom implementation.
        /// </summary>
        /// <param name="disposing">Setting to true will release both managed and unmanaged resources. Setting to
        /// false will only release the unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                InternalClient?.Dispose();
                InternalClient = null;
            }
        }

        /// <summary>
        /// Set a callback that will be called whenever the client receives a state update
        /// (desired or reported) from the service.  This has the side-effect of subscribing
        /// to the PATCH topic on the service.
        /// </summary>
        /// <param name="callback">Callback to call after the state update has been received and applied</param>
        /// <param name="userContext">Context object that will be passed into callback</param>
        [Obsolete("Please use SetDesiredPropertyUpdateCallbackAsync.")]
        public Task SetDesiredPropertyUpdateCallback(DesiredPropertyUpdateCallback callback, object userContext) =>
            InternalClient.SetDesiredPropertyUpdateCallback(callback, userContext);

        /// <summary>
        /// Set a callback that will be called whenever the client receives a state update
        /// (desired or reported) from the service.
        /// Set callback value to null to clear.
        /// </summary>
        /// <remarks>
        /// This has the side-effect of subscribing to the PATCH topic on the service.
        /// </remarks>
        /// <param name="callback">Callback to call after the state update has been received and applied</param>
        /// <param name="userContext">Context object that will be passed into callback</param>
        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback callback, object userContext) =>
            InternalClient.SetDesiredPropertyUpdateCallbackAsync(callback, userContext);

        /// <summary>
        /// Set a callback that will be called whenever the client receives a state update
        /// (desired or reported) from the service.
        /// Set callback value to null to clear.
        /// </summary>
        /// <remarks>
        /// This has the side-effect of subscribing to the PATCH topic on the service.
        /// </remarks>
        /// <param name="callback">Callback to call after the state update has been received and applied</param>
        /// <param name="userContext">Context object that will be passed into callback</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        public Task SetDesiredPropertyUpdateCallbackAsync(DesiredPropertyUpdateCallback callback, object userContext, CancellationToken cancellationToken) =>
            InternalClient.SetDesiredPropertyUpdateCallbackAsync(callback, userContext, cancellationToken);

        /// <summary>
        /// Retrieve the device twin properties for the current device.
        /// For the complete device twin object, use Microsoft.Azure.Devices.RegistryManager.GetTwinAsync(string deviceId).
        /// </summary>
        /// <returns>The device twin object for the current device</returns>
        public Task<Twin> GetTwinAsync() => InternalClient.GetTwinAsync();

        /// <summary>
        /// Retrieve the device twin properties for the current device.
        /// For the complete device twin object, use Microsoft.Azure.Devices.RegistryManager.GetTwinAsync(string deviceId).
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <exception cref="OperationCanceledException">Thrown when the operation has been canceled.</exception>
        /// <returns>The device twin object for the current device</returns>
        public Task<Twin> GetTwinAsync(CancellationToken cancellationToken) => InternalClient.GetTwinAsync(cancellationToken);

        /// <summary>
        /// Push reported property changes up to the service.
        /// </summary>
        /// <param name="reportedProperties">Reported properties to push</param>
        public Task UpdateReportedPropertiesAsync(TwinCollection reportedProperties) =>
            InternalClient.UpdateReportedPropertiesAsync(reportedProperties);

        #region Convention driven commands

        /// <summary>
        /// Update a single property.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="componentName"></param>
        /// <param name="cancellationToken"></param>
        public Task UpdatePropertyAsync(string propertyName, dynamic propertyValue, string componentName = default, CancellationToken cancellationToken = default)
            => UpdatePropertiesAsync(new Dictionary<string, dynamic> { { propertyName, propertyValue } }, componentName, cancellationToken);


        /// <summary>
        /// Update a collection of properties.
        /// </summary>
        /// <param name="properties">Reported properties to push</param>
        /// <param name="componentName"></param>
        /// <param name="cancellationToken"></param>
        public Task UpdatePropertiesAsync(IDictionary<string, dynamic> properties, string componentName = default, CancellationToken cancellationToken = default)
            => InternalClient.UpdatePropertiesAsync(properties, componentName, cancellationToken);

        /// <summary>
        /// Respond to a writable property request.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="componentName"></param>
        /// <param name="cancellationToken"></param>
        public Task RespondToWritablePropertyEventAsync(string propertyName, WritableProperty propertyValue, string componentName = default, CancellationToken cancellationToken = default)
            => InternalClient.UpdatePropertyAsync(propertyName, propertyValue, componentName, cancellationToken);

        /// <summary>
        /// Respond to a writable property request.
        /// </summary>
        /// <param name="propertyCollection"></param>
        /// <param name="componentName"></param>
        /// <param name="cancellationToken"></param>
        public Task RespondToWritablePropertyEventAsync(IDictionary<string, WritableProperty> propertyCollection, string componentName = default, CancellationToken cancellationToken = default)
            => InternalClient.UpdatePropertiesAsync((IDictionary<string, dynamic>)propertyCollection, componentName, cancellationToken);

        /// <summary>
        /// Respond to a writable property request.
        /// </summary>
        /// <param name="propertyCollection"></param>
        /// <param name="componentName"></param>
        /// <param name="cancellationToken"></param>
        public Task RespondToWritablePropertyEventAsync(TwinCollection propertyCollection, string componentName = default, CancellationToken cancellationToken = default)
            => InternalClient.UpdatePropertiesAsync(propertyCollection, componentName, cancellationToken);

        /// <summary>
        /// Sets the global listener for Writable properties
        /// </summary>
        /// <param name="callback"></param>
        /// <param name="userContext"></param>
        /// <param name="cancellationToken"></param>
        public Task ListenToWritablePropertyEvent(Action<TwinCollection, object> callback, object userContext, CancellationToken cancellationToken = default)
            => InternalClient.ListenToWritablePropertyEvent(callback, userContext, cancellationToken);

        /// <summary>
        /// Sends a single instance of telemetry.
        /// </summary>
        /// <param name="telemetryName">The name of the telemetry to send.</param>
        /// <param name="telemetryValue">The value of the telemetry to send.</param>
        /// <param name="conventionHandler">A convention handler that defines the content encoding and serializer to use for the telemetry message.</param>
        /// <param name="componentName">The component name this telemetry belongs to.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// <remarks>
        /// This will create a single telemetry message and will not combine multiple calls into one message. Use <seealso cref="SendTelemetryAsync(IDictionary{string, dynamic}, string, IConventionHandler, CancellationToken)"/>. Refer to the documentation for <see cref="IConventionHandler"/> if you want to use a custom serializer.
        /// </remarks>
        public Task SendTelemetryAsync(string telemetryName, dynamic telemetryValue, string componentName = default, IConventionHandler conventionHandler = default, CancellationToken cancellationToken = default)
            => SendTelemetryAsync(new Dictionary<string, dynamic> { { telemetryName, telemetryValue } }, componentName, conventionHandler, cancellationToken);

        /// <summary>
        /// Sends a collection of telemetry.
        /// </summary>
        /// <param name="telemetryDictionary">A dictionary of dynamic objects </param>
        /// <param name="componentName">The component name this telemetry belongs to.</param>
        /// <param name="conventionHandler">A convention handler that defines the content encoding and serializer to use for the telemetry message.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
        /// /// <remarks>
        /// This will either use the <see cref="DefaultTelemetryConventionHandler"/> to define the encoding and use the default Json serailzier. Refer to the documentation for <see cref="IConventionHandler"/> if you want to use a custom serializer.
        /// </remarks>
        public Task SendTelemetryAsync(IDictionary<string, dynamic> telemetryDictionary, string componentName = default, IConventionHandler conventionHandler = default, CancellationToken cancellationToken = default)
            => InternalClient.SendTelemetryAsync(telemetryDictionary, componentName, conventionHandler, cancellationToken);

        /// <summary>
        /// Send telemetry using the specified message.
        /// </summary>
        /// <param name="telemetryMessage">The </param>
        /// <param name="componentName">The component name this telemetry belongs to.</param>
        /// <param name="conventionHandler">A convention handler that defines the content encoding and serializer to use for the telemetry message.</param>
        /// <param name="cancellationToken">A cancellation token.</param>
#pragma warning disable CA1062 // Validate arguments of public methods
        public Task SendTelemetryAsync(Message telemetryMessage, string componentName = default, IConventionHandler conventionHandler = default, CancellationToken cancellationToken = default)
            => InternalClient.SendTelemetryAsync(telemetryMessage, componentName, conventionHandler, cancellationToken);
#pragma warning restore CA1062 // Validate arguments of public methods

        /// <summary>
        /// Set command callback handler.
        /// </summary>
        /// <param name="commandName"></param>
        /// <param name="commandCallback"></param>
        /// <param name="componentName"></param>
        /// <param name="userContext"></param>
        /// <param name="cancellationToken"></param>
        public Task SetCommandCallbackHandler(string commandName, Func<CommandRequest, object, Task<CommandResponse>> commandCallback, string componentName = default, object userContext = default, CancellationToken cancellationToken = default)
            => InternalClient.SetCommandCallbackHandler(commandName, commandCallback, componentName, userContext, cancellationToken);

        /// <summary>
        /// Set the global command callback handler. This handler will be called when no named handler was found for the command.
        /// </summary>
        /// <param name="commandCallback">A method implementation that will handle the incoming command.</param>
        /// <param name="userContext">Generic parameter to be interpreted by the client code.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        public Task SetCommandCallbackHandler(Func<CommandRequest, object, Task<CommandResponse>> commandCallback, object userContext = default, CancellationToken cancellationToken = default)
            => InternalClient.SetCommandCallbackHandler(commandCallback, userContext, cancellationToken);
        #endregion
    }
}
