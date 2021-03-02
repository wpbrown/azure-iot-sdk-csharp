
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Devices.Shared
{
    /// <summary>
    /// 
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2225:Operator overloads have named alternates", Justification = "In order to make the API seamless we need to allow a customer to implicitly convert the value")]
    public class Property<T> where T : ISerializableSchema
    {
        private ValueType _basevalue;
        private string _baseString;
        private T _serializableObject;

        /// <summary>
        /// Checks to see if this Proeprty should return a string
        /// </summary>
        public bool ReturnAsString
        {
            get
            {
                return (_baseString != null) || (_serializableObject != null);
            }
        }

        /// <summary>
        /// the stored <see cref="ValueType"/> as the type specified in <typeparamref name="TType"/>
        /// </summary>
        /// <typeparam name="TType">A struct type such as int, double, bool</typeparam>
        /// <returns>The value type cast as <typeparamref name="TType"/></returns>
        public TType GetValue<TType>() where TType : struct
        {
            return (TType)_basevalue;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public string GetString()
        {
            if ((_serializableObject != null))
            {
                return _serializableObject.Serialize();
            }
            else if (_baseString != null)
            {
                return _baseString;
            }
            return _basevalue.ToString();

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="valueToConvert"></param>
        protected Property(ValueType valueToConvert)
        {
            _basevalue = valueToConvert;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="valueToConvert"></param>
        protected Property(string valueToConvert)
        {
            _baseString = valueToConvert;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="valueToConvert"></param>
        protected Property(T valueToConvert)
        {
            _serializableObject = valueToConvert;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(int value)
        {
            return new Property<T>(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(uint value)
        {
            return new Property<T>(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(short value)
        {
            return new Property<T>(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(ushort value)
        {
            return new Property<T>(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(double value)
        {
            return new Property<T>(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(decimal value)
        {
            return new Property<T>(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(long value)
        {
            return new Property<T>(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(ulong value)
        {
            return new Property<T>(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(string value)
        {
            return new Property<T>(value);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator Property<T>(T value)
        {
            return new Property<T>(value);
        }
    }
}
