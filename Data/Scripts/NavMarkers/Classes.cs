using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Serialization;
using VRageMath;

namespace NavMarkers
{
    [ProtoContract]
    public class NavMarkerDict
    {
        [ProtoMember(1)]
        public SerializableDictionary<string, NavMarker> markers { get; set; }
    }

    [ProtoContract]
    public class NavMarker
    {
        [ProtoMember(1)]
        public string Name { get; set; }

        [ProtoMember(2)]
        public Vector3D Position { get; set; }

        [ProtoMember(3)]
        public float Radius { get; set; }

        [ProtoMember(4)]
        public Color Color { get; set; }
    }
}
