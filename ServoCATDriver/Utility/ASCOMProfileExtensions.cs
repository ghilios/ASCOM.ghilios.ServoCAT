#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Utilities.Interfaces;
using System.Globalization;

namespace ASCOM.Joko.ServoCAT.Utility {

    public static class ASCOMProfileExtensions {

        public static double GetDouble(this IProfile profile, string driverId, string name, string subkey, double defaultvalue) {
            if (double.TryParse(profile.GetValue(driverId, name, subkey, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)) {
                return result;
            }
            return defaultvalue;
        }

        public static void WriteDouble(this IProfile profile, string driverId, string name, string subkey, double value) {
            profile.WriteValue(driverId, name, value.ToString(CultureInfo.InvariantCulture), subkey);
        }

        public static bool GetBool(this IProfile profile, string driverId, string name, string subkey, bool defaultvalue) {
            if (bool.TryParse(profile.GetValue(driverId, name, subkey, ""), out var result)) {
                return result;
            }
            return defaultvalue;
        }

        public static void WriteBool(this IProfile profile, string driverId, string name, string subkey, bool value) {
            profile.WriteValue(driverId, name, value.ToString(CultureInfo.InvariantCulture), subkey);
        }

        public static string GetString(this IProfile profile, string driverId, string name, string subkey, string defaultvalue) {
            return profile.GetValue(driverId, name, subkey, defaultvalue);
        }

        public static void WriteString(this IProfile profile, string driverId, string name, string subkey, string value) {
            profile.WriteValue(driverId, name, value, subkey);
        }
    }
}