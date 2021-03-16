using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace Microsoft.Azure.Devices.Client
{
    /// <summary>
    /// 
    /// </summary>
    public class CommandResponse : MethodResponse
    {
        /// <summary>
        /// </summary>
        /// <param name="result">this needs to be json</param>
        /// <param name="status"></param>
        /// <returns></returns>
        public CommandResponse(object result, int status) : base (ConvertToByteArray(result), status)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="status"></param>
        public CommandResponse(int status) : base (status)
        {
        }

        private static byte[] ConvertToByteArray(object result)
        {
            var bf = new BinaryFormatter();
            using var ms = new MemoryStream();
            bf.Serialize(ms, result);
            return ms.ToArray();
        }
    }
}
