using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Microsoft.Azure.Devices.Shared
{
    /// <inheritdoc/>
    public class DefaultConvention
        : IConventionHandler
    {
        private DefaultConvention() { }

        /// <summary>
        /// Default instance 
        /// </summary>
        public static readonly DefaultConvention Instance = new DefaultConvention();

        /// <inheritdoc/>
        public string ComponentIdentifierKey { get { return "__t";  } set { } }

        /// <inheritdoc/>
        public string ComponentIdentifierValue { get { return "c"; } set { } }

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
