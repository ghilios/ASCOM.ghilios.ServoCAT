#region "copyright"

/*
    Copyright © 2021 - 2021 George Hilios <ghilios+SERVOCAT@googlemail.com>

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using System;
using System.Text;

namespace ASCOM.Joko.ServoCAT.Exceptions {

    public class UnexpectedResponseException : Exception {

        public UnexpectedResponseException(string message) : base(message) {
        }

        private static string ResponseEpilogue(byte[] response) {
            return $"{Environment.NewLine}{BitConverter.ToString(response)}{Environment.NewLine}{Encoding.ASCII.GetString(response)}";
        }

        public static UnexpectedResponseException ExpectedByteInResponse(byte[] response, string expected, int offset) {
            var msg = $"Expected {expected} at byte {offset} within response from server.{ResponseEpilogue(response)}";
            return new UnexpectedResponseException(msg);
        }

        public static UnexpectedResponseException ExpectedIntInResponse(byte[] response, int offset, int length) {
            var msg = $"Expected integer at start from {offset}, {length} bytes long within response from server.{ResponseEpilogue(response)}";
            return new UnexpectedResponseException(msg);
        }

        public static UnexpectedResponseException XORValidationFailed(byte[] response, int offset, int length, byte actualXor, byte expectedXor) {
            var msg = $"Failed XOR validation from {offset}, {length} bytes. ExpectedXOR={expectedXor}, ActualXOR={actualXor}.{ResponseEpilogue(response)}";
            return new UnexpectedResponseException(msg);
        }
    }
}