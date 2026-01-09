using System.Text.RegularExpressions;

namespace TPLink.SwitchClient;

public class SwitchManager
{
    private readonly WebClient _webClient;
    private static readonly string[] SpeedLabels = { "Link Down", "Auto", "10MH", "10MF", "100MH", "100MF", "1000MF", "" };
    private static readonly string[] FlowControlLabels = { "Off", "On" };
    private static readonly string[] StateLabels = { "Disabled", "Enabled" };
    private static readonly string[] LinkStatusLabels = { "Link Down", "Auto", "10M Half", "10M Full", "100M Half", "100M Full", "1000M Full", "" };

    public SwitchManager(WebClient webClient)
    {
        _webClient = webClient;
    }

    public async Task<bool> Login() => await _webClient.Login();

    public async Task<List<PortInfo>> GetPortStatus()
    {
        try
        {
            var html = await _webClient.Get("/PortSettingRpm.htm");
            if (string.IsNullOrEmpty(html))
                return new List<PortInfo>();

            return ParsePortSettings(html);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting port status: {ex.Message}");
            return new List<PortInfo>();
        }
    }

    public async Task<List<PortStatistics>> GetPortStatistics()
    {
        try
        {
            var html = await _webClient.Get("/PortStatisticsRpm.htm");
            if (string.IsNullOrEmpty(html))
                return new List<PortStatistics>();

            return ParsePortStatistics(html);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting port statistics: {ex.Message}");
            return new List<PortStatistics>();
        }
    }

    public async Task<List<VlanInfo>> GetVlans()
    {
        try
        {
            var html = await _webClient.Get("/Vlan8021QRpm.htm");
            if (string.IsNullOrEmpty(html))
                return new List<VlanInfo>();

            return ParseVlans(html);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting VLANs: {ex.Message}");
            return new List<VlanInfo>();
        }
    }

    public async Task<bool> SetPortState(int portNumber, bool enable)
    {
        try
        {
            var state = enable ? "1" : "0";
            var requestBody = $"portid={portNumber}&state={state}&speed=7&flowcontrol=7&apply=Apply";
            var response = await _webClient.Post("/port_setting.cgi", requestBody);
            return !string.IsNullOrEmpty(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting port state: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> CreateOrModifyVlan(int vlanId, string name, Dictionary<int, VlanMembershipType> portMemberships, int maxPorts = 24)
    {
        try
        {
            var queryParams = $"vid={vlanId}&vname={Uri.EscapeDataString(name)}";

            for (int i = 1; i <= maxPorts; i++)
            {
                var membershipType = portMemberships.ContainsKey(i) ? portMemberships[i] : VlanMembershipType.NotMember;
                queryParams += $"&selType_{i}={((int)membershipType)}";
            }

            queryParams += "&qvlan_add=Add%2FModify";

            var response = await _webClient.Get($"/qvlanSet.cgi?{queryParams}");
            return !string.IsNullOrEmpty(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating/modifying VLAN: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteVlan(int vlanId)
    {
        try
        {
            var response = await _webClient.Get($"/qvlanSet.cgi?selVlans={vlanId}&qvlan_del=Delete");
            return !string.IsNullOrEmpty(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting VLAN: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> SetPortPvid(int portNumber, int vlanId)
    {
        try
        {
            var pbm = 1 << (portNumber - 1);
            var response = await _webClient.Get($"/vlanPvidSet.cgi?pbm={pbm}&pvid={vlanId}");
            return !string.IsNullOrEmpty(response);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error setting port PVID: {ex.Message}");
            return false;
        }
    }

    public async Task<List<CableTestResult>> RunCableTest(List<int> ports)
    {
        try
        {
            if (ports.Count == 0)
                return new List<CableTestResult>();

            var queryParams = string.Join("&", ports.Select(p => $"chk_{p}={p}"));
            queryParams += "&Apply=Apply";

            var html = await _webClient.Get($"/cable_diag_get.cgi?{queryParams}");

            if (string.IsNullOrEmpty(html))
                return new List<CableTestResult>();

            return ParseCableTestResults(html, ports);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error running cable test: {ex.Message}");
            return new List<CableTestResult>();
        }
    }

    private List<CableTestResult> ParseCableTestResults(string html, List<int> testedPorts)
    {
        var results = new List<CableTestResult>();

        try
        {
            // Extract cablestate array
            var statePattern = @"var cablestate\s*=\s*\[([^\]]+)\]";
            var stateMatch = Regex.Match(html, statePattern);

            if (!stateMatch.Success)
            {
                Console.WriteLine("DEBUG: Could not find cablestate array in response");
                return results;
            }

            var stateValues = stateMatch.Groups[1].Value.Split(',')
                .Select(s => ParseInt(s.Trim()))
                .ToList();

            // Extract cablelength array
            var lengthPattern = @"var cablelength\s*=\s*\[([^\]]+)\]";
            var lengthMatch = Regex.Match(html, lengthPattern);

            if (!lengthMatch.Success)
            {
                Console.WriteLine("DEBUG: Could not find cablelength array in response");
                return results;
            }

            var lengthValues = lengthMatch.Groups[1].Value.Split(',')
                .Select(s => ParseInt(s.Trim()))
                .ToList();

            // Process results for tested ports only
            foreach (var portNumber in testedPorts)
            {
                var index = portNumber - 1;

                if (index >= 0 && index < stateValues.Count && index < lengthValues.Count)
                {
                    var statusCode = stateValues[index];
                    var cableLength = lengthValues[index];

                    // Only add if test was actually performed (not -1)
                    if (statusCode != -1)
                    {
                        var result = new CableTestResult
                        {
                            PortNumber = portNumber,
                            TestCompleted = true,
                            Status = GetCableStatus(statusCode),
                            CableLength = cableLength >= 0 ? cableLength : 0
                        };

                        // Initialize pairs array with same status
                        for (int i = 0; i < 4; i++)
                        {
                            result.Pairs[i] = new CablePairResult
                            {
                                PairNumber = i + 1,
                                Status = result.Status,
                                Length = result.CableLength
                            };
                        }

                        results.Add(result);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing cable test results: {ex.Message}");
        }

        return results;
    }

    private string GetCableStatus(int code)
    {
        return code switch
        {
            -1 => "--",
            0 => "No Cable",
            1 => "Normal",
            2 => "Open",
            3 => "Short",
            4 => "Open & Short",
            5 => "Cross Cable",
            _ => "Others"
        };
    }

    private List<PortInfo> ParsePortSettings(string html)
    {
        var ports = new List<PortInfo>();

        var maxPortMatch = Regex.Match(html, @"var max_port_num\s*=\s*(\d+)");
        if (!maxPortMatch.Success) return ports;

        var maxPorts = int.Parse(maxPortMatch.Groups[1].Value);

        var stateArray = ExtractJsArray(html, "state");
        var trunkInfoArray = ExtractJsArray(html, "trunk_info");
        var spdCfgArray = ExtractJsArray(html, "spd_cfg");
        var spdActArray = ExtractJsArray(html, "spd_act");
        var fcCfgArray = ExtractJsArray(html, "fc_cfg");
        var fcActArray = ExtractJsArray(html, "fc_act");

        for (int i = 0; i < maxPorts && i < stateArray.Count; i++)
        {
            ports.Add(new PortInfo
            {
                PortNumber = i + 1,
                Enabled = stateArray[i] == 1,
                ConfiguredSpeed = GetLabel(SpeedLabels, spdCfgArray, i),
                ActualSpeed = GetLabel(SpeedLabels, spdActArray, i),
                ConfiguredFlowControl = GetLabel(FlowControlLabels, fcCfgArray, i),
                ActualFlowControl = GetLabel(FlowControlLabels, fcActArray, i),
                LagGroup = trunkInfoArray.Count > i ? trunkInfoArray[i] : 0
            });
        }

        return ports;
    }

    private List<PortStatistics> ParsePortStatistics(string html)
    {
        var stats = new List<PortStatistics>();

        var maxPortMatch = Regex.Match(html, @"var max_port_num\s*=\s*(\d+)");
        if (!maxPortMatch.Success) return stats;

        var maxPorts = int.Parse(maxPortMatch.Groups[1].Value);

        var stateArray = ExtractJsArray(html, "state");
        var linkStatusArray = ExtractJsArray(html, "link_status");
        var pktsArray = ExtractJsArray(html, "pkts");

        for (int i = 0; i < maxPorts && i < stateArray.Count; i++)
        {
            var pktIndex = i * 4;
            stats.Add(new PortStatistics
            {
                PortNumber = i + 1,
                Enabled = stateArray[i] == 1,
                LinkStatus = GetLabel(LinkStatusLabels, linkStatusArray, i),
                TxGoodPackets = pktIndex < pktsArray.Count ? pktsArray[pktIndex] : 0,
                TxBadPackets = pktIndex + 1 < pktsArray.Count ? pktsArray[pktIndex + 1] : 0,
                RxGoodPackets = pktIndex + 2 < pktsArray.Count ? pktsArray[pktIndex + 2] : 0,
                RxBadPackets = pktIndex + 3 < pktsArray.Count ? pktsArray[pktIndex + 3] : 0
            });
        }

        return stats;
    }

    private List<VlanInfo> ParseVlans(string html)
    {
        var vlans = new List<VlanInfo>();

        var vidsArray = ExtractJsArray(html, "vids");
        var namesArray = ExtractJsStringArray(html, "names");
        var tagMbrsArray = ExtractJsArray(html, "tagMbrs");
        var untagMbrsArray = ExtractJsArray(html, "untagMbrs");

        for (int i = 0; i < vidsArray.Count; i++)
        {
            var taggedPorts = BitmaskToPorts(i < tagMbrsArray.Count ? tagMbrsArray[i] : 0);
            var untaggedPorts = BitmaskToPorts(i < untagMbrsArray.Count ? untagMbrsArray[i] : 0);
            var memberPorts = taggedPorts.Union(untaggedPorts).OrderBy(p => p).ToList();

            vlans.Add(new VlanInfo
            {
                VlanId = vidsArray[i],
                Name = i < namesArray.Count ? namesArray[i] : "",
                TaggedPorts = taggedPorts,
                UntaggedPorts = untaggedPorts,
                MemberPorts = memberPorts
            });
        }

        return vlans;
    }

    private List<int> ExtractJsArray(string html, string arrayName)
    {
        var pattern = $@"{arrayName}:\s*\[\s*([\d,\s]*)\s*\]";
        var match = Regex.Match(html, pattern);

        if (!match.Success)
            return new List<int>();

        var values = match.Groups[1].Value;
        return values.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => ParseInt(s))
            .ToList();
    }

    private List<string> ExtractJsStringArray(string html, string arrayName)
    {
        var pattern = $@"{arrayName}:\s*\[\s*([^\]]*)\s*\]";
        var match = Regex.Match(html, pattern);

        if (!match.Success)
            return new List<string>();

        var values = match.Groups[1].Value;
        return Regex.Matches(values, @"'([^']*)'")
            .Cast<Match>()
            .Select(m => m.Groups[1].Value)
            .ToList();
    }

    private int ParseInt(string value)
    {
        if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return Convert.ToInt32(value, 16);

        return int.TryParse(value, out var result) ? result : 0;
    }

    private List<int> BitmaskToPorts(int bitmask)
    {
        var ports = new List<int>();
        for (int i = 0; i < 32; i++)
        {
            if ((bitmask & (1 << i)) != 0)
                ports.Add(i + 1);
        }
        return ports;
    }

    private string GetLabel(string[] labels, List<int> values, int index)
    {
        if (index >= values.Count) return "Unknown";
        var value = values[index];
        return value >= 0 && value < labels.Length ? labels[value] : "Unknown";
    }
}

public enum VlanMembershipType
{
    Untagged = 0,
    Tagged = 1,
    NotMember = 2
}
