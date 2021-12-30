#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using ASCOM.Joko.ServoCAT.Interfaces;
using ASCOM.Utilities;

namespace ASCOM.Joko.ServoCAT.IO {

    public class SerialUtilities : ISerialUtilities {

        public string[] GetAvailableCOMPorts() {
            using (var serial = new Serial()) {
                return serial.AvailableCOMPorts;
            }
        }
    }
}