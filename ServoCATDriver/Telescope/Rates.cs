#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.DeviceInterface;
using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Interfaces;
using ASCOM.ghilios.ServoCAT.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace ASCOM.ghilios.ServoCAT.Telescope {

    #region Rate class

    //
    // The Rate class implements IRate, and is used to hold values
    // for AxisRates. You do not need to change this class.
    //
    // The Guid attribute sets the CLSID for ASCOM.ghilios.ServoCAT.Rate
    // The ClassInterface/None attribute prevents an empty interface called
    // _Rate from being created and used as the [default] interface
    //
    [Guid("ab121b7f-0737-46f0-9a66-285562e8543b")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class Rate : IRate {
        public static double RateEpsilon = 1.0d / 60.0d; // 1 arcmin / sec

        private double maximum = 0;
        private double minimum = 0;

        //
        // Default constructor - Internal prevents public creation
        // of instances. These are values for AxisRates.
        //
        internal Rate(double minimum, double maximum) {
            this.maximum = maximum;
            this.minimum = minimum;
        }

        #region Implementation of IRate

        public void Dispose() {
        }

        public double Maximum {
            get { return this.maximum; }
            set { this.maximum = value; }
        }

        public double Minimum {
            get { return this.minimum; }
            set { this.minimum = value; }
        }

        internal static Rate SingleValue(double val) {
            return new Rate(val, val + double.Epsilon);
        }

        public bool Equals(double val) {
            return val >= Minimum && val <= Maximum;
        }

        #endregion
    }

    #endregion

    #region AxisRates

    //
    // AxisRates is a strongly-typed collection that must be enumerable by
    // both COM and .NET. The IAxisRates and IEnumerable interfaces provide
    // this polymorphism.
    //
    // The Guid attribute sets the CLSID for ASCOM.ghilios.ServoCAT.AxisRates
    // The ClassInterface/None attribute prevents an empty interface called
    // _AxisRates from being created and used as the [default] interface
    //
    [Guid("ee8a2d2b-d240-4a2a-84ad-615cf99f7bec")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class AxisRates : IAxisRates, IEnumerable {
        private TelescopeAxes axis;
        private readonly Rate[] rates;

        internal AxisRates(TelescopeAxes axis, IServoCatOptions options) {
            ServoCatFirmwareAxisConfig axisConfig;
            if (axis == TelescopeAxes.axisPrimary) {
                axisConfig = options.FirmwareConfig.AzimuthConfig;
            } else if (axis == TelescopeAxes.axisSecondary) {
                axisConfig = options.FirmwareConfig.AltitudeConfig;
            } else {
                this.rates = new Rate[0];
                return;
            }

            var guideSlowRate = axisConfig.GuideRateSlow(options.UseSpeed1);
            var guideFastRate = axisConfig.GuideRateFast(options.UseSpeed1);
            var jogRate = axisConfig.JogRate(options.UseSpeed1);
            var slewRate = axisConfig.SlewRate(options.UseSpeed1);
            var values = new List<double>() {
                0.0d, guideSlowRate.Degrees, guideFastRate.Degrees, jogRate.Degrees, slewRate.Degrees
            };

            this.axis = axis;
            this.rates = values.Distinct().Select(r => Rate.SingleValue(r)).ToArray();
        }

        #region IAxisRates Members

        public int Count {
            get { return this.rates.Length; }
        }

        public void Dispose() {
        }

        public IEnumerator GetEnumerator() {
            return rates.GetEnumerator();
        }

        public IRate this[int index] {
            get { return this.rates[index - 1]; }	// 1-based
        }

        #endregion
    }

    #endregion

    #region TrackingRates

    //
    // TrackingRates is a strongly-typed collection that must be enumerable by
    // both COM and .NET. The ITrackingRates and IEnumerable interfaces provide
    // this polymorphism.
    //
    // The Guid attribute sets the CLSID for ASCOM.ghilios.ServoCAT.TrackingRates
    // The ClassInterface/None attribute prevents an empty interface called
    // _TrackingRates from being created and used as the [default] interface
    //
    // This class is implemented in this way so that applications based on .NET 3.5
    // will work with this .NET 4.0 object.  Changes to this have proved to be challenging
    // and it is strongly suggested that it isn't changed.
    //
    [Guid("2fcc86d8-4563-4b82-847c-ef51c5b2eaf9")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class TrackingRates : ITrackingRates, IEnumerable, IEnumerator {
        private readonly DriveRates[] trackingRates;

        // this is used to make the index thread safe
        private readonly ThreadLocal<int> pos = new ThreadLocal<int>(() => { return -1; });

        private static readonly object lockObj = new object();

        //
        // Default constructor - Internal prevents public creation
        // of instances. Returned by Telescope.AxisRates.
        //
        internal TrackingRates() {
            //
            // This array must hold ONE or more DriveRates values, indicating
            // the tracking rates supported by your telescope. The one value
            // (tracking rate) that MUST be supported is driveSidereal!
            //
            this.trackingRates = new[] { DriveRates.driveSidereal };
        }

        #region ITrackingRates Members

        public int Count {
            get { return this.trackingRates.Length; }
        }

        public IEnumerator GetEnumerator() {
            pos.Value = -1;
            return this as IEnumerator;
        }

        public void Dispose() {
        }

        public DriveRates this[int index] {
            get { return this.trackingRates[index - 1]; }   // 1-based
        }

        #endregion

        #region IEnumerable members

        public object Current {
            get {
                lock (lockObj) {
                    if (pos.Value < 0 || pos.Value >= trackingRates.Length) {
                        throw new System.InvalidOperationException();
                    }
                    return trackingRates[pos.Value];
                }
            }
        }

        public bool MoveNext() {
            lock (lockObj) {
                if (++pos.Value >= trackingRates.Length) {
                    return false;
                }
                return true;
            }
        }

        public void Reset() {
            pos.Value = -1;
        }

        #endregion
    }

    #endregion
}