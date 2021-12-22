#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+NINA@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Runtime.InteropServices;

namespace ASCOM.Joko.ServoCAT.Service.Utility {

    [ComVisible(false)]
    public class ReferenceCountedObjectBase {

        public ReferenceCountedObjectBase() {
            // We increment the global count of objects.
            Server.IncrementObjectCount();
        }

        ~ReferenceCountedObjectBase() {
            // We decrement the global count of objects.
            Server.DecrementObjectCount();
            // We then immediately test to see if we the conditions
            // are right to attempt to terminate this server application.
            Server.ExitIf();
        }
    }
}