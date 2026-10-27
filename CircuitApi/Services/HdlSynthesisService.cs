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
        private readonly string yosysExecutable;
        private readonly int timeoutSeconds;
        private readonly int maxSourceCharacters;
        private readonly ILogger<HdlSynthesisService> logger;
        private static readonly SemaphoreSlim synthesisLock = new SemaphoreSlim(2, 2);

        public HdlSynthesisService(IConfiguration configuration, ILogger<HdlSynthesisService> logger, IWebHostEnvironment hostEnvironment)
        {
            environmentScript = Environment.ExpandEnvironmentVariables(
                configuration["HdlSynthesis:EnvironmentScript"] ?? string.Empty);
            if (!string.IsNullOrWhiteSpace(environmentScript) && !Path.IsPathRooted(environmentScript))
                environmentScript = Path.GetFullPath(environmentScript, hostEnvironment.ContentRootPath);
            if (OperatingSystem.IsWindows() && string.IsNullOrWhiteSpace(environmentScript))
            {
                string localScript = Path.GetFullPath("../tools/oss-cad-suite/environment.ps1", hostEnvironment.ContentRootPath);
                if (File.Exists(localScript))
                    environmentScript = localScript;
            }
            yosysExecutable = Environment.ExpandEnvironmentVariables(
                configuration["HdlSynthesis:YosysExecutable"] ?? "yosys");
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
            if (!string.IsNullOrWhiteSpace(environmentScript) && !File.Exists(environmentScript))
                return Failure("HdlSynthesis:EnvironmentScript does not point to an existing file on the API computer.");

            await synthesisLock.WaitAsync(cancellationToken);
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
                    FileName = OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(environmentScript)
                        ? "powershell.exe" : yosysExecutable,
                    WorkingDirectory = workingFolder,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                if (OperatingSystem.IsWindows() && !string.IsNullOrWhiteSpace(environmentScript))
                {
                    startInfo.ArgumentList.Add("-NoProfile");
                    startInfo.ArgumentList.Add("-NonInteractive");
                    startInfo.ArgumentList.Add("-ExecutionPolicy");
                    startInfo.ArgumentList.Add("Bypass");
                    startInfo.ArgumentList.Add("-Command");
                    string quotedPath = environmentScript.Replace("'", "''");
                    string quotedExecutable = yosysExecutable.Replace("'", "''");
                    startInfo.ArgumentList.Add($"$ErrorActionPreference = 'Stop'; . '{quotedPath}'; & '{quotedExecutable}' -Q -s synthesis.ys; exit $LASTEXITCODE");
                }
                else
                {
                    startInfo.ArgumentList.Add("-Q");
                    startInfo.ArgumentList.Add("-s");
                    startInfo.ArgumentList.Add("synthesis.ys");
                }

                using var process = new Process { StartInfo = startInfo };
                process.Start();
                Task<string> outputTask = ReadDiagnosticsAsync(process.StandardOutput);
                Task<string> errorTask = ReadDiagnosticsAsync(process.StandardError);
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
                if (new FileInfo(netlistPath).Length > 10 * 1024 * 1024)
                    return Failure("The generated circuit exceeds the 10 MB netlist limit.");
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
            catch (System.ComponentModel.Win32Exception ex)
            {
                logger.LogError(ex, "Could not start Yosys.");
                return Failure("Yosys could not start. Set HdlSynthesis:EnvironmentScript to the OSS CAD Suite environment.ps1 file on Windows, or configure HdlSynthesis:YosysExecutable with the installed executable path.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HDL synthesis failed.");
                return Failure(ex.Message);
            }
            finally
            {
                synthesisLock.Release();
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

        private static async Task<string> ReadDiagnosticsAsync(StreamReader reader)
        {
            var text = new StringBuilder();
            char[] buffer = new char[4096];
            int read;
            while ((read = await reader.ReadAsync(buffer.AsMemory())) > 0)
            {
                int available = 65536 - text.Length;
                if (available > 0)
                    text.Append(buffer, 0, Math.Min(read, available));
            }
            return text.ToString();
        }
    }
}
