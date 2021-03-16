using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Microsoft.Azure.Devices.Client
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class CommandRequest
    {
        private const char CommandDelimiter = '*';

        /// <summary>
        /// </summary>
        /// <param name="name"></param>
        public CommandRequest(string name) : this(name, null)
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        public CommandRequest(string name, dynamic data)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            string[] parts = name.Split(CommandDelimiter);
            if (parts.Length > 1)
            {
                ComponentName = parts[0];
                CommandName = parts[1];
            }
            else
            {
                ComponentName = null;
                CommandName = name;
            }

            Data = data;
        }

        /// <summary>
        /// </summary>
        public string CommandName { get; private set; }

        /// <summary>
        /// </summary>
        public string ComponentName { get; private set; }

        /// <summary>
        /// </summary>
        public dynamic Data { get; private set; }
    }
}
