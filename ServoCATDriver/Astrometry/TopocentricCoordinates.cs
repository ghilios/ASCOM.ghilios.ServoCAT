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

    public class TopocentricCoordinates {

        public TopocentricCoordinates(Angle altitude, Angle azimuth, Angle latitude, Angle longitude, double elevation, DateTime referenceDateTime) {
            this.Azimuth = azimuth.ToNormal();
            if (altitude.Radians > Angle.HALF_PI || altitude.Radians < -Angle.HALF_PI) {
                throw new ArgumentException($"{altitude.Degrees} must be within [-90, 90] to be a valid altitude coordinate");
            }
            this.Altitude = altitude;
            this.Longitude = longitude.ToNormal();
            if (latitude.Radians > Angle.HALF_PI || latitude.Radians < -Angle.HALF_PI) {
                throw new ArgumentException($"{latitude.Degrees} must be within [-90, 90] to be a valid latitude");
            }
            this.Latitude = latitude;
            this.Elevation = elevation;
            this.ReferenceDateTime = referenceDateTime;
        }

        public DateTime ReferenceDateTime { get; private set; }

        public Angle Altitude { get; private set; }

        public Angle Azimuth { get; private set; }

        public Angle Latitude { get; private set; }

        public Angle Longitude { get; private set; }

        public double Elevation { get; private set; }

        public override string ToString() {
            return $"Alt={Altitude.DMS}, Az={Azimuth.DMS}, lat={Latitude:0.0000}°, long={Longitude:0.0000}°, elevation={Elevation:0.00}m";
        }

        public Vector3D ToUnitCartesian() {
            var polarAngle = Angle.ByDegree(90.0d - Altitude.Degrees);
            var x = Math.Cos(Azimuth.Radians) * Math.Sin(polarAngle.Radians);
            var y = Math.Sin(Azimuth.Radians) * Math.Sin(polarAngle.Radians);
            var z = Math.Cos(polarAngle.Radians);
            return new Vector3D(x, y, z);
        }

        public static TopocentricCoordinates FromUnitCartesian(Vector3D coords, Angle latitude, Angle longitude, double elevation, DateTime referenceDateTime) {
            var polarAngle = Angle.ByRadians(Math.Acos(coords.Z));
            Angle azimuth;
            if (coords.X > 0) {
                azimuth = Angle.ByRadians(Math.Atan(coords.Y / coords.X));
            } else if (coords.X < 0) {
                azimuth = Angle.ByRadians(Math.Atan(coords.Y / coords.X) + Math.PI);
            } else {
                azimuth = Angle.ByRadians(Math.PI / 2.0d);
            }
            var altitude = Angle.ByDegree(90.0d - polarAngle.Degrees);
            return new TopocentricCoordinates(
                altitude: altitude,
                azimuth: azimuth,
                latitude: latitude,
                longitude: longitude,
                elevation: elevation,
                referenceDateTime: referenceDateTime);
        }
    }
}