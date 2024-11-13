using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Serialization;
using VRageMath;

namespace NavMarkers
{
    [ProtoContract]
    public class NavMarkerData
    {
        [ProtoMember(1)]
        public bool Enabled { get; set; }

        [ProtoMember(2)]
        public bool ShowOnlyCloseMarkers { get; set; } = false;

        [ProtoMember(3)]
        public bool ShowPartialMarkers { get; set; } = false;

        [ProtoMember(4)]
        public SerializableDictionary<string, NavMarker> Markers { get; set; }
        
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

    public class ChatCommand
    {
        public string command;
        public Action<string[]> callback;
    }
    public class Segment
    {
        public double StartX { get; set; }
        public double StartY { get; set; }
        public double StartZ { get; set; }
        public double EndX { get; set; }
        public double EndY { get; set; }
        public double EndZ { get; set; }
        public Vector3 Start => new Vector3(StartX, StartY, StartZ);
        public Vector3 End => new Vector3(EndX, EndY, EndZ);
        public Segment(double startX, double startY, double startZ, double endX, double endY, double endZ)
        {
            StartX = startX;
            StartY = startY;
            StartZ = startZ;
            EndX = endX;
            EndY = endY;
            EndZ = endZ;
        }
        public Segment(Vector3 start, Vector3 end)
        {
            StartX = start.X;
            StartY = start.Y;
            StartZ = start.Z;
            EndX = end.X;
            EndY = end.Y;
            EndZ = end.Z;
        }
    }
}
