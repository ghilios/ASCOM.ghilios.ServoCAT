#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.ghilios.ServoCAT.Astrometry;
using System;
using System.Windows.Data;

namespace ASCOM.ghilios.ServoCAT.Converters {

    [ValueConversion(typeof(Angle), typeof(string))]
    public class AngleHMSConverter : IValueConverter {

        #region IValueConverter Members

        private const string NotSetValue = " --:--:--";

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (!(value is Angle)) {
                return NotSetValue;
            }

            var angle = (Angle)value;
            if (double.IsNaN(angle.Radians)) {
                return NotSetValue;
            }
            return angle.HMS;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            throw new NotImplementedException();
        }

        #endregion IValueConverter Members
    }
}