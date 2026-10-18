using System.Diagnostics;

namespace EmlExtraction.Bench.Sweep;

/// <summary>One DOP measurement: n worker threads pull the next message from a shared index (ADR-015).</summary>
internal static class SweepRunner
{
    private const int Warmup = 0;
    private const int Measure = 1;
    private const int Stop = 2;

    // Latencies per worker: fixed buffer allocated before the window, reservoir-sampled once full,
    // so recording never allocates inside the window (keeps A and P(n) clean on microsecond-scale ops).
    private const int ReservoirSize = 1 << 17;

    /// <summary>Runs one point. Efficiency is left at 0; the caller fills it once T(1) is known.</summary>
    public static SweepPoint Run(byte[][] inputs, int dop, Action<byte[]> op, SweepParameters p)
    {
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();

        long next = -1;
        long completed = 0;
        var phase = Warmup;
        var latencies = new long[dop][];
        var recorded = new long[dop];
        var bytes = new long[dop];
        var threads = new Thread[dop];
        for (var w = 0; w < dop; w++)
        {
            var id = w;
            latencies[id] = new long[ReservoirSize];
            var rng = new Random(id);
            threads[id] = new Thread(() =>
            {
                var lat = latencies[id];
                long processed = 0;
                long seen = 0;
                while (Volatile.Read(ref phase) != Stop)
                {
                    var input = inputs[Interlocked.Increment(ref next) % inputs.Length];
                    var started = Stopwatch.GetTimestamp();
                    var phaseAtStart = Volatile.Read(ref phase);
                    op(input);
                    var ended = Stopwatch.GetTimestamp();
                    if (phaseAtStart == Measure && Volatile.Read(ref phase) == Measure)
                    {
                        seen++;
                        var slot = seen <= lat.Length ? seen - 1 : rng.NextInt64(seen);
                        if (slot < lat.Length)
                            lat[slot] = ended - started;
                        processed += input.Length;
                        Interlocked.Increment(ref completed);
                    }
                }

                bytes[id] = processed;
                recorded[id] = Math.Min(seen, lat.Length);
            }) { IsBackground = true, Name = $"sweep-{id}" };
        }

        foreach (var t in threads)
            t.Start();
        Thread.Sleep(p.WarmupMs);

        using var self = Process.GetCurrentProcess();
        var target = (long)p.MinPasses * inputs.Length;
        var allocBefore = GC.GetTotalAllocatedBytes(precise: true);
        var sw = Stopwatch.StartNew();
        Volatile.Write(ref phase, Measure);
        long peak = 0;
        while (true)
        {
            self.Refresh();
            peak = Math.Max(peak, self.WorkingSet64);
            if (sw.ElapsedMilliseconds >= p.WindowMs && Interlocked.Read(ref completed) >= target)
                break;
            Thread.Sleep(p.SampleIntervalMs);
        }

        Volatile.Write(ref phase, Stop);
        var elapsed = sw.Elapsed.TotalSeconds;
        var count = Interlocked.Read(ref completed);
        var allocAfter = GC.GetTotalAllocatedBytes(precise: true);
        foreach (var t in threads)
            t.Join();

        var ms = latencies.SelectMany((l, w) => l.Take((int)recorded[w]))
            .Select(t => t * 1000.0 / Stopwatch.Frequency).Order().ToArray();
        return new SweepPoint(
            dop,
            count / elapsed,
            SweepMath.Percentile(ms, 50),
            SweepMath.Percentile(ms, 95),
            peak,
            SweepMath.AllocPerInputByte(allocAfter - allocBefore, bytes.Sum()),
            0,
            count,
            elapsed,
            (double)count / inputs.Length);
    }
}
