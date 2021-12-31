#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ASCOM.ghilios.ServoCAT.ValidationRules {

    public class DoubleRangeRule : ValidationRule {
        public DoubleRangeChecker ValidRange { get; set; }

        public override ValidationResult Validate(object value, CultureInfo cultureInfo) {
            double parameter = 0;

            try {
                if (("" + value).Length > 0) {
                    parameter = double.Parse(value.ToString(), NumberStyles.Number, cultureInfo);
                }
            } catch (Exception e) {
                return new ValidationResult(false, $"Illegal characters or {e.Message}");
            }

            if (((parameter < ValidRange.Minimum) || (parameter > ValidRange.Maximum)) && parameter != (double)-1) {
                return new ValidationResult(false, $"Value must be between {ValidRange.Minimum} - {ValidRange.Maximum}");
            }
            return new ValidationResult(true, null);
        }
    }

    public class DoubleRangeChecker : DependencyObject {

        public double Minimum {
            get { return (double)GetValue(MinimumProperty); }
            set { SetValue(MinimumProperty, value); }
        }

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(DoubleRangeChecker), new UIPropertyMetadata(double.MinValue));

        public double Maximum {
            get { return (double)GetValue(MaximumProperty); }
            set { SetValue(MaximumProperty, value); }
        }

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(DoubleRangeChecker), new UIPropertyMetadata(double.MaxValue));
    }
}