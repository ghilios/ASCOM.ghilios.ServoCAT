#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System.Runtime.InteropServices;

namespace ASCOM.Joko.ServoCAT.Service.Utility {

    [ComVisible(false)]
    public class ReferenceCountedObjectBase {
        private bool constructed = false;

        public ReferenceCountedObjectBase() {
            LocalServerApp.App.IncrementObjectCount();
            constructed = true;
        }

        ~ReferenceCountedObjectBase() {
            if (constructed) {
                LocalServerApp.App.DecrementObjectCount();
                LocalServerApp.App.ExitIf();
            }
        }
    }
}