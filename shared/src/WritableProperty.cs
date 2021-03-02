using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Devices.Shared
{
    /// <summary>
    /// 
    /// </summary>
    public class WritableProperty
    {
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

        /// <summary>
        /// The acknowledgement description, an optional, human-readable message about the result of the property update.
        /// </summary>
        public Property<ISerializableSchema> Value { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="valueToConvert"></param>
        protected WritableProperty(Property<ISerializableSchema> valueToConvert)
        {
            Value = valueToConvert;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator WritableProperty(Property<ISerializableSchema> value)
        {
            return new WritableProperty(value);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public WritableProperty CreateOKResponse()
        {
            AckCode = (int)System.Net.HttpStatusCode.OK;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public WritableProperty CreateOKResponse(string message)
        {
            AckCode = (int)System.Net.HttpStatusCode.OK;
            AckDescription = message;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public WritableProperty CreateAcceptedResponse(string message)
        {
            AckCode = (int)System.Net.HttpStatusCode.Accepted;
            AckDescription = message;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public WritableProperty CreateNotFoundResponse()
        {
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public WritableProperty CreateBadResponse(string message)
        {
            AckCode = (int)System.Net.HttpStatusCode.BadRequest;
            AckDescription = message;
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public WritableProperty CreateErrorResponse()
        {
            return this;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static WritableProperty FromProperty(Property<ISerializableSchema> value)
        {
            return new WritableProperty(value);
        }
    }

}
