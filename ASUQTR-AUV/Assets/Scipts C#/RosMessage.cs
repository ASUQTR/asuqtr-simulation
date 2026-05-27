using System;

/// <summary>
/// Minimal serializable models for rosbridge payloads used by the simulator.
///
/// ThrusterReceiver uses these classes to parse the rosbridge envelope while
/// ignoring extra fields that rosbridge may include.
/// </summary>
[Serializable]
public class ThrusterCommandMsg
{
    public double[] efforts;
}

[Serializable]
public class ThrusterCommandEnvelope
{
    public string op;
    public string topic;
    public ThrusterCommandMsg msg;
}
