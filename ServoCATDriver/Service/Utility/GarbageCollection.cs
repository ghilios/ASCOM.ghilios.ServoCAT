#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Threading;

namespace ASCOM.Joko.ServoCAT.Service.Utility {

    /// <summary>
    /// Summary description for GarbageCollection.
    /// </summary>
    internal class GarbageCollection {
        private readonly TimeSpan interval;

        public GarbageCollection(TimeSpan interval) {
            this.interval = interval;
        }

        public void GCWatch(CancellationToken token) {
            if (token == null) {
                throw new ArgumentException("GCWatch was called with a null cancellation token!");
            }

            bool taskCancelled = false;
            while (!taskCancelled) {
                GC.Collect();
                taskCancelled = token.WaitHandle.WaitOne(interval);
            }
            GC.Collect();
        }
    }
}