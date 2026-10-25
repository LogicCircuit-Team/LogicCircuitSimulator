using CircuitApi.Contracts;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace CircuitApi.Services
{
    public class HdlSynthesisService : IHdlSynthesisService
    { 
        private const string EnvironmentScript=
                 @"C:\Users\zim\tools\oss-cad-suite-windows-x64-20261020\oss-cad-suite\environment.ps1";


        public async Task<HdlSynthesisResponse> SynthesizeAsync(
           HdlSynthesisRequest request,
           CancellationToken cancellationToken)
        {
            // Checks that the request exists.
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Source))
            {
                return new HdlSynthesisResponse
                {
                    Diagnostics = "The source code is required."
                };
            }

            // Allows simple module names and prevents invalid Yosys commands.
            if (!Regex.IsMatch(request.TopModule, @"^[A-Za-z_][A-Za-z0-9_$]*$"))
            {
                return new HdlSynthesisResponse
                {
                    Diagnostics = "The top module name is invalid."
                };
            }

            if (!File.Exists(EnvironmentScript)) 
            {
                return new HdlSynthesisResponse
                {
                    Diagnostics = $"Yosys setup script was not found: {EnvironmentScript}"
                };
            }

            var workingFolder = Path.Combine(Path.GetTempPath(),Guid.NewGuid().ToString("N"));

            try
            {
                Directory.CreateDirectory(workingFolder);
                // Saves the submitted SystemVerilog source
                var sourcePath = Path.Combine(workingFolder, "input.sv");


                await File.WriteAllTextAsync(
                sourcePath,
                request.Source,
                new UTF8Encoding(false),
                cancellationToken);

                var yosysScript = $"""
                    read_slang input.sv
                    hierarchy -top {request.TopModule}
                    proc
                    flatten
                    opt
                    techmap
                    opt
                    write_json netlist.json
                    """;
                var yosysScriptPath = Path.Combine(workingFolder, "synthesis.ys");

                await File.WriteAllTextAsync(
                   yosysScriptPath,
                   yosysScript,
                   cancellationToken);

                var startInfo = new ProcessStartInfo
                {

                    FileName = "powershell.exe",
                    WorkingDirectory = workingFolder,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };


                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-NonInteractive");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-Command");

                // Loads the environment and runs the Yosys script.
                var scriptPath = EnvironmentScript.Replace("'", "''");
                startInfo.ArgumentList.Add($". '{scriptPath}'; yosys -Q -s synthesis.ys");



                // Starts the Yosys process.
                using var process = new Process
                {
                    StartInfo = startInfo

                };
                process.Start();



                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                // Waits until Yosys finishes or the request is cancelled.
                try
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Stops Yosys if the request was cancelled.
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }

                    throw;
                }

                // Gets the messages after Yosys finishes.
                var output = await outputTask;
                var error = await errorTask;


                // Returns Yosys messages if synthesis failed.
                if (process.ExitCode != 0)
                {
                    return new HdlSynthesisResponse
                    {
                        Diagnostics = string.IsNullOrWhiteSpace(error)
                      ? output
                      : error + Environment.NewLine + output
                    };
                }
                // Checks that Yosys created the JSON file.
                var netlistPath = Path.Combine(workingFolder, "netlist.json");
                if (!File.Exists(netlistPath))
                {
                    return new HdlSynthesisResponse
                    {
                        Diagnostics = "Yosys did not create the netlist file."
                    };
                }

                var netlistJson = await File.ReadAllTextAsync(netlistPath, cancellationToken);


                return new HdlSynthesisResponse
                {
                    NetlistJson = netlistJson,
                    Diagnostics = string.Empty
                };
            }




            catch (OperationCanceledException)
            {
                // Keeps cancellation behaviour for ASP.NET Core.
                throw;
            }
            catch (Exception ex)
            {
                // Returns the error message to the API caller.
                return new HdlSynthesisResponse
                {
                    Diagnostics = ex.Message
                };
            }
            finally
            {
                // Removes temporary files after synthesis.
                if (Directory.Exists(workingFolder))
                {
                    Directory.Delete(workingFolder, recursive: true);
                }


            }

        }
    
    
    
    
    }


   

}
