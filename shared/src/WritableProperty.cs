using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Microsoft.Azure.Devices.Shared
{
    /// <summary>
    /// 
    /// </summary>
    public class WritableProperty
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="valueToConvert"></param>
        /// <param name="ackCode"></param>
        /// <param name="ackVersion"></param>
        public WritableProperty(dynamic valueToConvert, int ackCode, int ackVersion)
        {
            Value = valueToConvert;
            AckCode = ackCode;
            AckVersion = ackVersion;
        }

        /// <summary>
        /// The payload for this writable telemetry.
        /// </summary>
        public dynamic Value { get; set; }

        /// <summary>
        /// The acknowledgement code, usually an HTTP Status Code e.g. 200, 400.
        /// </summary>
        public int AckCode { get; set; }

        /// <summary>
        /// The acknowledgement version, as supplied in the property update request.
        /// </summary>
        public long AckVersion { get; set; }

        /// <summary>
        /// The acknowledgement description, an optional, human-readable message about the result of the property update.
        /// </summary>
        public string AckDescription { get; set; }
    }

}
