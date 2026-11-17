using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

// Manual, headless graph probe. It never launches Core/UI or selects a user's profile.
// Every invocation is a fresh process, so native runtimes and model lifetimes stay isolated.
if (args.Length != 1) throw new ArgumentException("Expected one JSON request path.");
var json = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var request = JsonSerializer.Deserialize<Request>(File.ReadAllText(args[0]), json)!;
if (request.Cases.Length != 1 || request.Runs < 1)
    throw new ArgumentException("Use one input case per process and at least one measured run.");
if (request.Backend is not ("cpu" or "dml")) throw new ArgumentException("Backend must be cpu or dml.");
Directory.CreateDirectory(request.OutputDirectory);
var report = new Report { Backend = request.Backend };
try {
    if (request.Backend == "dml") NativeLibrary.Load(Path.GetFullPath(request.DirectML));
    nint native = NativeLibrary.Load(Path.GetFullPath(request.Runtime));
    NativeLibrary.SetDllImportResolver(typeof(InferenceSession).Assembly,
        (name, _, _) => name is "onnxruntime" or "onnxruntime.dll" ? native : 0);
    report.NativeVersion = OrtEnv.Instance().GetVersionString();
    report.AvailableProviders = OrtEnv.Instance().GetAvailableProviders();
    if (request.Backend == "dml" && !report.AvailableProviders.Contains("DmlExecutionProvider"))
        throw new InvalidOperationException("The selected native runtime has no DirectML provider.");

    using var options = new SessionOptions {
        EnableCpuMemArena = false,
        EnableMemoryPattern = false,
        IntraOpNumThreads = Math.Max(1, Environment.ProcessorCount / 2),
        ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
        ProfileOutputPathPrefix = Path.Combine(request.OutputDirectory, "placement"),
        EnableProfiling = request.Profile
    };
    if (request.Backend == "dml") options.AppendExecutionProvider_DML(0);
    var timer = Stopwatch.StartNew();
    using var session = new InferenceSession(request.Model, options);
    report.LoadMs = timer.Elapsed.TotalMilliseconds;
    report.Inputs = session.InputMetadata.ToDictionary(p => p.Key,
        p => $"{p.Value.ElementType.Name}[{string.Join(',', p.Value.Dimensions)}]");

    foreach (var image in request.Cases) {
        var feeds = image.Inputs.Select(input => {
            byte[] bytes = File.ReadAllBytes(input.Path);
            int count = input.Shape.Aggregate(1, (a, b) => checked(a * b));
            if (bytes.Length != count * sizeof(float)) throw new InvalidDataException("Input shape/length mismatch.");
            var data = new float[count];
            Buffer.BlockCopy(bytes, 0, data, 0, bytes.Length);
            return NamedOnnxValue.CreateFromTensor(input.Name, new DenseTensor<float>(data, input.Shape));
        }).ToArray();
        using var runOptions = new RunOptions();
        var sample = new Sample { Name = image.Name };
        for (int iteration = -1; iteration < request.Runs; iteration++) {
            timer.Restart();
            using var outputs = session.Run(feeds, session.OutputMetadata.Keys.ToArray(), runOptions);
            double elapsed = timer.Elapsed.TotalMilliseconds;
            if (iteration == -1) sample.FirstRunMs = elapsed;
            else sample.WarmMs.Add(elapsed);
            if (iteration == request.Runs - 1) {
                var output = outputs.First().AsTensor<float>();
                sample.Shape = output.Dimensions.ToArray();
                // Freeze memory measurements before allocating a copy for artifact export.
                report.PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64;
                Console.WriteLine(JsonSerializer.Serialize(new { Stage = "output", image.Name }));
                Console.Out.Flush();
                var data = output.ToArray();
                sample.Output = Path.Combine(request.OutputDirectory, image.Name + ".f32");
                using var outputFile = File.Create(sample.Output);
                outputFile.Write(MemoryMarshal.AsBytes(data.AsSpan()));
            }
        }
        report.Samples.Add(sample);
    }
    if (request.Profile) report.Profile = session.EndProfiling();
    report.Success = true;
}
catch (Exception ex) {
    report.Error = ex.ToString();
    report.PeakWorkingSetBytes = Process.GetCurrentProcess().PeakWorkingSet64;
}
finally {
    File.WriteAllText(Path.Combine(request.OutputDirectory, "result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
}
return report.Success ? 0 : 1;

sealed class Request {
    public string Model { get; set; } = "";
    public string Runtime { get; set; } = "";
    public string DirectML { get; set; } = "";
    public string Backend { get; set; } = "cpu";
    public string OutputDirectory { get; set; } = "";
    public int Runs { get; set; } = 3;
    public bool Profile { get; set; }
    public Case[] Cases { get; set; } = [];
}
sealed class Case {
    public string Name { get; set; } = "";
    public Input[] Inputs { get; set; } = [];
}
sealed class Input {
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public int[] Shape { get; set; } = [];
}
sealed class Report {
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string Backend { get; set; } = "";
    public string NativeVersion { get; set; } = "";
    public string[] AvailableProviders { get; set; } = [];
    public Dictionary<string, string> Inputs { get; set; } = [];
    public double LoadMs { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public string? Profile { get; set; }
    public List<Sample> Samples { get; set; } = [];
}
sealed class Sample {
    public string Name { get; set; } = "";
    public double FirstRunMs { get; set; }
    public List<double> WarmMs { get; set; } = [];
    public int[] Shape { get; set; } = [];
    public string Output { get; set; } = "";
}
