using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.Devices.Shared
{
    /// <inheritdoc/>
    public class DefaultTelemetryConventionHandler
        : IConventionHandler
    {
        private DefaultTelemetryConventionHandler() { }

        /// <summary>
        /// Default instance 
        /// </summary>
        public static readonly DefaultTelemetryConventionHandler Instance = new DefaultTelemetryConventionHandler();

        /// <inheritdoc/>
        public Encoding ContentEncoding { get; set; } = Encoding.UTF8;
        /// <inheritdoc/>
        public string ContentType { get; set; } = "application/json";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectToSerialize"></param>
        /// <returns></returns>
        public string SerializeToString(object objectToSerialize)
        {
            return Newtonsoft.Json.JsonConvert.SerializeObject(objectToSerialize);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="contentPayload"></param>
        /// <returns></returns>
        public byte[] EncodeStringToByteArray(string contentPayload)
        {
            return ContentEncoding.GetBytes(contentPayload);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="objectToSendWithConvention"></param>
        /// <returns></returns>
        public byte[] GetObjectBytes(object objectToSendWithConvention)
        {
            return EncodeStringToByteArray(SerializeToString(objectToSendWithConvention));
        }
    }
}
