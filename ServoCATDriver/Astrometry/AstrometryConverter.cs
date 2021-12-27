#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Astrometry.NOVAS;
using ASCOM.Astrometry.SOFA;
using ASCOM.Joko.ServoCAT.Interfaces;
using System;

namespace ASCOM.Joko.ServoCAT.Astrometry {

    public class AstrometryConverter {
        private readonly INOVAS31 novas31;
        private readonly ISOFA sofa;
        private readonly IServoCatOptions options;

        public AstrometryConverter(IServoCatOptions options, INOVAS31 novas31, ISOFA sofa) {
            this.novas31 = novas31;
            this.sofa = sofa;
            this.options = options;
        }

        public TopocentricCoordinates ToTopocentric(ICRSCoordinates coordinates, DateTime? asOf = null) {
            var transform = TransformEpoch(coordinates, Epoch.J2000);

            var now = asOf ?? DateTime.Now;
            var jdUTC = GetJulianDate(now);

            var deltaT = novas31.DeltaT(jdUTC);
            double aob = 0d, zob = 0d, hob = 0d, dob = 0d, rob = 0d, eo = 0d;
            var longitude = Angle.ByDegree(options.Longitude);
            var latitude = Angle.ByDegree(options.Latitude);

            // No refraction correction
            double pressurehPa = 0.0d;
            double tempCelcius = 0.0d;
            double relativeHumidity = 0.0d;
            double wavelength = 0.5d;
            sofa.Atco13(transform.RA.Radians, transform.Dec.Radians, 0d, 0d, 0d, 0d, jdUTC, 0d, deltaT, longitude.Radians, latitude.Radians, options.Elevation, 0d, 0d, pressurehPa, tempCelcius, relativeHumidity, wavelength, ref aob, ref zob, ref hob, ref dob, ref rob, ref eo);

            var az = Angle.ByRadians(aob);
            var alt = Angle.ByRadians(Angle.HALF_PI - zob);
            return new TopocentricCoordinates(az, alt, latitude, longitude, options.Elevation, now);
        }

        public ICRSCoordinates TransformEpoch(ICRSCoordinates coordinates, Epoch targetEpoch) {
            if (coordinates.Epoch == targetEpoch) {
                return coordinates;
            }

            if (targetEpoch == Epoch.JNOW) {
                var now = DateTime.Now;
                double jdTT = GetJdTT(now);

                double ri = 0, di = 0, eo = 0;
                sofa.Atci13(coordinates.RA.Radians, coordinates.Dec.Radians, 0.0, 0.0, 0.0, 0.0, jdTT, 0.0, ref ri, ref di, ref eo);

                var raApparent = Angle.ByRadians(sofa.Anp(ri - eo));
                var decApparent = Angle.ByRadians(di);

                return new ICRSCoordinates(raApparent, decApparent, Epoch.JNOW, now);
            } else {
                // J2000
                var jdTT = GetJdTT(coordinates.ReferenceDateTime);
                var jdUTC = GetJulianDate(coordinates.ReferenceDateTime);
                double rc = 0, dc = 0, eo = 0;
                sofa.Atic13(sofa.Anp(coordinates.RA.Radians + sofa.Eo06a(jdUTC, 0.0)), coordinates.Dec.Radians, jdTT, 0.0, ref rc, ref dc, ref eo);

                var raCelestial = Angle.ByRadians(rc);
                var decCelestial = Angle.ByRadians(dc);

                return new ICRSCoordinates(raCelestial, decCelestial, Epoch.J2000, coordinates.ReferenceDateTime);
            }
        }

        private double GetJdTT(DateTime date) {
            var utcDate = date.ToUniversalTime();
            double tai1 = 0, tai2 = 0, tt1 = 0, tt2 = 0;
            var utc = GetJulianDate(utcDate);
            sofa.UtcTai(utc, 0.0, ref tai1, ref tai2);
            sofa.TaiTt(tai1, tai2, ref tt1, ref tt2);
            return tt1 + tt2;
        }

        public double GetJulianDate(DateTime date) {
            var utcdate = date.ToUniversalTime();
            return novas31.JulianDate((short)utcdate.Year, (short)utcdate.Month, (short)utcdate.Day, utcdate.Hour + utcdate.Minute / 60.0 + utcdate.Second / 60.0 / 60.0 + utcdate.Millisecond / 60.0 / 60.0 / 1000.0);
        }
    }
}