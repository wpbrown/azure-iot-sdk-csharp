﻿using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.Devices.Shared
{
    /// <summary>
    /// A container for properties for your device
    /// </summary>
    public class Properties
    {
        private PropertyCollection _readOnlyProperties;
        /// <summary>
        /// Initializes a new instance of <see cref="Properties"/>
        /// </summary>
        public Properties()
        {
            Writable = new PropertyCollection();
            _readOnlyProperties = new PropertyCollection();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="Properties"/> with the specified collections
        /// </summary>
        /// <param name="writablePropertyCollection"></param>
        /// <param name="readOnlyPropertyCollection"></param>
        public Properties(PropertyCollection writablePropertyCollection, PropertyCollection readOnlyPropertyCollection)
        {
            Writable = writablePropertyCollection;
            _readOnlyProperties = readOnlyPropertyCollection;
        }

        /// <summary>
        /// Gets and sets the writable properties.
        /// </summary>
        [JsonProperty(PropertyName = "desired", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public PropertyCollection Writable { get; set; }

        /// <summary>
        /// Get the property from 
        /// </summary>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        public dynamic this[string propertyName]
        {
            get
            {
                return _readOnlyProperties[propertyName];
            }
        }

        /// <summary>
        /// Converts a <see cref="TwinProperties"/> collection to a properties collection
        /// </summary>
        /// <param name="twinProperties"></param>
        /// <returns></returns>
        public static Properties FromTwinProperties(TwinProperties twinProperties)
        {
            if (twinProperties == null)
            {
                throw new ArgumentNullException(nameof(twinProperties));
            }

            return new Properties()
            {
                _readOnlyProperties = (PropertyCollection)twinProperties.Reported,
                Writable = (PropertyCollection)twinProperties.Desired
            };
        }
    }
}