using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Devices.Shared
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    public class Command<TRequest, TResponse> 
        where TRequest: ISerializableSchema<TRequest> 
        where TResponse: ISerializableSchema<TResponse>
    {
        /// <summary>
        /// 
        /// </summary>
        public TRequest Request { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        public TResponse Response { get; set; }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="requestPayload"></param>
        /// <param name="responsePayload"></param>
        public Command(TRequest requestPayload, TResponse responsePayload)
        {
            Request = requestPayload;
            Response = responsePayload;
        }
    }
}
