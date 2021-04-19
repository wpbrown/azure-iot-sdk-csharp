﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.Devices.Client
{
    /// <summary>
    ///
    /// </summary>
    public class PropertyConvention : DefaultPayloadConvention
    {
        /// <summary>
        ///
        /// </summary>
        internal static string ComponentIdentifierKey => "__t";

        /// <summary>
        ///
        /// </summary>
        internal static string ComponentIdentifierValue => "c";

        /// <summary>
        ///
        /// </summary>
        public new static readonly PropertyConvention Instance = new PropertyConvention();
    }
}