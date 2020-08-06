using System;
using System.Collections.Generic;
using System.Text;
using Google.Protobuf;

namespace Microsoft.Telepathy.ProtoBuf
{
    public partial class EchoRequest
    {
        public EchoRequest(string msg, long length, int delay)
        {
            this.Message = msg;
            this.Dummydata = ByteString.CopyFrom(new byte[length]);
            this.DelayTime = delay;
        }
    }
}
