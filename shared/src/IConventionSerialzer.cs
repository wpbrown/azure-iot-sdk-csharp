using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Devices.Shared
{
    /// <summary>
    /// Implements both convention and serialization 
    /// </summary>
    public interface IConventionHandler : ISerialzier, IConventionContent
    {
        /// <summary>
        /// Returns the byte array for the convention based message
        /// </summary>
        /// <param name="objectToSendWithConvention"></param>
        /// <returns></returns>
        byte[] GetObjectBytes(object objectToSendWithConvention);
    }

    /// <summary>
    /// Sets the content type and content encoding of the convention
    /// </summary>
    public interface IConventionContent
    {
        /// <summary>
        /// The key for a component identifier within a property update patch. Corresponding value is <see cref="ComponentIdentifierValue"/>.
        /// </summary>
        static string ComponentIdentifierKey { get; }

        /// <summary>
        /// The value for a component identifier within a property update patch. Corresponding key is <see cref="ComponentIdentifierKey"/>.
        /// </summary>
        static string ComponentIdentifierValue { get; }

        /// <summary>
        /// Used by the Message class to specify what encoding to expect
        /// </summary>
        Encoding ContentEncoding { get; set; }

        /// <summary>
        /// Used by the Message class to specify what type of content to expect
        /// </summary>
        string ContentType { get; set; }

        /// <summary>
        /// Outputs an encoded byte array for the Message
        /// </summary>
        /// <param name="contentPayload">The contents of the message payload</param>
        /// <returns></returns>

        byte[] EncodeStringToByteArray(string contentPayload);

    }

    /// <summary>
    /// Provides the serialzation for a specified convention
    /// </summary>
    public interface ISerialzier
    {
        /// <summary>
        /// Outputs a serialized string the Message
        /// </summary>
        /// <param name="objectToSerialize"></param>
        /// <returns></returns>
        string SerializeToString(object objectToSerialize);
    }

}
