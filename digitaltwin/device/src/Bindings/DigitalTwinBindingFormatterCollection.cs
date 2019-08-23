﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Azure.Iot.DigitalTwin.Device.Bindings
{
    /// <summary>
    /// The Digital Twin Binding Formatter Collection.
    /// </summary>
    internal class DigitalTwinBindingFormatterCollection : Collection<IDigitalTwinFormatter>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DigitalTwinBindingFormatterCollection"/> class.
        /// </summary>
        public DigitalTwinBindingFormatterCollection()
            : base(CreateDefaultFormatters())
        {
        }

        /// <summary>
        /// Serialize to string
        /// </summary>
        /// <typeparam name="T">Any class or struct</typeparam>
        /// <param name="userObject">The object needs to be serialized.</param>
        /// <returns>The serialized string.</returns>
        public string FromObject<T>(T userObject)
        {
            IDigitalTwinFormatter digitaltwinSerializer = FindSerializer();
            return digitaltwinSerializer.FromObject(userObject);
        }

        /// <summary>
        /// Serialize to string
        /// </summary>
        /// <typeparam name="T">Any class or struct</typeparam>
        /// <param name="value">The string.</param>
        /// <returns>The instance needs to be de-serialized.</returns>
        public T ToObject<T>(string value)
        {
            IDigitalTwinFormatter digitalTwinSerializer = FindSerializer();
            return digitalTwinSerializer.ToObject<T>(value);
        }

        /// <summary>
        /// Find Serializer
        /// </summary>
        /// <returns>The Digital Twin Serializer.</returns>
        public IDigitalTwinFormatter FindSerializer()
        {
            IDigitalTwinFormatter result = Items.FirstOrDefault();
            if (result == null)
            {
                throw new InvalidOperationException("Can't find the media type");
            }

            return result;
        }

        /// <summary>
        /// Create default formatters
        /// </summary>
        /// <returns>The default formatters.</returns>
        private static IList<IDigitalTwinFormatter> CreateDefaultFormatters()
        {
            return new List<IDigitalTwinFormatter>
            {
                new DigitalTwinJsonFormatter(),
            };
        }
    }
}
