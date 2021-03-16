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
    public sealed class CommandRequest : MethodRequest
    {
        internal CommandRequest(string commandName) : this (commandName, null)
        {
        }

        internal CommandRequest(string commandName, string componentName) : this (commandName, componentName, null)
        {

        }

        internal CommandRequest(string commandName, string componentName, object data) : base (commandName, ConvertToByteArray(data))
        {
            ComponentName = componentName;
        }

        /// <summary>
        /// </summary>
        public string ComponentName { get; private set; }

        private static byte[] ConvertToByteArray(object result)
        {
            var bf = new BinaryFormatter();
            using var ms = new MemoryStream();
            bf.Serialize(ms, result);
            return ms.ToArray();
        }
    }
}
