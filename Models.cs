namespace TPLink.SwitchClient;

public class PortInfo
{
    public int PortNumber { get; set; }
    public bool Enabled { get; set; }
    public string ConfiguredSpeed { get; set; } = "Auto";
    public string ActualSpeed { get; set; } = "Link Down";
    public string ConfiguredFlowControl { get; set; } = "Off";
    public string ActualFlowControl { get; set; } = "Off";
    public int LagGroup { get; set; }
}

public class PortStatistics
{
    public int PortNumber { get; set; }
    public bool Enabled { get; set; }
    public string LinkStatus { get; set; } = "Link Down";
    public long TxGoodPackets { get; set; }
    public long TxBadPackets { get; set; }
    public long RxGoodPackets { get; set; }
    public long RxBadPackets { get; set; }
}

public class VlanInfo
{
    public int VlanId { get; set; }
    public string Name { get; set; } = "";
    public List<int> TaggedPorts { get; set; } = new();
    public List<int> UntaggedPorts { get; set; } = new();
    public List<int> MemberPorts { get; set; } = new();
}

public class CableTestResult
{
    public int PortNumber { get; set; }
    public string Status { get; set; } = "Not Tested";
    public CablePairResult[] Pairs { get; set; } = new CablePairResult[4];
    public int CableLength { get; set; }
    public bool TestCompleted { get; set; }
}

public class CablePairResult
{
    public int PairNumber { get; set; }
    public string Status { get; set; } = "Unknown";
    public int Length { get; set; }
}
