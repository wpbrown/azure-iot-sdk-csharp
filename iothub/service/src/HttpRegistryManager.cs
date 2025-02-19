﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Devices.Common;
using Microsoft.Azure.Devices.Common.Exceptions;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;

namespace Microsoft.Azure.Devices
{
    internal class HttpRegistryManager : RegistryManager
    {
        private const string AdminUriFormat = "/$admin/{0}?{1}";
        private const string RequestUriFormat = "/devices/{0}?{1}";
        private const string JobsUriFormat = "/jobs{0}?{1}";
        private const string StatisticsUriFormat = "/statistics/devices?" + ClientApiVersionHelper.ApiVersionQueryString;
        private const string DevicesRequestUriFormat = "/devices/?top={0}&{1}";
        private const string DevicesQueryUriFormat = "/devices/query?" + ClientApiVersionHelper.ApiVersionQueryString;
        private const string WildcardEtag = "*";

        private const string ContinuationTokenHeader = "x-ms-continuation";
        private const string PageSizeHeader = "x-ms-max-item-count";

        private const string TwinUriFormat = "/twins/{0}?{1}";

        private const string ModulesRequestUriFormat = "/devices/{0}/modules/{1}?{2}";
        private const string ModulesOnDeviceRequestUriFormat = "/devices/{0}/modules?{1}";
        private const string ModuleTwinUriFormat = "/twins/{0}/modules/{1}?{2}";

        private const string ConfigurationRequestUriFormat = "/configurations/{0}?{1}";
        private const string ConfigurationsRequestUriFormat = "/configurations/?top={0}&{1}";

        private const string ApplyConfigurationOnDeviceUriFormat = "/devices/{0}/applyConfigurationContent?" + ClientApiVersionHelper.ApiVersionQueryString;

        private static readonly TimeSpan s_regexTimeoutMilliseconds = TimeSpan.FromMilliseconds(500);

        private static readonly Regex s_deviceIdRegex = new Regex(
            @"^[A-Za-z0-9\-:.+%_#*?!(),=@;$']{1,128}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase,
            s_regexTimeoutMilliseconds);

        private static readonly TimeSpan s_defaultOperationTimeout = TimeSpan.FromSeconds(100);
        private static readonly TimeSpan s_defaultGetDevicesOperationTimeout = TimeSpan.FromSeconds(120);

        private IHttpClientHelper _httpClientHelper;
        private readonly string _iotHubName;

        internal HttpRegistryManager(IotHubConnectionString connectionString, HttpTransportSettings transportSettings)
        {
            _iotHubName = connectionString.IotHubName;
            _httpClientHelper = new HttpClientHelper(
                connectionString.HttpsEndpoint,
                connectionString,
                ExceptionHandlingHelper.GetDefaultErrorMapping(),
                s_defaultOperationTimeout,
                transportSettings.Proxy,
                transportSettings.ConnectionLeaseTimeoutMilliseconds);
        }

        // internal test helper
        internal HttpRegistryManager(IHttpClientHelper httpClientHelper, string iotHubName)
        {
            _httpClientHelper = httpClientHelper ?? throw new ArgumentNullException(nameof(httpClientHelper));
            _iotHubName = iotHubName;
        }

        public override Task OpenAsync()
        {
            return TaskHelpers.CompletedTask;
        }

        public override Task CloseAsync()
        {
            return TaskHelpers.CompletedTask;
        }

        public override Task<Device> AddDeviceAsync(Device device)
        {
            return AddDeviceAsync(device, CancellationToken.None);
        }

        public override Task<Device> AddDeviceAsync(Device device, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Adding device: {device?.Id}", nameof(AddDeviceAsync));

            try
            {
                EnsureInstanceNotClosed();

                ValidateDeviceId(device);

                if (!string.IsNullOrEmpty(device.ETag))
                {
                    throw new ArgumentException(ApiResources.ETagSetWhileRegisteringDevice);
                }

                ValidateDeviceAuthentication(device.Authentication, device.Id);

                NormalizeDevice(device);

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
            {
                {
                    HttpStatusCode.PreconditionFailed,
                    async responseMessage => new PreconditionFailedException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                }
            };

                return _httpClientHelper.PutAsync(GetRequestUri(device.Id), device, PutOperationType.CreateEntity, errorMappingOverrides, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(AddDeviceAsync)} threw an exception: {ex}", nameof(AddDeviceAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Adding device: {device?.Id}", nameof(AddDeviceAsync));
            }
        }

        public override Task<Module> AddModuleAsync(Module module)
        {
            return AddModuleAsync(module, CancellationToken.None);
        }

        public override Task<Module> AddModuleAsync(Module module, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Adding module: {module?.Id}", nameof(AddModuleAsync));

            try
            {
                EnsureInstanceNotClosed();

                ValidateModuleId(module);

                if (!string.IsNullOrEmpty(module.ETag))
                {
                    throw new ArgumentException(ApiResources.ETagSetWhileRegisteringDevice);
                }

                ValidateDeviceAuthentication(module.Authentication, module.DeviceId);

                // auto generate keys if not specified
                if (module.Authentication == null)
                {
                    module.Authentication = new AuthenticationMechanism();
                }

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    {
                        HttpStatusCode.PreconditionFailed,
                        async responseMessage => new PreconditionFailedException(
                            await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                    },
                    {
                        HttpStatusCode.Conflict,
                        async responseMessage => new ModuleAlreadyExistsException(
                            await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                    },
                    {
                        HttpStatusCode.RequestEntityTooLarge,
                        async responseMessage => new TooManyModulesOnDeviceException(
                            await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                    }
                };

                return _httpClientHelper.PutAsync(GetModulesRequestUri(module.DeviceId, module.Id), module, PutOperationType.CreateEntity, errorMappingOverrides, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(AddModuleAsync)} threw an exception: {ex}", nameof(AddModuleAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Adding module: {module?.Id}", nameof(AddModuleAsync));
            }
        }

        public override Task<BulkRegistryOperationResult> AddDeviceWithTwinAsync(Device device, Twin twin)
        {
            return AddDeviceWithTwinAsync(device, twin, CancellationToken.None);
        }

        public override Task<BulkRegistryOperationResult> AddDeviceWithTwinAsync(Device device, Twin twin, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Adding device with twin: {device?.Id}", nameof(AddDeviceWithTwinAsync));

            try
            {
                ValidateDeviceId(device);
                if (!string.IsNullOrWhiteSpace(device.ETag))
                {
                    throw new ArgumentException(ApiResources.ETagSetWhileRegisteringDevice);
                }
                var exportImportDeviceList = new List<ExportImportDevice>(1);

                var exportImportDevice = new ExportImportDevice(device, ImportMode.Create)
                {
                    Tags = twin?.Tags,
                    Properties = new ExportImportDevice.PropertyContainer
                    {
                        DesiredProperties = twin?.Properties.Desired,
                        ReportedProperties = twin?.Properties.Reported,
                    }
                };

                exportImportDeviceList.Add(exportImportDevice);

                return BulkDeviceOperationsAsync<BulkRegistryOperationResult>(
                   exportImportDeviceList,
                   ClientApiVersionHelper.ApiVersionQueryString,
                   cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(AddDeviceWithTwinAsync)} threw an exception: {ex}", nameof(AddDeviceWithTwinAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Adding device with twin: {device?.Id}", nameof(AddDeviceWithTwinAsync));
            }
        }

        [Obsolete("Use AddDevices2Async")]
        public override Task<string[]> AddDevicesAsync(IEnumerable<Device> devices)
        {
            return AddDevicesAsync(devices, CancellationToken.None);
        }

        [Obsolete("Use AddDevices2Async")]
        public override Task<string[]> AddDevicesAsync(IEnumerable<Device> devices, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Adding {devices?.Count()} devices", nameof(AddDevicesAsync));

            try
            {
                return BulkDeviceOperationsAsync<string[]>(
                    GenerateExportImportDeviceListForBulkOperations(devices, ImportMode.Create),
                    ClientApiVersionHelper.ApiVersionQueryString,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(AddDevicesAsync)} threw an exception: {ex}", nameof(AddDevicesAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Adding {devices?.Count()} devices", nameof(AddDevicesAsync));
            }
        }

        public override Task<BulkRegistryOperationResult> AddDevices2Async(IEnumerable<Device> devices)
        {
            return AddDevices2Async(devices, CancellationToken.None);
        }

        public override Task<BulkRegistryOperationResult> AddDevices2Async(IEnumerable<Device> devices, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Adding {devices?.Count()} devices", nameof(AddDevices2Async));

            try
            {
                return BulkDeviceOperationsAsync<BulkRegistryOperationResult>(
                    GenerateExportImportDeviceListForBulkOperations(devices, ImportMode.Create),
                    ClientApiVersionHelper.ApiVersionQueryString,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(AddDevices2Async)} threw an exception: {ex}", nameof(AddDevices2Async));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Adding {devices?.Count()} devices", nameof(AddDevices2Async));
            }
        }

        public override Task<Device> UpdateDeviceAsync(Device device)
        {
            return UpdateDeviceAsync(device, CancellationToken.None);
        }

        public override Task<Device> UpdateDeviceAsync(Device device, bool forceUpdate)
        {
            return UpdateDeviceAsync(device, forceUpdate, CancellationToken.None);
        }

        public override Task<Device> UpdateDeviceAsync(Device device, CancellationToken cancellationToken)
        {
            return UpdateDeviceAsync(device, false, cancellationToken);
        }

        public override Task<Device> UpdateDeviceAsync(Device device, bool forceUpdate, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Updating device: {device?.Id}", nameof(UpdateDeviceAsync));
            try
            {
                EnsureInstanceNotClosed();

                ValidateDeviceId(device);

                if (string.IsNullOrWhiteSpace(device.ETag) && !forceUpdate)
                {
                    throw new ArgumentException(ApiResources.ETagNotSetWhileUpdatingDevice);
                }

                ValidateDeviceAuthentication(device.Authentication, device.Id);

                NormalizeDevice(device);

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>()
                {
                    { HttpStatusCode.PreconditionFailed, async (responseMessage) => new PreconditionFailedException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false)) },
                    {
                        HttpStatusCode.NotFound, async responseMessage =>
                        {
                            string responseContent = await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false);
                            return (Exception)new DeviceNotFoundException(responseContent, (Exception)null);
                        }
                    }
                };

                PutOperationType operationType = forceUpdate ? PutOperationType.ForceUpdateEntity : PutOperationType.UpdateEntity;

                return _httpClientHelper.PutAsync(GetRequestUri(device.Id), device, operationType, errorMappingOverrides, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(UpdateDeviceAsync)} threw an exception: {ex}", nameof(UpdateDeviceAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Updating device: {device?.Id}", nameof(UpdateDeviceAsync));
            }
        }

        public override Task<Module> UpdateModuleAsync(Module module)
        {
            return UpdateModuleAsync(module, CancellationToken.None);
        }

        public override Task<Module> UpdateModuleAsync(Module module, bool forceUpdate)
        {
            return UpdateModuleAsync(module, forceUpdate, CancellationToken.None);
        }

        public override Task<Module> UpdateModuleAsync(Module module, CancellationToken cancellationToken)
        {
            return UpdateModuleAsync(module, false, CancellationToken.None);
        }

        public override Task<Module> UpdateModuleAsync(Module module, bool forceUpdate, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Updating module: {module?.Id}", nameof(UpdateModuleAsync));

            try
            {
                EnsureInstanceNotClosed();

                ValidateModuleId(module);

                if (string.IsNullOrWhiteSpace(module.ETag) && !forceUpdate)
                {
                    throw new ArgumentException(ApiResources.ETagNotSetWhileUpdatingDevice);
                }

                ValidateDeviceAuthentication(module.Authentication, module.DeviceId);

                // auto generate keys if not specified
                if (module.Authentication == null)
                {
                    module.Authentication = new AuthenticationMechanism();
                }

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>()
                {
                    { HttpStatusCode.PreconditionFailed, async (responseMessage) => new PreconditionFailedException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false)) },
                    {
                        HttpStatusCode.NotFound, async responseMessage =>
                        {
                            string responseContent = await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false);
                            return new ModuleNotFoundException(responseContent, (Exception)null);
                        }
                    }
                };

                PutOperationType operationType = forceUpdate ? PutOperationType.ForceUpdateEntity : PutOperationType.UpdateEntity;

                return _httpClientHelper.PutAsync(GetModulesRequestUri(module.DeviceId, module.Id), module, operationType, errorMappingOverrides, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(UpdateModuleAsync)} threw an exception: {ex}", nameof(UpdateModuleAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Updating module: {module?.Id}", nameof(UpdateModuleAsync));
            }
        }

        public override Task<Configuration> AddConfigurationAsync(Configuration configuration)
        {
            return AddConfigurationAsync(configuration, CancellationToken.None);
        }

        public override Task<Configuration> AddConfigurationAsync(Configuration configuration, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Adding configuration: {configuration?.Id}", nameof(AddConfigurationAsync));

            try
            {
                EnsureInstanceNotClosed();

                if (!string.IsNullOrEmpty(configuration.ETag))
                {
                    throw new ArgumentException(ApiResources.ETagSetWhileCreatingConfiguration);
                }

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    {
                        HttpStatusCode.PreconditionFailed,
                        async responseMessage => new PreconditionFailedException(
                            await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                    }
                };

                return _httpClientHelper.PutAsync(GetConfigurationRequestUri(configuration.Id), configuration, PutOperationType.CreateEntity, errorMappingOverrides, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(AddConfigurationAsync)} threw an exception: {ex}", nameof(AddConfigurationAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Adding configuration: {configuration?.Id}", nameof(AddConfigurationAsync));
            }
        }

        public override Task<Configuration> GetConfigurationAsync(string configurationId)
        {
            return GetConfigurationAsync(configurationId, CancellationToken.None);
        }

        public override Task<Configuration> GetConfigurationAsync(string configurationId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting configuration: {configurationId}", nameof(GetConfigurationAsync));
            try
            {
                if (string.IsNullOrWhiteSpace(configurationId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "configurationId"));
                }

                EnsureInstanceNotClosed();
                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>()
                {
                    { HttpStatusCode.NotFound, async responseMessage => new ConfigurationNotFoundException(
                        await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false)) }
                };

                return _httpClientHelper.GetAsync<Configuration>(GetConfigurationRequestUri(configurationId), errorMappingOverrides, null, false, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetConfigurationAsync)} threw an exception: {ex}", nameof(GetConfigurationAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Get configuration: {configurationId}", nameof(GetConfigurationAsync));
            }
        }

        public override Task<IEnumerable<Configuration>> GetConfigurationsAsync(int maxCount)
        {
            return GetConfigurationsAsync(maxCount, CancellationToken.None);
        }

        public override Task<IEnumerable<Configuration>> GetConfigurationsAsync(int maxCount, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting configuration: max count: {maxCount}", nameof(GetConfigurationsAsync));
            try
            {
                EnsureInstanceNotClosed();

                return _httpClientHelper.GetAsync<IEnumerable<Configuration>>(
                    GetConfigurationsRequestUri(maxCount),
                    null,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetConfigurationsAsync)} threw an exception: {ex}", nameof(GetConfigurationsAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting configuration: max count: {maxCount}", nameof(GetConfigurationsAsync));
            }
        }

        public override Task<Configuration> UpdateConfigurationAsync(Configuration configuration)
        {
            return UpdateConfigurationAsync(configuration, CancellationToken.None);
        }

        public override Task<Configuration> UpdateConfigurationAsync(Configuration configuration, bool forceUpdate)
        {
            return UpdateConfigurationAsync(configuration, forceUpdate, CancellationToken.None);
        }

        public override Task<Configuration> UpdateConfigurationAsync(Configuration configuration, CancellationToken cancellationToken)
        {
            return UpdateConfigurationAsync(configuration, false, cancellationToken);
        }

        public override Task<Configuration> UpdateConfigurationAsync(Configuration configuration, bool forceUpdate, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Updating configuration: {configuration?.Id} - Force update: {forceUpdate}", nameof(UpdateConfigurationAsync));

            try
            {
                EnsureInstanceNotClosed();

                if (string.IsNullOrWhiteSpace(configuration.ETag) && !forceUpdate)
                {
                    throw new ArgumentException(ApiResources.ETagNotSetWhileUpdatingConfiguration);
                }

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>()
                {
                    { HttpStatusCode.PreconditionFailed, async (responseMessage) => new PreconditionFailedException(
                        await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false)) },
                    {
                        HttpStatusCode.NotFound, async responseMessage =>
                        {
                            string responseContent = await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false);
                            return new ConfigurationNotFoundException(responseContent, (Exception)null);
                        }
                    }
                };

                PutOperationType operationType = forceUpdate
                    ? PutOperationType.ForceUpdateEntity
                    : PutOperationType.UpdateEntity;

                return _httpClientHelper.PutAsync(GetConfigurationRequestUri(configuration.Id), configuration, operationType, errorMappingOverrides, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(UpdateConfigurationAsync)} threw an exception: {ex}", nameof(UpdateConfigurationAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Updating configuration: {configuration?.Id} - Force update: {forceUpdate}", nameof(UpdateConfigurationAsync));
            }
        }

        public override Task RemoveConfigurationAsync(string configurationId)
        {
            return RemoveConfigurationAsync(configurationId, CancellationToken.None);
        }

        public override Task RemoveConfigurationAsync(string configurationId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Removing configuration: {configurationId}", nameof(RemoveConfigurationAsync));

            try
            {
                EnsureInstanceNotClosed();

                if (string.IsNullOrWhiteSpace(configurationId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "configurationId"));
                }

                // use wild-card ETag
                var eTag = new ETagHolder { ETag = "*" };
                return RemoveConfigurationAsync(configurationId, eTag, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(RemoveConfigurationAsync)} threw an exception: {ex}", nameof(RemoveConfigurationAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Removing configuration: {configurationId}", nameof(RemoveConfigurationAsync));
            }
        }

        public override Task RemoveConfigurationAsync(Configuration configuration)
        {
            return RemoveConfigurationAsync(configuration, CancellationToken.None);
        }

        public override Task RemoveConfigurationAsync(Configuration configuration, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Removing configuration: {configuration?.Id}", nameof(RemoveConfigurationAsync));
            try
            {
                EnsureInstanceNotClosed();

                return string.IsNullOrWhiteSpace(configuration.ETag)
                    ? throw new ArgumentException(ApiResources.ETagNotSetWhileDeletingConfiguration)
                    : RemoveConfigurationAsync(configuration.Id, configuration, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(RemoveConfigurationAsync)} threw an exception: {ex}", nameof(RemoveConfigurationAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Removing configuration: {configuration?.Id}", nameof(RemoveConfigurationAsync));
            }
        }

        public override Task ApplyConfigurationContentOnDeviceAsync(string deviceId, ConfigurationContent content)
        {
            return ApplyConfigurationContentOnDeviceAsync(deviceId, content, CancellationToken.None);
        }

        public override Task ApplyConfigurationContentOnDeviceAsync(string deviceId, ConfigurationContent content, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Applying configuration content on device: {deviceId}", nameof(ApplyConfigurationContentOnDeviceAsync));

            try
            {
                return _httpClientHelper.PostAsync(GetApplyConfigurationOnDeviceRequestUri(deviceId), content, null, null, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(ApplyConfigurationContentOnDeviceAsync)} threw an exception: {ex}", nameof(ApplyConfigurationContentOnDeviceAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Applying configuration content on device: {deviceId}", nameof(ApplyConfigurationContentOnDeviceAsync));
            }
        }

        private Task RemoveConfigurationAsync(string configurationId, IETagHolder eTagHolder, CancellationToken cancellationToken)
        {
            var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
            {
                {
                    HttpStatusCode.NotFound,
                    async responseMessage =>
                        {
                            string responseContent = await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false);
                            return new ConfigurationNotFoundException(responseContent, (Exception) null);
                        }
                },
                {
                    HttpStatusCode.PreconditionFailed,
                    async responseMessage => new PreconditionFailedException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                }
            };

            return _httpClientHelper.DeleteAsync(GetConfigurationRequestUri(configurationId), eTagHolder, errorMappingOverrides, null, cancellationToken);
        }

        [Obsolete("Use UpdateDevices2Async")]
        public override Task<string[]> UpdateDevicesAsync(IEnumerable<Device> devices)
        {
            return UpdateDevicesAsync(devices, false, CancellationToken.None);
        }

        [Obsolete("Use UpdateDevices2Async")]
        public override Task<string[]> UpdateDevicesAsync(IEnumerable<Device> devices, bool forceUpdate, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Updating multiple devices: count: {devices?.Count()}", nameof(UpdateDevicesAsync));

            try
            {
                return BulkDeviceOperationsAsync<string[]>(
                GenerateExportImportDeviceListForBulkOperations(devices, forceUpdate ? ImportMode.Update : ImportMode.UpdateIfMatchETag),
                ClientApiVersionHelper.ApiVersionQueryString,
                cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(UpdateDevicesAsync)} threw an exception: {ex}", nameof(UpdateDevicesAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Updating multiple devices: count: {devices?.Count()}", nameof(UpdateDevicesAsync));
            }
        }

        public override Task<BulkRegistryOperationResult> UpdateDevices2Async(IEnumerable<Device> devices)
        {
            return UpdateDevices2Async(devices, false, CancellationToken.None);
        }

        public override Task<BulkRegistryOperationResult> UpdateDevices2Async(IEnumerable<Device> devices, bool forceUpdate, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Updating multiple devices: count: {devices?.Count()} - Force update: {forceUpdate}", nameof(UpdateDevices2Async));

            try
            {
                return BulkDeviceOperationsAsync<BulkRegistryOperationResult>(
                    GenerateExportImportDeviceListForBulkOperations(devices, forceUpdate ? ImportMode.Update : ImportMode.UpdateIfMatchETag),
                    ClientApiVersionHelper.ApiVersionQueryString,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(UpdateDevices2Async)} threw an exception: {ex}", nameof(UpdateDevices2Async));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Updating multiple devices: count: {devices?.Count()} - Force update: {forceUpdate}", nameof(UpdateDevices2Async));
            }

        }

        public override Task RemoveDeviceAsync(string deviceId)
        {
            return RemoveDeviceAsync(deviceId, CancellationToken.None);
        }

        public override Task RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Removing device: {deviceId}", nameof(RemoveDeviceAsync));
            try
            {
                EnsureInstanceNotClosed();

                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "deviceId"));
                }

                // use wild-card ETag
                var eTag = new ETagHolder { ETag = "*" };
                return RemoveDeviceAsync(deviceId, eTag, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(RemoveDeviceAsync)} threw an exception: {ex}", nameof(RemoveDeviceAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Removing device: {deviceId}", nameof(RemoveDeviceAsync));
            }
        }

        public override Task RemoveDeviceAsync(Device device)
        {
            return RemoveDeviceAsync(device, CancellationToken.None);
        }

        public override Task RemoveDeviceAsync(Device device, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Removing device: {device?.Id}", nameof(RemoveDeviceAsync));

            try
            {
                EnsureInstanceNotClosed();

                ValidateDeviceId(device);

                return string.IsNullOrWhiteSpace(device.ETag)
                    ? throw new ArgumentException(ApiResources.ETagNotSetWhileDeletingDevice)
                    : RemoveDeviceAsync(device.Id, device, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(RemoveDeviceAsync)} threw an exception: {ex}", nameof(RemoveDeviceAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Removing device: {device?.Id}", nameof(RemoveDeviceAsync));
            }
        }

        public override Task RemoveModuleAsync(string deviceId, string moduleId)
        {
            return RemoveModuleAsync(deviceId, moduleId, CancellationToken.None);
        }

        public override Task RemoveModuleAsync(string deviceId, string moduleId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Removing module: device Id:{deviceId} moduleId: {moduleId}", nameof(RemoveDeviceAsync));

            try
            {
                EnsureInstanceNotClosed();

                if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrEmpty(moduleId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "deviceId"));
                }

                // use wild-card ETag
                var eTag = new ETagHolder { ETag = "*" };
                return RemoveDeviceModuleAsync(deviceId, moduleId, eTag, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(RemoveModuleAsync)} threw an exception: {ex}", nameof(RemoveModuleAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Removing module: device Id:{deviceId} moduleId: {moduleId}", nameof(RemoveModuleAsync));
            }
        }

        public override Task RemoveModuleAsync(Module module)
        {
            return RemoveModuleAsync(module, CancellationToken.None);
        }

        public override Task RemoveModuleAsync(Module module, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Removing module: device Id:{module?.DeviceId} moduleId: {module?.Id}", nameof(RemoveModuleAsync));

            try
            {
                EnsureInstanceNotClosed();

                ValidateModuleId(module);

                return string.IsNullOrWhiteSpace(module.ETag)
                    ? throw new ArgumentException(ApiResources.ETagNotSetWhileDeletingDevice)
                    : RemoveDeviceModuleAsync(module.DeviceId, module.Id, module, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(RemoveModuleAsync)} threw an exception: {ex}", nameof(RemoveModuleAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Removing module: device Id:{module?.DeviceId} moduleId: {module?.Id}", nameof(RemoveModuleAsync));
            }
        }

        [Obsolete("Use RemoveDevices2Async")]
        public override Task<string[]> RemoveDevicesAsync(IEnumerable<Device> devices)
        {
            return RemoveDevicesAsync(devices, false, CancellationToken.None);
        }

        [Obsolete("Use RemoveDevices2Async")]
        public override Task<string[]> RemoveDevicesAsync(IEnumerable<Device> devices, bool forceRemove, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Removing devices : count: {devices?.Count()} - Force remove: {forceRemove}", nameof(RemoveDevicesAsync));
            try
            {
                return BulkDeviceOperationsAsync<string[]>(
                   GenerateExportImportDeviceListForBulkOperations(devices, forceRemove ? ImportMode.Delete : ImportMode.DeleteIfMatchETag),
                   ClientApiVersionHelper.ApiVersionQueryString,
                   cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(RemoveDevicesAsync)} threw an exception: {ex}", nameof(RemoveDevicesAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Removing devices : count: {devices?.Count()} - Force remove: {forceRemove}", nameof(RemoveDevicesAsync));
            }
        }

        public override Task<BulkRegistryOperationResult> RemoveDevices2Async(IEnumerable<Device> devices)
        {
            return RemoveDevices2Async(devices, false, CancellationToken.None);
        }

        public override Task<BulkRegistryOperationResult> RemoveDevices2Async(IEnumerable<Device> devices, bool forceRemove, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Removing devices : count: {devices?.Count()} - Force remove: {forceRemove}", nameof(RemoveDevices2Async));

            try
            {
                return BulkDeviceOperationsAsync<BulkRegistryOperationResult>(
                    GenerateExportImportDeviceListForBulkOperations(devices, forceRemove ? ImportMode.Delete : ImportMode.DeleteIfMatchETag),
                    ClientApiVersionHelper.ApiVersionQueryString,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(RemoveDevicesAsync)} threw an exception: {ex}", nameof(RemoveDevicesAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Removing devices : count: {devices?.Count()} - Force remove: {forceRemove}", nameof(RemoveDevicesAsync));
            }
        }

        public override Task<RegistryStatistics> GetRegistryStatisticsAsync()
        {
            return GetRegistryStatisticsAsync(CancellationToken.None);
        }

        public override Task<RegistryStatistics> GetRegistryStatisticsAsync(CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting registry statistics", nameof(GetRegistryStatisticsAsync));

            try
            {
                EnsureInstanceNotClosed();
                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    { HttpStatusCode.NotFound, responseMessage => Task.FromResult((Exception)new IotHubNotFoundException(_iotHubName)) }
                };

                return _httpClientHelper.GetAsync<RegistryStatistics>(GetStatisticsUri(), errorMappingOverrides, null, cancellationToken);
            }

            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetRegistryStatisticsAsync)} threw an exception: {ex}", nameof(GetRegistryStatisticsAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting registry statistics", nameof(GetRegistryStatisticsAsync));
            }
        }

        public override Task<Device> GetDeviceAsync(string deviceId)
        {
            return GetDeviceAsync(deviceId, CancellationToken.None);
        }

        public override Task<Device> GetDeviceAsync(string deviceId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting device: {deviceId}", nameof(GetDeviceAsync));
            try
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "deviceId"));
                }

                EnsureInstanceNotClosed();
                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>()
                {
                    { HttpStatusCode.NotFound, async responseMessage => new DeviceNotFoundException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false)) }
                };

                return _httpClientHelper.GetAsync<Device>(GetRequestUri(deviceId), errorMappingOverrides, null, false, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetDeviceAsync)} threw an exception: {ex}", nameof(GetDeviceAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting device: {deviceId}", nameof(GetDeviceAsync));
            }
        }

        public override Task<Module> GetModuleAsync(string deviceId, string moduleId)
        {
            return GetModuleAsync(deviceId, moduleId, CancellationToken.None);
        }

        public override Task<Module> GetModuleAsync(string deviceId, string moduleId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting module: device Id: {deviceId} - module Id: {moduleId}", nameof(GetModuleAsync));

            try
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "deviceId"));
                }

                if (string.IsNullOrWhiteSpace(moduleId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "moduleId"));
                }

                EnsureInstanceNotClosed();
                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    {
                        HttpStatusCode.NotFound,
                        responseMessage => Task.FromResult<Exception>(new ModuleNotFoundException(deviceId, moduleId))
                    },
                };

                return _httpClientHelper.GetAsync<Module>(GetModulesRequestUri(deviceId, moduleId), errorMappingOverrides, null, false, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetModuleAsync)} threw an exception: {ex}", nameof(GetModuleAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting module: device Id: {deviceId} - module Id: {moduleId}", nameof(GetModuleAsync));
            }
        }

        public override Task<IEnumerable<Module>> GetModulesOnDeviceAsync(string deviceId)
        {
            return GetModulesOnDeviceAsync(deviceId, CancellationToken.None);
        }

        public override Task<IEnumerable<Module>> GetModulesOnDeviceAsync(string deviceId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting module on device: {deviceId}", nameof(GetModulesOnDeviceAsync));

            try
            {

                EnsureInstanceNotClosed();

                return _httpClientHelper.GetAsync<IEnumerable<Module>>(
                    GetModulesOnDeviceRequestUri(deviceId),
                    null,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetModulesOnDeviceAsync)} threw an exception: {ex}", nameof(GetModulesOnDeviceAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting module on device: {deviceId}", nameof(GetModulesOnDeviceAsync));
            }
        }

        [Obsolete("Use CreateQuery(\"select * from devices\", pageSize);")]
        public override Task<IEnumerable<Device>> GetDevicesAsync(int maxCount)
        {
            return GetDevicesAsync(maxCount, CancellationToken.None);
        }

        [Obsolete("Use CreateQuery(\"select * from devices\", pageSize);")]
        public override Task<IEnumerable<Device>> GetDevicesAsync(int maxCount, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting devices - max count: {maxCount}", nameof(GetDevicesAsync));

            try
            {
                EnsureInstanceNotClosed();

                return _httpClientHelper.GetAsync<IEnumerable<Device>>(
                    GetDevicesRequestUri(maxCount),
                    s_defaultGetDevicesOperationTimeout,
                    null,
                    null,
                    true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetDevicesAsync)} threw an exception: {ex}", nameof(GetDevicesAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting devices - max count: {maxCount}", nameof(GetDevicesAsync));
            }
        }

        public override IQuery CreateQuery(string sqlQueryString)
        {
            return CreateQuery(sqlQueryString, null);
        }

        public override IQuery CreateQuery(string sqlQueryString, int? pageSize)
        {
            Logging.Enter(this, $"Creating query", nameof(CreateQuery));
            try
            {
                return new Query((token) => ExecuteQueryAsync(
                    sqlQueryString,
                    pageSize,
                    token,
                    CancellationToken.None));
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(CreateQuery)} threw an exception: {ex}", nameof(CreateQuery));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Creating query", nameof(CreateQuery));
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && _httpClientHelper != null)
            {
                _httpClientHelper.Dispose();
                _httpClientHelper = null;
            }
        }

        private static IEnumerable<ExportImportDevice> GenerateExportImportDeviceListForBulkOperations(IEnumerable<Device> devices, ImportMode importMode)
        {
            if (devices == null)
            {
                throw new ArgumentNullException(nameof(devices));
            }

            if (!devices.Any())
            {
                throw new ArgumentException($"Parameter {nameof(devices)} cannot be empty.");
            }

            var exportImportDeviceList = new List<ExportImportDevice>(devices.Count());
            foreach (Device device in devices)
            {
                ValidateDeviceId(device);

                switch (importMode)
                {
                    case ImportMode.Create:
                        if (!string.IsNullOrWhiteSpace(device.ETag))
                        {
                            throw new ArgumentException(ApiResources.ETagSetWhileRegisteringDevice);
                        }
                        break;

                    case ImportMode.Update:
                        // No preconditions
                        break;

                    case ImportMode.UpdateIfMatchETag:
                        if (string.IsNullOrWhiteSpace(device.ETag))
                        {
                            throw new ArgumentException(ApiResources.ETagNotSetWhileUpdatingDevice);
                        }
                        break;

                    case ImportMode.Delete:
                        // No preconditions
                        break;

                    case ImportMode.DeleteIfMatchETag:
                        if (string.IsNullOrWhiteSpace(device.ETag))
                        {
                            throw new ArgumentException(ApiResources.ETagNotSetWhileDeletingDevice);
                        }
                        break;

                    default:
                        throw new ArgumentException(IotHubApiResources.GetString(ApiResources.InvalidImportMode, importMode));
                }

                var exportImportDevice = new ExportImportDevice(device, importMode);
                exportImportDeviceList.Add(exportImportDevice);
            }

            return exportImportDeviceList;
        }

        private static IEnumerable<ExportImportDevice> GenerateExportImportDeviceListForTwinBulkOperations(IEnumerable<Twin> twins, ImportMode importMode)
        {
            if (twins == null)
            {
                throw new ArgumentNullException(nameof(twins));
            }

            if (!twins.Any())
            {
                throw new ArgumentException($"Parameter {nameof(twins)} cannot be empty");
            }

            var exportImportDeviceList = new List<ExportImportDevice>(twins.Count());
            foreach (Twin twin in twins)
            {
                ValidateTwinId(twin);

                switch (importMode)
                {
                    case ImportMode.UpdateTwin:
                        // No preconditions
                        break;

                    case ImportMode.UpdateTwinIfMatchETag:
                        if (string.IsNullOrWhiteSpace(twin.ETag))
                        {
                            throw new ArgumentException(ApiResources.ETagNotSetWhileUpdatingTwin);
                        }
                        break;

                    default:
                        throw new ArgumentException(IotHubApiResources.GetString(ApiResources.InvalidImportMode, importMode));
                }

                var exportImportDevice = new ExportImportDevice
                {
                    Id = twin.DeviceId,
                    ModuleId = twin.ModuleId,
                    ImportMode = importMode,
                    TwinETag = importMode == ImportMode.UpdateTwinIfMatchETag ? twin.ETag : null,
                    Tags = twin.Tags,
                    Properties = new ExportImportDevice.PropertyContainer(),
                };
                exportImportDevice.Properties.DesiredProperties = twin.Properties?.Desired;

                exportImportDeviceList.Add(exportImportDevice);
            }

            return exportImportDeviceList;
        }

        private Task<T> BulkDeviceOperationsAsync<T>(IEnumerable<ExportImportDevice> devices, string version, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Performing bulk device operation on : {devices?.Count()} devices. version: {version}", nameof(BulkDeviceOperationsAsync));
            try
            {
                BulkDeviceOperationSetup(devices);

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    { HttpStatusCode.PreconditionFailed, async responseMessage => new PreconditionFailedException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false)) },
                    { HttpStatusCode.RequestEntityTooLarge, async responseMessage => new TooManyDevicesException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false)) },
                    { HttpStatusCode.BadRequest, async responseMessage => new ArgumentException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false)) }
                };

                return _httpClientHelper.PostAsync<IEnumerable<ExportImportDevice>, T>(GetBulkRequestUri(version), devices, errorMappingOverrides, null, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(BulkDeviceOperationsAsync)} threw an exception: {ex}", nameof(BulkDeviceOperationsAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Performing bulk device operation on : {devices?.Count()} devices. version: {version}", nameof(BulkDeviceOperationsAsync));
            }
        }

        private void BulkDeviceOperationSetup(IEnumerable<ExportImportDevice> devices)
        {
            EnsureInstanceNotClosed();

            if (devices == null)
            {
                throw new ArgumentNullException(nameof(devices));
            }

            foreach (ExportImportDevice device in devices)
            {
                ValidateDeviceAuthentication(device.Authentication, device.Id);

                NormalizeExportImportDevice(device);
            }
        }

        public override Task ExportRegistryAsync(string storageAccountConnectionString, string containerName)
        {
            return ExportRegistryAsync(storageAccountConnectionString, containerName, CancellationToken.None);
        }

        public override Task ExportRegistryAsync(string storageAccountConnectionString, string containerName, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Exporting registry", nameof(ExportRegistryAsync));
            try
            {
                EnsureInstanceNotClosed();

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    { HttpStatusCode.NotFound, responseMessage => Task.FromResult((Exception)new IotHubNotFoundException(_iotHubName)) }
                };

                return _httpClientHelper.PostAsync(
                    GetAdminUri("exportRegistry"),
                    new ExportImportRequest
                    {
                        ContainerName = containerName,
                        StorageConnectionString = storageAccountConnectionString,
                    },
                    errorMappingOverrides,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(ExportRegistryAsync)} threw an exception: {ex}", nameof(ExportRegistryAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Exporting registry", nameof(ExportRegistryAsync));
            }
        }

        public override Task ImportRegistryAsync(string storageAccountConnectionString, string containerName)
        {
            return ImportRegistryAsync(storageAccountConnectionString, containerName, CancellationToken.None);
        }

        public override Task ImportRegistryAsync(string storageAccountConnectionString, string containerName, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Importing registry", nameof(ImportRegistryAsync));

            try
            {
                EnsureInstanceNotClosed();

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    { HttpStatusCode.NotFound, responseMessage => Task.FromResult((Exception)new IotHubNotFoundException(_iotHubName)) }
                };

                return _httpClientHelper.PostAsync(
                    GetAdminUri("importRegistry"),
                    new ExportImportRequest
                    {
                        ContainerName = containerName,
                        StorageConnectionString = storageAccountConnectionString,
                    },
                    errorMappingOverrides,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(ImportRegistryAsync)} threw an exception: {ex}", nameof(ImportRegistryAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Importing registry", nameof(ImportRegistryAsync));
            }
        }

        public override Task<JobProperties> GetJobAsync(string jobId)
        {
            return GetJobAsync(jobId, CancellationToken.None);
        }

        public override Task<IEnumerable<JobProperties>> GetJobsAsync()
        {
            return GetJobsAsync(CancellationToken.None);
        }

        public override Task CancelJobAsync(string jobId)
        {
            return CancelJobAsync(jobId, CancellationToken.None);
        }

        public override Task<JobProperties> ExportDevicesAsync(string exportBlobContainerUri, bool excludeKeys)
        {
            return ExportDevicesAsync(
                JobProperties.CreateForExportJob(
                    exportBlobContainerUri,
                    excludeKeys));
        }

        public override Task<JobProperties> ExportDevicesAsync(string exportBlobContainerUri, bool excludeKeys, CancellationToken ct)
        {
            return ExportDevicesAsync(
                JobProperties.CreateForExportJob(
                    exportBlobContainerUri,
                    excludeKeys),
                ct);
        }

        public override Task<JobProperties> ExportDevicesAsync(string exportBlobContainerUri, string outputBlobName, bool excludeKeys)
        {
            return ExportDevicesAsync(
                JobProperties.CreateForExportJob(
                    exportBlobContainerUri,
                    excludeKeys,
                    outputBlobName));
        }

        public override Task<JobProperties> ExportDevicesAsync(string exportBlobContainerUri, string outputBlobName, bool excludeKeys, CancellationToken ct)
        {
            return ExportDevicesAsync(
                JobProperties.CreateForExportJob(
                    exportBlobContainerUri,
                    excludeKeys,
                    outputBlobName),
                ct);
        }

        public override Task<JobProperties> ExportDevicesAsync(JobProperties jobParameters, CancellationToken cancellationToken = default)
        {
            Logging.Enter(this, $"Export Job running with {jobParameters}", nameof(ExportDevicesAsync));

            try
            {
                jobParameters.Type = JobType.ExportDevices;
                return CreateJobAsync(jobParameters, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(ExportDevicesAsync)} threw an exception: {ex}", nameof(ExportDevicesAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Export Job running with {jobParameters}", nameof(ExportDevicesAsync));
            }
        }

        public override Task<JobProperties> ImportDevicesAsync(string importBlobContainerUri, string outputBlobContainerUri)
        {
            return ImportDevicesAsync(
                JobProperties.CreateForImportJob(
                    importBlobContainerUri,
                    outputBlobContainerUri));
        }

        public override Task<JobProperties> ImportDevicesAsync(string importBlobContainerUri, string outputBlobContainerUri, CancellationToken ct)
        {
            return ImportDevicesAsync(
                JobProperties.CreateForImportJob(
                    importBlobContainerUri,
                    outputBlobContainerUri),
                ct);
        }

        public override Task<JobProperties> ImportDevicesAsync(string importBlobContainerUri, string outputBlobContainerUri, string inputBlobName)
        {
            return ImportDevicesAsync(
                JobProperties.CreateForImportJob(
                    importBlobContainerUri,
                    outputBlobContainerUri,
                    inputBlobName));
        }

        public override Task<JobProperties> ImportDevicesAsync(string importBlobContainerUri, string outputBlobContainerUri, string inputBlobName, CancellationToken ct)
        {
            return ImportDevicesAsync(
                JobProperties.CreateForImportJob(
                    importBlobContainerUri,
                    outputBlobContainerUri,
                    inputBlobName),
                ct);
        }

        public override Task<JobProperties> ImportDevicesAsync(JobProperties jobParameters, CancellationToken cancellationToken = default)
        {
            Logging.Enter(this, $"Import Job running with {jobParameters}", nameof(ImportDevicesAsync));
            try
            {

                jobParameters.Type = JobType.ImportDevices;
                return CreateJobAsync(jobParameters, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(ExportDevicesAsync)} threw an exception: {ex}", nameof(ImportDevicesAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Import Job running with {jobParameters}", nameof(ImportDevicesAsync));
            }
        }

        private Task<JobProperties> CreateJobAsync(JobProperties jobProperties, CancellationToken ct)
        {
            EnsureInstanceNotClosed();

            var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
            {
                { HttpStatusCode.Forbidden, async (responseMessage) => new JobQuotaExceededException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))}
            };

            string clientApiVersion = ClientApiVersionHelper.ApiVersionQueryString;

            return _httpClientHelper.PostAsync<JobProperties, JobProperties>(
                GetJobUri("/create", clientApiVersion),
                jobProperties,
                errorMappingOverrides,
                null,
                ct);
        }

        public override Task<JobProperties> GetJobAsync(string jobId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting job {jobId}", nameof(GetJobsAsync));
            try
            {
                EnsureInstanceNotClosed();

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    { HttpStatusCode.NotFound, responseMessage => Task.FromResult((Exception)new JobNotFoundException(jobId)) }
                };

                return _httpClientHelper.GetAsync<JobProperties>(
                    GetJobUri("/{0}".FormatInvariant(jobId)),
                    errorMappingOverrides,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetJobsAsync)} threw an exception: {ex}", nameof(GetJobsAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting job {jobId}", nameof(GetJobsAsync));
            }
        }

        public override Task<IEnumerable<JobProperties>> GetJobsAsync(CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting job", nameof(GetJobsAsync));
            try
            {
                EnsureInstanceNotClosed();

                return _httpClientHelper.GetAsync<IEnumerable<JobProperties>>(
                    GetJobUri(string.Empty),
                    null,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetJobsAsync)} threw an exception: {ex}", nameof(GetJobsAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting job", nameof(GetJobsAsync));
            }
        }

        public override Task CancelJobAsync(string jobId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Canceling job: {jobId}", nameof(CancelJobAsync));
            try
            {
                EnsureInstanceNotClosed();

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    { HttpStatusCode.NotFound, responseMessage => Task.FromResult((Exception)new JobNotFoundException(jobId)) }
                };

                IETagHolder jobETag = new ETagHolder
                {
                    ETag = jobId,
                };

                return _httpClientHelper.DeleteAsync(
                    GetJobUri("/{0}".FormatInvariant(jobId)),
                    jobETag,
                    errorMappingOverrides,
                    null,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetJobsAsync)} threw an exception: {ex}", nameof(GetJobsAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting job {jobId}", nameof(GetJobsAsync));
            }
        }

        public override Task<Twin> GetTwinAsync(string deviceId)
        {
            return GetTwinAsync(deviceId, CancellationToken.None);
        }

        public override Task<Twin> GetTwinAsync(string deviceId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting device twin on device: {deviceId}", nameof(GetTwinAsync));
            try
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "deviceId"));
                }

                EnsureInstanceNotClosed();
                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>()
                {
                    { HttpStatusCode.NotFound, async responseMessage => new DeviceNotFoundException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false)) }
                };

                return _httpClientHelper.GetAsync<Twin>(GetTwinUri(deviceId), errorMappingOverrides, null, false, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetTwinAsync)} threw an exception: {ex}", nameof(GetTwinAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting device twin on device: {deviceId}", nameof(GetTwinAsync));
            }
        }

        public override Task<Twin> GetTwinAsync(string deviceId, string moduleId)
        {
            return GetTwinAsync(deviceId, moduleId, CancellationToken.None);
        }

        public override Task<Twin> GetTwinAsync(string deviceId, string moduleId, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Getting device twin on device: {deviceId} and module: {moduleId}", nameof(GetTwinAsync));

            try
            {
                if (string.IsNullOrWhiteSpace(deviceId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "deviceId"));
                }

                if (string.IsNullOrWhiteSpace(moduleId))
                {
                    throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrWhitespace, "moduleId"));
                }

                EnsureInstanceNotClosed();
                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>()
                {
                    { HttpStatusCode.NotFound, async responseMessage => new ModuleNotFoundException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false), (Exception)null) }
                };

                return _httpClientHelper.GetAsync<Twin>(GetModuleTwinRequestUri(deviceId, moduleId), errorMappingOverrides, null, false, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(GetTwinAsync)} threw an exception: {ex}", nameof(GetTwinAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Getting device twin on device: {deviceId} and module: {moduleId}", nameof(GetTwinAsync));
            }
        }

        public override Task<Twin> UpdateTwinAsync(string deviceId, string jsonTwinPatch, string etag)
        {
            return UpdateTwinAsync(deviceId, jsonTwinPatch, etag, CancellationToken.None);
        }

        public override Task<Twin> UpdateTwinAsync(string deviceId, string jsonTwinPatch, string etag, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Updating device twin on device: {deviceId}", nameof(UpdateTwinAsync));

            try
            {
                if (string.IsNullOrWhiteSpace(jsonTwinPatch))
                {
                    throw new ArgumentNullException(nameof(jsonTwinPatch));
                }

                // TODO: Do we need to deserialize Twin, only to serialize it again?
                Twin twin = JsonConvert.DeserializeObject<Twin>(jsonTwinPatch);
                return UpdateTwinAsync(deviceId, twin, etag, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(UpdateTwinAsync)} threw an exception: {ex}", nameof(UpdateTwinAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Updating device twin on device: {deviceId}", nameof(UpdateTwinAsync));
            }
        }

        public override Task<Twin> UpdateTwinAsync(string deviceId, string moduleId, Twin twinPatch, string etag)
        {
            return UpdateTwinAsync(deviceId, moduleId, twinPatch, etag, CancellationToken.None);
        }

        public override Task<Twin> UpdateTwinAsync(string deviceId, string moduleId, Twin twinPatch, string etag, CancellationToken cancellationToken)
        {
            return UpdateTwinInternalAsync(deviceId, moduleId, twinPatch, etag, false, cancellationToken);
        }

        public override Task<Twin> UpdateTwinAsync(string deviceId, string moduleId, string jsonTwinPatch, string etag)
        {
            return UpdateTwinAsync(deviceId, moduleId, jsonTwinPatch, etag, CancellationToken.None);
        }

        public override Task<Twin> UpdateTwinAsync(string deviceId, string moduleId, string jsonTwinPatch, string etag, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Updating device twin on device: {deviceId} and module: {moduleId}", nameof(UpdateTwinAsync));
            try
            {
                if (string.IsNullOrWhiteSpace(jsonTwinPatch))
                {
                    throw new ArgumentNullException(nameof(jsonTwinPatch));
                }

                // TODO: Do we need to deserialize Twin, only to serialize it again?
                Twin twin = JsonConvert.DeserializeObject<Twin>(jsonTwinPatch);
                return UpdateTwinAsync(deviceId, moduleId, twin, etag, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(UpdateTwinAsync)} threw an exception: {ex}", nameof(UpdateTwinAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Updating device twin on device: {deviceId} and module: {moduleId}", nameof(UpdateTwinAsync));
            }
        }

        public override Task<Twin> UpdateTwinAsync(string deviceId, Twin twinPatch, string etag)
        {
            return UpdateTwinAsync(deviceId, twinPatch, etag, CancellationToken.None);
        }

        public override Task<Twin> UpdateTwinAsync(string deviceId, Twin twinPatch, string etag, CancellationToken cancellationToken)
        {
            return UpdateTwinInternalAsync(deviceId, twinPatch, etag, false, cancellationToken);
        }

        public override Task<Twin> ReplaceTwinAsync(string deviceId, string newTwinJson, string etag)
        {
            return ReplaceTwinAsync(deviceId, newTwinJson, etag, CancellationToken.None);
        }

        public override Task<Twin> ReplaceTwinAsync(string deviceId, string newTwinJson, string etag, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Replacing device twin on device: {deviceId}", nameof(ReplaceTwinAsync));
            try
            {
                if (string.IsNullOrWhiteSpace(newTwinJson))
                {
                    throw new ArgumentNullException(nameof(newTwinJson));
                }

                // TODO: Do we need to deserialize Twin, only to serialize it again?
                Twin twin = JsonConvert.DeserializeObject<Twin>(newTwinJson);
                return ReplaceTwinAsync(deviceId, twin, etag, cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(ReplaceTwinAsync)} threw an exception: {ex}", nameof(ReplaceTwinAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Replacing device twin on device: {deviceId}", nameof(ReplaceTwinAsync));
            }
        }

        public override Task<Twin> ReplaceTwinAsync(string deviceId, string moduleId, Twin newTwin, string etag)
        {
            return ReplaceTwinAsync(deviceId, moduleId, newTwin, etag, CancellationToken.None);
        }

        public override Task<Twin> ReplaceTwinAsync(string deviceId, string moduleId, Twin newTwin, string etag, CancellationToken cancellationToken)
        {
            return UpdateTwinInternalAsync(deviceId, moduleId, newTwin, etag, true, cancellationToken);
        }

        public override Task<Twin> ReplaceTwinAsync(string deviceId, string moduleId, string newTwinJson, string etag)
        {
            return ReplaceTwinAsync(deviceId, moduleId, newTwinJson, etag, CancellationToken.None);
        }

        public override Task<Twin> ReplaceTwinAsync(string deviceId, string moduleId, string newTwinJson, string etag, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(newTwinJson))
            {
                throw new ArgumentNullException(nameof(newTwinJson));
            }

            // TODO: Do we need to deserialize Twin, only to serialize it again?
            Twin twin = JsonConvert.DeserializeObject<Twin>(newTwinJson);
            return ReplaceTwinAsync(deviceId, moduleId, twin, etag, cancellationToken);
        }

        public override Task<Twin> ReplaceTwinAsync(string deviceId, Twin newTwin, string etag)
        {
            return ReplaceTwinAsync(deviceId, newTwin, etag, CancellationToken.None);
        }

        public override Task<Twin> ReplaceTwinAsync(string deviceId, Twin newTwin, string etag, CancellationToken cancellationToken)
        {
            return UpdateTwinInternalAsync(deviceId, newTwin, etag, true, cancellationToken);
        }

        private Task<Twin> UpdateTwinInternalAsync(string deviceId, Twin twin, string etag, bool isReplace, CancellationToken cancellationToken)
        {
            EnsureInstanceNotClosed();

            if (twin != null)
            {
                twin.DeviceId = deviceId;
            }

            ValidateTwinId(twin);

            if (string.IsNullOrEmpty(etag))
            {
                throw new ArgumentNullException(nameof(etag));
            }

            var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
            {
                {
                    HttpStatusCode.PreconditionFailed,
                    async responseMessage => new PreconditionFailedException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                },
                {
                    HttpStatusCode.NotFound,
                    async responseMessage => new DeviceNotFoundException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false), (Exception)null)
                }
            };

            return isReplace
                ? _httpClientHelper.PutAsync<Twin, Twin>(
                    GetTwinUri(deviceId),
                    twin,
                    etag,
                    etag == WildcardEtag ? PutOperationType.ForceUpdateEntity : PutOperationType.UpdateEntity,
                    errorMappingOverrides,
                    cancellationToken)
                : _httpClientHelper.PatchAsync<Twin, Twin>(
                    GetTwinUri(deviceId),
                    twin,
                    etag,
                    etag == WildcardEtag ? PutOperationType.ForceUpdateEntity : PutOperationType.UpdateEntity,
                    errorMappingOverrides,
                    cancellationToken);
        }

        private Task<Twin> UpdateTwinInternalAsync(string deviceId, string moduleId, Twin twin, string etag, bool isReplace, CancellationToken cancellationToken)
        {
            Logging.Enter(this, $"Replacing device twin on device: {deviceId} - module: {moduleId} - is replace: {isReplace}", nameof(UpdateTwinAsync));
            try
            {
                EnsureInstanceNotClosed();

                if (twin != null)
                {
                    twin.DeviceId = deviceId;
                    twin.ModuleId = moduleId;
                }

                ValidateTwinId(twin);

                if (string.IsNullOrEmpty(etag))
                {
                    throw new ArgumentNullException(nameof(etag));
                }

                var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
                {
                    {
                        HttpStatusCode.PreconditionFailed,
                        async responseMessage => new PreconditionFailedException(
                            await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                    },
                    {
                        HttpStatusCode.NotFound,
                        async responseMessage => new ModuleNotFoundException(
                            await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false),
                            (Exception)null)
                    }
                };

                return isReplace
                    ? _httpClientHelper.PutAsync<Twin, Twin>(
                        GetModuleTwinRequestUri(deviceId, moduleId),
                        twin,
                        etag,
                        etag == WildcardEtag ? PutOperationType.ForceUpdateEntity : PutOperationType.UpdateEntity,
                        errorMappingOverrides,
                        cancellationToken)
                    : _httpClientHelper.PatchAsync<Twin, Twin>(
                        GetModuleTwinRequestUri(deviceId, moduleId),
                        twin,
                        etag,
                        etag == WildcardEtag ? PutOperationType.ForceUpdateEntity : PutOperationType.UpdateEntity,
                        errorMappingOverrides,
                        cancellationToken);
            }
            catch (Exception ex)
            {
                Logging.Error(this, $"{nameof(UpdateTwinAsync)} threw an exception: {ex}", nameof(UpdateTwinAsync));
                throw;
            }
            finally
            {
                Logging.Exit(this, $"Replacing device twin on device: {deviceId} - module: {moduleId} - is replace: {isReplace}", nameof(UpdateTwinAsync));
            }
        }

        public override Task<BulkRegistryOperationResult> UpdateTwins2Async(IEnumerable<Twin> twins)
        {
            return UpdateTwins2Async(twins, false, CancellationToken.None);
        }

        public override Task<BulkRegistryOperationResult> UpdateTwins2Async(IEnumerable<Twin> twins, CancellationToken cancellationToken)
        {
            return UpdateTwins2Async(twins, false, cancellationToken);
        }

        public override Task<BulkRegistryOperationResult> UpdateTwins2Async(IEnumerable<Twin> twins, bool forceUpdate)
        {
            return UpdateTwins2Async(twins, forceUpdate, CancellationToken.None);
        }

        public override Task<BulkRegistryOperationResult> UpdateTwins2Async(IEnumerable<Twin> twins, bool forceUpdate, CancellationToken cancellationToken)
        {
            return BulkDeviceOperationsAsync<BulkRegistryOperationResult>(
                GenerateExportImportDeviceListForTwinBulkOperations(twins, forceUpdate ? ImportMode.UpdateTwin : ImportMode.UpdateTwinIfMatchETag),
                ClientApiVersionHelper.ApiVersionQueryString,
                cancellationToken);
        }

        private Task RemoveDeviceAsync(string deviceId, IETagHolder eTagHolder, CancellationToken cancellationToken)
        {
            var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
            {
                {
                    HttpStatusCode.NotFound,
                    async responseMessage =>
                        {
                            string responseContent = await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false);
                            return new DeviceNotFoundException(responseContent, (Exception) null);
                        }
                },
                {
                    HttpStatusCode.PreconditionFailed,
                    async responseMessage => new PreconditionFailedException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                },
            };

            return _httpClientHelper.DeleteAsync(GetRequestUri(deviceId), eTagHolder, errorMappingOverrides, null, cancellationToken);
        }

        private Task RemoveDeviceModuleAsync(string deviceId, string moduleId, IETagHolder eTagHolder, CancellationToken cancellationToken)
        {
            var errorMappingOverrides = new Dictionary<HttpStatusCode, Func<HttpResponseMessage, Task<Exception>>>
            {
                {
                    HttpStatusCode.NotFound,
                    async responseMessage =>
                        {
                            string responseContent = await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false);
                            return new DeviceNotFoundException(responseContent, (Exception) null);
                        }
                },
                {
                    HttpStatusCode.PreconditionFailed,
                    async responseMessage => new PreconditionFailedException(await ExceptionHandlingHelper.GetExceptionMessageAsync(responseMessage).ConfigureAwait(false))
                },
            };

            return _httpClientHelper.DeleteAsync(GetModulesRequestUri(deviceId, moduleId), eTagHolder, errorMappingOverrides, null, cancellationToken);
        }

        private static Uri GetRequestUri(string deviceId)
        {
            deviceId = WebUtility.UrlEncode(deviceId);
            return new Uri(RequestUriFormat.FormatInvariant(deviceId, ClientApiVersionHelper.ApiVersionQueryString), UriKind.Relative);
        }

        private static Uri GetModulesRequestUri(string deviceId, string moduleId)
        {
            deviceId = WebUtility.UrlEncode(deviceId);
            moduleId = WebUtility.UrlEncode(moduleId);
            return new Uri(ModulesRequestUriFormat.FormatInvariant(deviceId, moduleId, ClientApiVersionHelper.ApiVersionQueryString), UriKind.Relative);
        }

        private static Uri GetModulesOnDeviceRequestUri(string deviceId)
        {
            deviceId = WebUtility.UrlEncode(deviceId);
            return new Uri(ModulesOnDeviceRequestUriFormat.FormatInvariant(deviceId, ClientApiVersionHelper.ApiVersionQueryString), UriKind.Relative);
        }

        private static Uri GetModuleTwinRequestUri(string deviceId, string moduleId)
        {
            deviceId = WebUtility.UrlEncode(deviceId);
            moduleId = WebUtility.UrlEncode(moduleId);
            return new Uri(ModuleTwinUriFormat.FormatInvariant(deviceId, moduleId, ClientApiVersionHelper.ApiVersionQueryString), UriKind.Relative);
        }

        private static Uri GetConfigurationRequestUri(string configurationId)
        {
            configurationId = WebUtility.UrlEncode(configurationId);
            return new Uri(ConfigurationRequestUriFormat.FormatInvariant(configurationId, ClientApiVersionHelper.ApiVersionQueryString), UriKind.Relative);
        }

        private static Uri GetConfigurationsRequestUri(int maxCount)
        {
            return new Uri(ConfigurationsRequestUriFormat.FormatInvariant(maxCount, ClientApiVersionHelper.ApiVersionQueryString), UriKind.Relative);
        }

        private static Uri GetApplyConfigurationOnDeviceRequestUri(string deviceId)
        {
            return new Uri(ApplyConfigurationOnDeviceUriFormat.FormatInvariant(deviceId), UriKind.Relative);
        }

        private static Uri GetBulkRequestUri(string apiVersionQueryString)
        {
            return new Uri(RequestUriFormat.FormatInvariant(string.Empty, apiVersionQueryString), UriKind.Relative);
        }

        private static Uri GetJobUri(string jobId, string apiVersion = ClientApiVersionHelper.ApiVersionQueryString)
        {
            return new Uri(JobsUriFormat.FormatInvariant(jobId, apiVersion), UriKind.Relative);
        }

        private static Uri GetDevicesRequestUri(int maxCount)
        {
            return new Uri(DevicesRequestUriFormat.FormatInvariant(maxCount, ClientApiVersionHelper.ApiVersionQueryString), UriKind.Relative);
        }

        private static Uri QueryDevicesRequestUri()
        {
            return new Uri(DevicesQueryUriFormat, UriKind.Relative);
        }

        private static Uri GetAdminUri(string operation)
        {
            return new Uri(AdminUriFormat.FormatInvariant(operation, ClientApiVersionHelper.ApiVersionQueryString), UriKind.Relative);
        }

        private static Uri GetStatisticsUri()
        {
            return new Uri(StatisticsUriFormat, UriKind.Relative);
        }

        private static Uri GetTwinUri(string deviceId)
        {
            deviceId = WebUtility.UrlEncode(deviceId);
            return new Uri(TwinUriFormat.FormatInvariant(deviceId, ClientApiVersionHelper.ApiVersionQueryString), UriKind.Relative);
        }

        private static void ValidateDeviceId(Device device)
        {
            if (device == null)
            {
                throw new ArgumentNullException(nameof(device));
            }

            if (string.IsNullOrWhiteSpace(device.Id))
            {
                throw new ArgumentException("device.Id");
            }

            if (!s_deviceIdRegex.IsMatch(device.Id))
            {
                throw new ArgumentException(ApiResources.DeviceIdInvalid.FormatInvariant(device.Id));
            }
        }

        private static void ValidateTwinId(Twin twin)
        {
            if (twin == null)
            {
                throw new ArgumentNullException(nameof(twin));
            }

            if (string.IsNullOrWhiteSpace(twin.DeviceId))
            {
                throw new ArgumentException("twin.DeviceId");
            }

            if (!s_deviceIdRegex.IsMatch(twin.DeviceId))
            {
                throw new ArgumentException(ApiResources.DeviceIdInvalid.FormatInvariant(twin.DeviceId));
            }
        }

        private static void ValidateModuleId(Module module)
        {
            if (module == null)
            {
                throw new ArgumentNullException(nameof(module));
            }

            if (string.IsNullOrWhiteSpace(module.DeviceId))
            {
                throw new ArgumentException("module.Id");
            }

            if (string.IsNullOrWhiteSpace(module.Id))
            {
                throw new ArgumentException("module.ModuleId");
            }

            if (!s_deviceIdRegex.IsMatch(module.DeviceId))
            {
                throw new ArgumentException(ApiResources.DeviceIdInvalid.FormatInvariant(module.DeviceId));
            }

            if (!s_deviceIdRegex.IsMatch(module.Id))
            {
                throw new ArgumentException(ApiResources.DeviceIdInvalid.FormatInvariant(module.Id));
            }
        }

        private static void ValidateDeviceAuthentication(AuthenticationMechanism authentication, string deviceId)
        {
            if (authentication != null)
            {
                // Both symmetric keys and X.509 cert thumbprints cannot be specified for the same device
                bool symmetricKeyIsSet = !authentication.SymmetricKey?.IsEmpty() ?? false;
                bool x509ThumbprintIsSet = !authentication.X509Thumbprint?.IsEmpty() ?? false;

                if (symmetricKeyIsSet && x509ThumbprintIsSet)
                {
                    throw new ArgumentException(ApiResources.DeviceAuthenticationInvalid.FormatInvariant(deviceId ?? string.Empty));
                }

                // Validate X.509 thumbprints or SymmetricKeys since we should not have both at the same time
                if (x509ThumbprintIsSet)
                {
                    authentication.X509Thumbprint.IsValid(true);
                }
                else if (symmetricKeyIsSet)
                {
                    authentication.SymmetricKey.IsValid(true);
                }
            }
        }

        private void EnsureInstanceNotClosed()
        {
            if (_httpClientHelper == null)
            {
                throw new ObjectDisposedException("RegistryManager", ApiResources.RegistryManagerInstanceAlreadyClosed);
            }
        }

        private async Task<QueryResult> ExecuteQueryAsync(string sqlQueryString, int? pageSize, string continuationToken, CancellationToken cancellationToken)
        {
            EnsureInstanceNotClosed();

            if (string.IsNullOrWhiteSpace(sqlQueryString))
            {
                throw new ArgumentException(IotHubApiResources.GetString(ApiResources.ParameterCannotBeNullOrEmpty, nameof(sqlQueryString)));
            }

            var customHeaders = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(continuationToken))
            {
                customHeaders.Add(ContinuationTokenHeader, continuationToken);
            }

            if (pageSize != null)
            {
                customHeaders.Add(PageSizeHeader, pageSize.ToString());
            }

            HttpResponseMessage response = await _httpClientHelper
                .PostAsync(
                    QueryDevicesRequestUri(),
                    new QuerySpecification { Sql = sqlQueryString },
                    null,
                    customHeaders,
                    new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" },
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            return await QueryResult.FromHttpResponseAsync(response).ConfigureAwait(false);
        }

        private static void NormalizeExportImportDevice(ExportImportDevice device)
        {
            // auto generate keys if not specified
            if (device.Authentication == null)
            {
                device.Authentication = new AuthenticationMechanism();
            }

            NormalizeAuthenticationInfo(device.Authentication);
        }

        private static void NormalizeDevice(Device device)
        {
            // auto generate keys if not specified
            if (device.Authentication == null)
            {
                device.Authentication = new AuthenticationMechanism();
            }

            NormalizeAuthenticationInfo(device.Authentication);
        }

        private static void NormalizeAuthenticationInfo(AuthenticationMechanism authenticationInfo)
        {
            //to make it backward compatible we set the type according to the values
            //we don't set CA type - that has to be explicit
            if (authenticationInfo.SymmetricKey != null && !authenticationInfo.SymmetricKey.IsEmpty())
            {
                authenticationInfo.Type = AuthenticationType.Sas;
            }

            if (authenticationInfo.X509Thumbprint != null && !authenticationInfo.X509Thumbprint.IsEmpty())
            {
                authenticationInfo.Type = AuthenticationType.SelfSigned;
            }
        }
    }
}
