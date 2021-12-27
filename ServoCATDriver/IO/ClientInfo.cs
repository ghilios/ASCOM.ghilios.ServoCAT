#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;

namespace ASCOM.Joko.ServoCAT.IO {

    public partial class DriverConnectionManager {

        private class ClientInfo {

            public ClientInfo(Guid guid) {
                Guid = guid;
            }

            public Guid Guid { get; private set; }
            public bool Connected;

            public override string ToString() {
                return $"{Guid}: Connected={Connected}";
            }

            public override bool Equals(object obj) {
                if (!(obj is ClientInfo)) {
                    return false;
                }
                var clientInfoObj = (ClientInfo)obj;
                return clientInfoObj.Guid == Guid;
            }

            public override int GetHashCode() {
                return -737073652 + Guid.GetHashCode();
            }
        }
    }
}