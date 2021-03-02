using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Devices.Shared
{
    /// <summary>
    /// 
    /// </summary>
    public interface ISerializableSchema
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        string Serialize();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ISerializableSchema<T>
    {
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        string Serialize();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        T Deserialize(string input);
    }
}
