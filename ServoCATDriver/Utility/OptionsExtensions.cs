#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Astrometry;
using ASCOM.ghilios.ServoCAT.Telescope;

namespace ASCOM.ghilios.ServoCAT.Utility {

    public static class OptionsExtensions {

        public static Angle GuideRateSlow(this ServoCatFirmwareAxisConfig axisConfig, bool useSpeed1) {
            // With speed set to 1, GuideSlow is Guide1, and GuideFast is 2x Guide1
            // With speed set to 2, GuideSlow is 0.5x Guide2, and GuideFast is Guide2
            return useSpeed1 ? axisConfig.GuideRatePerSecond1 : Angle.ByDegree(0.5 * axisConfig.GuideRatePerSecond2.Degrees);
        }

        public static Angle GuideRateFast(this ServoCatFirmwareAxisConfig axisConfig, bool useSpeed1) {
            return useSpeed1 ? Angle.ByDegree(2.0 * axisConfig.GuideRatePerSecond1.Degrees) : axisConfig.GuideRatePerSecond2;
        }

        public static Angle JogRate(this ServoCatFirmwareAxisConfig axisConfig, bool useSpeed1) {
            return useSpeed1 ? axisConfig.JogRatePerSecond1 : axisConfig.JogRatePerSecond2;
        }

        public static Angle SlewRate(this ServoCatFirmwareAxisConfig axisConfig, bool useSpeed1) {
            return useSpeed1 ? axisConfig.SlewRatePerSecond1 : axisConfig.SlewRatePerSecond2;
        }
    }
}