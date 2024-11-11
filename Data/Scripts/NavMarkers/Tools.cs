using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRage.ModAPI;
using System.Globalization;
using System.Text.RegularExpressions;
using System;
using VRageMath;
using VRage;
using System.Collections.Generic;
using System.Reflection;

namespace NavMarkers
{

    public class Tools
    {
        public static bool LoggingEnabled = false;

        public static void Log(string message)
        {
            if (LoggingEnabled)
            {
                MyLog.Default.WriteLineAndConsole(message);
            }
        }

        public static bool IsDedicatedServer =>
            MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Utilities.IsDedicated;

        public static bool IsClient =>
            MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer;

        public static bool IsValid(IMyEntity obj)
        {
            return obj != null && !obj.MarkedForClose && !obj.Closed;
        }

        public static void Log(MyLogSeverity level, string message)
        {
            MyLog.Default.Log(level, $"[RaceCourse] {message}");
            MyLog.Default.Flush();
        }

        public static bool IsAllowedSpecialOperations(ulong steamId)
        {
            return IsAllowedSpecialOperations(MyAPIGateway.Session.GetUserPromoteLevel(steamId));
        }

        public static bool IsAllowedSpecialOperations(MyPromoteLevel level)
        {
            return level == MyPromoteLevel.SpaceMaster || level == MyPromoteLevel.Admin || level == MyPromoteLevel.Owner;
        }

        public static bool TryParseGPSRange(string input, out double range)
        {
            double radius = 0;
            string scanPattern = ".*\\(R-(\\d+)\\)";
            Match match = Regex.Match(input, scanPattern);
            if (match.Success)
            {
                radius = match.Success ? double.Parse(match.Groups[1].Value) * 1000.0 : 1000.0;
            }
            else
            {
                scanPattern = ".*\\(R:(\\d+km)\\)";
                match = Regex.Match(input, scanPattern);
                if (match.Success)
                {
                    string r = match.Groups[1].Value;
                    if (r.Contains("km"))
                    {
                        r = r.Replace("km", "");
                    }
                    MyLog.Default.WriteLineAndConsole($"Trying to parse a range from GPS: {input}. {r}");
                    radius = match.Success ? double.Parse(r) * 1000.0 : 1000.0;
                }
            }
            range = radius;
            return radius > 0;
        }

        public static bool TryParseGPS(string input, out Vector3 position, out string name)
        {
            position = default(Vector3);
            name = default(string);

            string m_ScanPattern = "GPS:([^:]{0,32}):([\\d\\.-]*):([\\d\\.-]*):([\\d\\.-]*):";
            foreach (object obj in Regex.Matches(input, m_ScanPattern))
            {
                Match item = (Match)obj;
                name = item.Groups[1].Value;
                double value2;
                double value3;
                double value4;
                try
                {
                    value2 = double.Parse(item.Groups[2].Value, CultureInfo.InvariantCulture);
                    value2 = Math.Round(value2, 2);
                    value3 = double.Parse(item.Groups[3].Value, CultureInfo.InvariantCulture);
                    value3 = Math.Round(value3, 2);
                    value4 = double.Parse(item.Groups[4].Value, CultureInfo.InvariantCulture);
                    value4 = Math.Round(value4, 2);
                }
                catch (Exception)
                {
                    return false;
                }
                position = new Vector3(value2, value3, value4);
            }
            return true;
        }
    }
}
