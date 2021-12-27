#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;

namespace ASCOM.Joko.ServoCAT.Astrometry {

    public class Angle {
        public const double TWO_PI = 2.0d * Math.PI;
        public const double HALF_PI = Math.PI / 2.0d;

        private Angle(double radians) {
            this.Radians = radians;
        }

        public static Angle ByDegree(double degree) {
            return new Angle(DegreesToRadians(degree));
        }

        public static Angle ByRadians(double radians) {
            return new Angle(radians);
        }

        public static Angle ByHours(double hours) {
            return new Angle(HoursToRadians(hours));
        }

        public static double DegreeToArcmin(double degree) {
            return degree * 60d;
        }

        public static double DegreeToArcsec(double degree) {
            return degree * 3600d;
        }

        public static double ArcminToDegree(double arcmin) {
            return arcmin / 60d;
        }

        public Angle ToNormal() {
            if (Radians >= 0.0d && Radians < TWO_PI) {
                return this;
            }

            var radians = Radians % TWO_PI;
            if (radians < 0) {
                radians += TWO_PI;
            }
            return new Angle(radians);
        }

        public Angle ToAbsolute() {
            if (Radians >= 0.0d) {
                return this;
            }
            return new Angle(Math.Abs(Radians));
        }

        public static double DegreesToRadians(double degree) {
            return degree / 180.0d * Math.PI;
        }

        public static double HoursToRadians(double hours) {
            return hours / 12.0d * Math.PI;
        }

        public static double RadiansToDegrees(double radians) {
            return radians / Math.PI * 180.0d;
        }

        public static double RadiansToHours(double radians) {
            return radians / Math.PI * 12.0d;
        }

        public double Radians { get; private set; }

        public double Degrees => RadiansToDegrees(Radians);

        public double Hours => RadiansToHours(Radians);

        public bool Positive => Radians > 0.0d;

        public bool Negative => Radians < 0.0d;

        public bool NonNegative => Radians >= 0.0d;

        public string DMS {
            get => Format(Degrees, "{0}{1:00}° {2:00}' {3:00}\"");
        }

        public string HMS {
            get => Format(Hours, "{0}{1:00}:{2:00}:{3:00}");
        }

        private static string Format(double units, string pattern) {
            var negative = false;
            var degrees = units;
            if (degrees < 0) {
                negative = true;
                degrees = -degrees;
            }
            var sign = negative ? "-" : "";

            var degree = Math.Floor(degrees);
            var arcmin = Math.Floor(DegreeToArcmin(degrees - degree));
            var arcminDeg = ArcminToDegree(arcmin);
            var arcsec = Math.Round(DegreeToArcsec(degrees - degree - arcminDeg), 0);
            if (arcsec == 60) {
                arcsec = 0;
                arcmin += 1;

                if (arcmin == 60) {
                    arcmin = 0;
                    degree += 1;
                }
            }
            return string.Format(pattern, sign, degree, arcmin, arcsec);
        }

        public override string ToString() {
            return DMS;
        }
    }
}