namespace Attractor.Benchmark.Utils;

public record PingMessage(IRef Ping, int Limit) : LimitMessage(Limit);
