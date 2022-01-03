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

        public static readonly TopocentricDifference ZERO = new TopocentricDifference(
            new Quaternion(1.0f, 0.0f, 0.0f, 0.0f));

        private readonly Quaternion rotation;
        public Angle RotationAngle { get; private set; }

        public TopocentricDifference(Quaternion rotation) {
            this.rotation = rotation;
            RotationAngle = Angle.ByRadians(Math.Acos(rotation.Real));
        }

        public TopocentricCoordinates Rotate(TopocentricCoordinates tc, bool negate) {
            // https://stackoverflow.com/questions/57185542/3d-rotation-using-system-numerics-quaternion
            var cartesian = tc.ToUnitCartesian();
            var rotationToUse = negate ? rotation.Negate() : rotation;
            var rotatedQuaternion = rotationToUse * new Quaternion(0, cartesian.X, cartesian.Y, cartesian.Z) * rotationToUse.Conjugate();
            var rotatedCartesian = new Vector3D(rotatedQuaternion.ImagX, rotatedQuaternion.ImagY, rotatedQuaternion.ImagZ).Normalize().ToVector3D();
            return TopocentricCoordinates.FromUnitCartesian(
                coords: rotatedCartesian,
                latitude: tc.Latitude,
                longitude: tc.Longitude,
                elevation: tc.Elevation,
                referenceDateTime: tc.ReferenceDateTime);
        }

        public static TopocentricDifference Difference(TopocentricCoordinates lhs, TopocentricCoordinates rhs) {
            // https://math.stackexchange.com/questions/114107/determine-the-rotation-necessary-to-transform-one-point-on-a-sphere-to-another
            var lhsCartesian = lhs.ToUnitCartesian();
            var rhsCartesian = rhs.ToUnitCartesian();
            var crossProduct = lhsCartesian.CrossProduct(rhsCartesian).Normalize().ToVector3D();
            if (double.IsNaN(crossProduct.X)) {
                // Cross product is NaN if the vectors are coincident
                return ZERO;
            }

            var theta = Math.Acos(lhsCartesian.DotProduct(rhsCartesian));
            var imaginaryScale = Math.Sin(theta / 2);
            var quaternion = new Quaternion(Math.Cos(theta / 2), crossProduct.X * imaginaryScale, crossProduct.Y * imaginaryScale, crossProduct.Z * imaginaryScale);
            return new TopocentricDifference(quaternion);
        }

        public override string ToString() {
            return $"Angle: {RotationAngle.DMS}, Axis: [{rotation.ImagX}, {rotation.ImagY}, {rotation.ImagZ}]";
        }
    }
}