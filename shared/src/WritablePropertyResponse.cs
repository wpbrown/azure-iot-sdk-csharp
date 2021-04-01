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
        /// Empty constructor.
        /// </summary>
        public WritablePropertyResponse() { }

        /// <summary>
        /// Convenience constructor for specifying the properties.
        /// </summary>
        /// <param name="propertyValue">The unserialized property value.</param>
        /// <param name="ackCode">The acknowledgement code, usually an HTTP Status Code e.g. 200, 400.</param>
        /// <param name="ackVersion">The acknowledgement version, as supplied in the property update request.</param>
        /// <param name="ackDescription">The acknowledgement description, an optional, human-readable message about the result of the property update.</param>
        public WritablePropertyResponse(object propertyValue, int ackCode, long ackVersion, string ackDescription = null)
        {
            Value = propertyValue;
            AckCode = ackCode;
            AckVersion = ackVersion;
            AckDescription = ackDescription;
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
