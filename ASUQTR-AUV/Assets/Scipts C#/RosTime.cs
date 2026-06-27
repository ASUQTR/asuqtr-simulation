using System;

/// <summary>
/// Provides ROS-compatible wall-clock timestamps for rosbridge messages.
/// Uses Unix time so timestamps stay monotonic across Unity Editor Stop/Play cycles.
/// </summary>
public static class RosTime
{
    public static void Now(out uint sec, out uint nanosec)
    {
        DateTimeOffset utcNow = DateTimeOffset.UtcNow;
        sec = (uint)utcNow.ToUnixTimeSeconds();
        nanosec = (uint)((utcNow.Ticks % TimeSpan.TicksPerSecond) * 100);
    }
}
