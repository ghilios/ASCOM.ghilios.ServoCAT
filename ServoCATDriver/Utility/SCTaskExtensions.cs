#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Nito.AsyncEx;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ASCOM.ghilios.ServoCAT.Utility {

    public static class SCTaskExtensions {

        private class NoOpDisposable : IDisposable {

            public void Dispose() {
            }
        }

        public static CancellationToken TimeoutCancellationToken(TimeSpan timeout) {
            var cts = new CancellationTokenSource(timeout);
            return cts.Token;
        }

        public static AwaitableDisposable<IDisposable> MaybeLockAsync(this AsyncLock asyncLock, CancellationToken ct, bool takeLock) {
            if (takeLock) {
                return asyncLock.LockAsync(ct);
            }
            return new AwaitableDisposable<IDisposable>(Task.FromResult<IDisposable>(new NoOpDisposable()));
        }
    }
}