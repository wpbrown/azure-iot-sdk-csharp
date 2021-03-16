using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.Devices.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class CommandResponse
    {
        /// <summary>
        /// </summary>
        /// <param name="result"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        public CommandResponse(dynamic result, int status)
        {
            Result = result;
            Status = status;
        }

        /// <summary>
        /// </summary>
        /// <param name="status"></param>
        public CommandResponse(int status)
        {
            Status = status;
        }

        /// <summary>
        /// </summary>
        public dynamic Result
        {
            get; private set;
        }

        /// <summary>
        /// </summary>
        public int Status
        {
            get; private set;
        }
    }
}
