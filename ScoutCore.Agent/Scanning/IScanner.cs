using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Scanning;

public interface IScanner
{
    void Scan(ScanContext ctx);
}
