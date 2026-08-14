namespace Attractor.Benchmark.Utils;

public record PingPongMessage(IRef Ping, IRef Pong, int Limit) : PingMessage(Ping, Limit);
