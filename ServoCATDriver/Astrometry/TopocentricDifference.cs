#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using MathNet.Spatial.Euclidean;
using System;

namespace ASCOM.ghilios.ServoCAT.Astrometry {

    public class TopocentricDifference {
        public static readonly TopocentricDifference ZERO = new TopocentricDifference(Angle.ZERO, Angle.ZERO);

        public Angle RotationAngle { get; private set; }
        public Angle AltDelta { get; private set; }
        public Angle AzDelta { get; private set; }

        public TopocentricDifference(Angle altDelta, Angle azDelta) {
            this.AltDelta = altDelta;
            this.AzDelta = azDelta;
            this.RotationAngle = CalculateRotationAngle(altDelta: altDelta, azDelta: azDelta);
        }

        private static Angle CalculateRotationAngle(Angle altDelta, Angle azDelta) {
            var now = DateTime.Now;
            var offset = new TopocentricCoordinates(altDelta, azDelta, Angle.ZERO, Angle.ZERO, 0.0, now).ToUnitCartesian();
            var zero = new TopocentricCoordinates(Angle.ZERO, Angle.ZERO, Angle.ZERO, Angle.ZERO, 0.0d, now).ToUnitCartesian();
            return Angle.ByRadians(offset.AngleTo(zero).Radians);
        }

        public TopocentricCoordinates Rotate(TopocentricCoordinates tc, bool negate) {
            var negateFactor = negate ? -1.0d : 1.0d;
            var azDelta = this.AzDelta.Radians * negateFactor;
            var altDelta = this.AltDelta.Radians * negateFactor;

            var targetAlt = tc.Altitude.Radians + altDelta;
            var targetAz = tc.Azimuth.Radians + azDelta;
            if (targetAlt > Angle.HALF_PI) {
                targetAz += Math.PI;
                targetAlt = Math.PI - targetAlt;
            } else if (targetAlt < -Angle.HALF_PI) {
                targetAz += Math.PI;
                targetAlt = targetAlt - Math.PI;
            }

            var azAngle = Angle.ByRadians(targetAz).ToNormal();
            var altAngle = Angle.ByRadians(targetAlt);
            return new TopocentricCoordinates(altitude: altAngle, azimuth: azAngle, latitude: tc.Latitude, longitude: tc.Longitude, elevation: tc.Elevation, referenceDateTime: tc.ReferenceDateTime);
        }

        public static TopocentricDifference Difference(TopocentricCoordinates lhs, TopocentricCoordinates rhs) {
            return new TopocentricDifference(
                altDelta: Angle.ByRadians(lhs.Altitude.Radians - rhs.Altitude.Radians),
                azDelta: Angle.ByRadians(lhs.Azimuth.Radians - rhs.Azimuth.Radians));
        }

        public override string ToString() {
            return $"Angle: {RotationAngle.DMS}, AltDelta: {AltDelta.DMS}, AzDelta: {AzDelta.DMS}";
        }
    }
}