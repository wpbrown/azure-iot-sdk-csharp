using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.Devices.Shared
{
    /// <summary>
    /// 
    /// </summary>
    public class WritablePropertyResponse
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="valueToConvert"></param>
        /// <param name="ackCode"></param>
        /// <param name="ackVersion"></param>
        public WritablePropertyResponse(object valueToConvert, int ackCode, int ackVersion)
        {
            Value = valueToConvert;
            AckCode = ackCode;
            AckVersion = ackVersion;
        }

        /// <summary>
        /// The unserialized property value.
        /// </summary>
        [JsonProperty("value")]
        public object Value { get; set; }

        /// <summary>
        /// The acknowledgement code, usually an HTTP Status Code e.g. 200, 400.
        /// </summary>
        [JsonProperty("ac")]
        public int AckCode { get; set; }

        /// <summary>
        /// The acknowledgement version, as supplied in the property update request.
        /// </summary>
        [JsonProperty("av")]
        public long AckVersion { get; set; }

        /// <summary>
        /// The acknowledgement description, an optional, human-readable message about the result of the property update.
        /// </summary>
        [JsonProperty("ad", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string AckDescription { get; set; }
    }

}
