using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.Devices.Shared
{
    /// <summary>
    /// The abstract base class 
    /// </summary>
    public abstract class Component
    {
        /// <summary>
        /// 
        /// </summary>
        public string Name { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        public Component Parent { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public IDictionary<string, TwinCollection> Properties { get; private set; } = new Dictionary<string, TwinCollection>();

        /// <summary>
        /// 
        /// </summary>
        public IDictionary<string, Action<string, string, WritablePropertyResponse>> WritablePropertyCallbacks { get; private set; } = new Dictionary<string, Action<string, string, WritablePropertyResponse>>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        public void AddProperty(string propertyName, TwinCollection propertyValue)
        {
            if (!string.IsNullOrEmpty(propertyName ?? throw new ArgumentNullException(nameof(propertyName), "You must specify a non-null property name to be added to the reporeted properties collection.")))
            {
                // Throws if the value is already in the dictionary.
                Properties.Add(propertyName, propertyValue);
            }
            throw new ArgumentException(nameof(propertyName), "You must specify a non-empty property name to be added to the properties collection.");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="propertyName"></param>
        /// <param name="propertyValue"></param>
        /// <param name="properyCallback"></param>
        public void AddExternallyWritableProperty(string propertyName, TwinCollection propertyValue, Action<string, string, WritablePropertyResponse> properyCallback = default)
        {
            if (!string.IsNullOrEmpty(propertyName ?? throw new ArgumentNullException(nameof(propertyName), "You must specify a non-null property name to be added to the externally writable properties collection.")))
            {
                AddProperty(propertyName, propertyValue);
                // Throws if the value is already in the dictionary.
                WritablePropertyCallbacks.Add(propertyName, properyCallback);
            }
            throw new ArgumentException(nameof(propertyName), "You must specify a non-empty property name to be added to the properties collection.");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class DefaultComponent : Component
    {
        /// <summary>
        /// 
        /// </summary>
        public DefaultComponent() { }
    }

    /// <summary>
    /// 
    /// </summary>
    public class NamedComponent : Component
    {
        /// <summary>
        /// 
        /// </summary>
        public NamedComponent(string name) { Name = name; }
    }
}
