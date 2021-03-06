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
        public dynamic Value { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="valueToConvert"></param>
        public WritableProperty(dynamic valueToConvert)
        {
            Value = valueToConvert;
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

        private static WritableProperty CreateResponse(HttpStatusCode httpStatusCode, dynamic value, long version, string message)
        {
            return new WritableProperty(value)
            {
                AckCode = (int)httpStatusCode,
                AckDescription = message,
                AckVersion = version
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="version"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static WritableProperty CreateOKResponse(dynamic value, long version, string message = default)
        {
            return CreateResponse(HttpStatusCode.OK, value, version, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="version"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static WritableProperty CreateAcceptedResponse(dynamic value, long version, string message = default)
        {
            return CreateResponse(HttpStatusCode.Accepted, value, version, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="version"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static WritableProperty CreateBadRequestResponse(dynamic value, long version, string message = default)
        {
            return CreateResponse(HttpStatusCode.BadRequest, value, version, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="version"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static WritableProperty CreateNotFoundResponse(dynamic value, long version, string message = default)
        {
            return CreateResponse(HttpStatusCode.NotFound, value, version, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="version"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static WritableProperty CreateConflictResponse(dynamic value, long version, string message = default)
        {
            return CreateResponse(HttpStatusCode.Conflict, value, version, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="version"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static WritableProperty CreateInternalErrorResponse(dynamic value, long version, string message = default)
        {
            return CreateResponse(HttpStatusCode.InternalServerError, value, version, message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="version"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public static WritableProperty CreateNotImplementedResponse(dynamic value, long version, string message = default)
        {
            return CreateResponse(HttpStatusCode.NotImplemented, value, version, message);
        }
    }

}
