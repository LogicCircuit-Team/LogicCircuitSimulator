using CircuitApi.Contracts;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace CircuitApi.Services
{
    public class HdlSynthesisService : IHdlSynthesisService
    {
        private static readonly Regex ModuleNamePattern = new(
            @"^[A-Za-z_][A-Za-z0-9_$]*$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        private readonly string environmentScript;
        private readonly int timeoutSeconds;
        private readonly int maxSourceCharacters;
        private readonly ILogger<HdlSynthesisService> logger;

        public HdlSynthesisService(IConfiguration configuration, ILogger<HdlSynthesisService> logger)
        {
            environmentScript = Environment.ExpandEnvironmentVariables(
                configuration["HdlSynthesis:EnvironmentScript"] ?? string.Empty);
            timeoutSeconds = Math.Clamp(configuration.GetValue("HdlSynthesis:TimeoutSeconds", 30), 1, 300);
            maxSourceCharacters = Math.Clamp(configuration.GetValue("HdlSynthesis:MaxSourceCharacters", 100000), 1, 1000000);
            this.logger = logger;
        }

        public async Task<HdlSynthesisResponse> SynthesizeAsync(
            HdlSynthesisRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Source))
                return Failure("The source code is required.");
            if (request.Source.Length > maxSourceCharacters)
                return Failure($"HDL source cannot exceed {maxSourceCharacters} characters.");
            if (string.IsNullOrWhiteSpace(request.TopModule) || !ModuleNamePattern.IsMatch(request.TopModule))
                return Failure("The top module name is invalid.");
            if (string.IsNullOrWhiteSpace(environmentScript) || !File.Exists(environmentScript))
                return Failure("Set HdlSynthesis:EnvironmentScript in CircuitApi/appsettings.json to the OSS CAD Suite environment.ps1 file on the API computer.");

            string workingFolder = Path.Combine(Path.GetTempPath(), "LogicCircuitSimulator", Guid.NewGuid().ToString("N"));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            try
            {
                Directory.CreateDirectory(workingFolder);
                await File.WriteAllTextAsync(Path.Combine(workingFolder, "input.sv"), request.Source,
                    new UTF8Encoding(false), linked.Token);
                string yosysScript = $"""
                    read_slang input.sv
                    hierarchy -top {request.TopModule}
                    proc
                    flatten
                    opt
                    techmap
                    opt
                    write_json netlist.json
                    """;
                await File.WriteAllTextAsync(Path.Combine(workingFolder, "synthesis.ys"), yosysScript, linked.Token);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    WorkingDirectory = workingFolder,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-NonInteractive");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-Command");
                string quotedPath = environmentScript.Replace("'", "''");
                startInfo.ArgumentList.Add($"$ErrorActionPreference = 'Stop'; . '{quotedPath}'; yosys -Q -s synthesis.ys; exit $LASTEXITCODE");

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                try
                {
                    await process.WaitForExitAsync(linked.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                    try
                    {
                        await Task.WhenAll(outputTask, errorTask).WaitAsync(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex) when (ex is TimeoutException or IOException or ObjectDisposedException)
                    {
                        logger.LogDebug(ex, "Output capture stopped after synthesis cancellation.");
                    }
                    throw;
                }

                string output = await outputTask;
                string error = await errorTask;
                if (process.ExitCode != 0)
                    return Failure(string.IsNullOrWhiteSpace(error) ? output : error + Environment.NewLine + output);
                string netlistPath = Path.Combine(workingFolder, "netlist.json");
                if (!File.Exists(netlistPath))
                    return Failure("Yosys did not create the netlist file.");
                string netlistJson = await File.ReadAllTextAsync(netlistPath, linked.Token);
                if (string.IsNullOrWhiteSpace(netlistJson))
                    return Failure("Yosys returned an empty netlist.");
                return new HdlSynthesisResponse { NetlistJson = netlistJson, Diagnostics = string.Empty };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return Failure($"HDL synthesis exceeded its {timeoutSeconds}-second time limit.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HDL synthesis failed.");
                return Failure(ex.Message);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(workingFolder))
                        Directory.Delete(workingFolder, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Could not remove the synthesis working folder.");
                }
            }
        }

        private static HdlSynthesisResponse Failure(string message)
        {
            return new HdlSynthesisResponse { Diagnostics = message };
        }
    }
}
