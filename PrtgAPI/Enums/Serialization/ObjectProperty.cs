﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrtgAPI
{
    /// <summary>
    /// Specifies the settings of objects that can be interfaced with using the PRTG HTTP API.
    /// </summary>
    public enum ObjectProperty
    {
        /*Name,
        Tags,
        Priority,
        DebugMode,
        WmiMode,*/

        /// <summary>
        /// Whether to inherit this object's scanning interval settings from its parent.
        /// </summary>
        [Description("intervalgroup")]
        InheritScanningInterval,

        /// <summary>
        /// The <see cref="PrtgAPI.ScanningInterval"/> with which an object refreshes its data.
        /// </summary>
        [Description("interval")]
        ScanningInterval,

        /// <summary>
        /// The <see cref="PrtgAPI.ErrorIntervalDown"/> indicating the number of scanning intervals to wait before setting a sensor to <see cref="SensorStatus.Down"/> when an error is reported.
        /// </summary>
        [Description("errorintervalsdown_")]
        ErrorIntervalDown
    }
}