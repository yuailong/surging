﻿using Microsoft.Extensions.Logging;
using Surging.Core.CPlatform.Address;
using Surging.Core.CPlatform.Routing;
using Surging.Core.CPlatform.Runtime.Client.Address.Resolvers.Implementation.Selectors;
using Surging.Core.CPlatform.Runtime.Client.HealthChecks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Surging.Core.CPlatform.Runtime.Client.Address.Resolvers.Implementation
{
    /// <summary>
    /// 一个人默认的服务地址解析器。
    /// </summary>
    public class DefaultAddressResolver : IAddressResolver
    {
        #region Field

        private readonly IServiceRouteManager _serviceRouteManager;
        private readonly ILogger<DefaultAddressResolver> _logger;
        private readonly IAddressSelector _addressSelector;
        private readonly IHealthCheckService _healthCheckService;
        private readonly CPlatformContainer _container;

        #endregion Field

        #region Constructor

        public DefaultAddressResolver(IServiceRouteManager serviceRouteManager, ILogger<DefaultAddressResolver> logger, CPlatformContainer container, IHealthCheckService healthCheckService)
        {
            _container = container;
            _serviceRouteManager = serviceRouteManager;
            _logger = logger;
            _addressSelector = container.GetInstances<IAddressSelector>();
            _healthCheckService = healthCheckService;
        }

        #endregion Constructor

        #region Implementation of IAddressResolver

        /// <summary>
        /// 解析服务地址。
        /// </summary>
        /// <param name="serviceId">服务Id。</param>
        /// <returns>服务地址模型。</returns>
        public async Task<AddressModel> Resolver(string serviceId)
        {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug($"准备为服务id：{serviceId}，解析可用地址。");
            var descriptors = await _serviceRouteManager.GetRoutesAsync();
            var descriptor = descriptors.FirstOrDefault(i => i.ServiceDescriptor.Id == serviceId);

            if (descriptor == null)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning($"根据服务id：{serviceId}，找不到相关服务信息。");
                return null;
            }

            var address = new List<AddressModel>();
            foreach (var addressModel in descriptor.Address)
            {
                await _healthCheckService.Monitor(addressModel);
                if (!await _healthCheckService.IsHealth(addressModel))
                    continue;

                address.Add(addressModel);
            }

            var hasAddress = address.Any();
            if (!hasAddress)
            {
                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning($"根据服务id：{serviceId}，找不到可用的地址。");
                return null;
            }

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation($"根据服务id：{serviceId}，找到以下可用地址：{string.Join(",", address.Select(i => i.ToString()))}。");

            return await _addressSelector.SelectAsync(new AddressSelectContext
            {
                Descriptor = descriptor.ServiceDescriptor,
                Address = address
            });
        }

        #endregion Implementation of IAddressResolver
    }
}