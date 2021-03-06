﻿namespace PrtgAPI
{
    /// <summary>
    /// Represents a child of a <see cref="IPrtgObject"/> that is not globally identifiable and may exist multiple times across different <see cref="IPrtgObject"/> instances.
    /// </summary>
    public interface ISubObject : IObject
    {
        /// <summary>
        /// The identifier of this object under its parent object.
        /// </summary>
        int SubId { get; set; }
    }
}