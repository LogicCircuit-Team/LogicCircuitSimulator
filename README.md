# Logic Circuit Simulator

A logic circuit simulator built with **C#, Blazor WebAssembly, and ASP.NET Core**. Draw circuits, connect gates, test inputs, generate truth tables, save and share circuits, and convert them to Verilog and SystemVerilog.

**The version described on this page is available on the [`beta-2`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/tree/beta-2). Use this branch to download and run the project.**

[**Browse Beta 2**](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/tree/beta-2) · [**Download Beta 2 ZIP**](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/archive/refs/heads/beta-2.zip) · [**Arabic Explanation (Word)**](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/docs/LogicCircuitSimulator_Explanation_AR.docx) · [**Azure Guide**](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/docs/AZURE_AR.md)

> This is the overview page on `main`. All code, example, and documentation links below point to `beta-2`. When using **Code → Download ZIP** on GitHub, select `beta-2` first or use the direct download link above.

## Features

| Feature | Description |
| --- | --- |
| Circuit Drawing | Add, move, and wire gates, switches, and lamps. |
| Simulation | Toggle switches and observe gate, wire, and output states. |
| Truth Table | Generate a truth table in a centered dialog and restore switch states afterward. |
| Step mode | Follow signal propagation and gate evaluation step by step. |
| Circuit JSON | Download a circuit file and import it again later. |
| Export HDL | Generate Verilog or SystemVerilog and download a `.v` or `.sv` file. |
| Import HDL | Read HDL from a file or text box and convert it into a circuit using Yosys. |
| Database Storage | Save, load, update, and delete circuits within the browser session. |
| Community Sharing | Publish a public copy, search posts, and open an independent copy for editing. |
| Navigation and View | Zoom, fit the circuit to the screen, pan the workspace, and toggle side panels. |

The interface is based on the **Logic Space Classic Bulb** template and uses locally hosted **Bootstrap 5.3.3** and **Bootstrap Icons 1.11.3**. Analysis dialogs can be expanded, and side panels use Offcanvas on small screens.

## Current Limitations

- Supported gates: `AND`, `OR`, `NOT`, `NAND`, `NOR`, and `XOR`, plus `Switch` and `Lamp`.
- Truth tables support up to **8 switches**, producing at most **256 rows**.
- HDL import is intended for combinational circuits that can be mapped to the cells listed below, with single-bit ports.
- The adapter does not support flip-flops, memory, bus ports, or constant bit connections such as `"0"`, `"1"`, `"x"`, and `"z"`.
- Successful HDL synthesis in Yosys does not mean every resulting cell is supported. The adapter validates the output before replacing the circuit.
- Combinational loops and invalid connections are rejected. Step mode illustrates combinational circuit evaluation; it does not provide clocked sequential simulation.
- Wire routing attempts to avoid gates and reduce overlap. Crowded circuits may require manual gate rearrangement. A visual crossing between two wire paths does not create an electrical connection.

## Requirements

| Requirement | When It Is Needed |
| --- | --- |
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Build and run the project from source. |
| Git or ZIP Download | Obtain the `beta-2` files. |
| Modern Browser | Open the Blazor frontend. |
| OSS CAD Suite with `read_slang` | Required only for Verilog and SystemVerilog import; drawing, JSON, and export work without it. |
| SQL Server LocalDB | Required when choosing SQL Server locally; the default startup script uses SQLite. |
| Docker Desktop with Linux Containers | Run Docker locally or use the Azure deployment script. |
| Azure CLI and an Active Azure Subscription | Deploy to Azure. |

Visual Studio is optional. You can run the project from PowerShell using the .NET SDK.

## Get the Correct Version

Download and extract the ZIP linked above, or run the following command in the directory where you want to save the project:

```powershell
git clone --branch beta-2 --single-branch https://github.com/LogicCircuit-Team/LogicCircuitSimulator.git LogicCircuitSimulator-Beta2
```

After cloning, open the directory:

```powershell
Set-Location -LiteralPath ".\LogicCircuitSimulator-Beta2"
git branch --show-current
```

The output should be `beta-2`. ZIP downloads do not include Git metadata and can be run without executing Git commands.

Throughout this guide, **project root** means the directory containing `LogiclyInspiredSimulator.slnx`, `Dockerfile`, and the `LogicCore`, `BlazorApp1`, and `CircuitApi` directories.

## Run Locally on Windows

Open PowerShell in the project root and run:

```powershell
dotnet dev-certs https --trust
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Start-Local.ps1
```

Accept the development certificate confirmation if prompted. Once `Now listening on` appears, open:

**[https://localhost:7178](https://localhost:7178)**

Keep the terminal open. Press `Ctrl+C` in that terminal to stop the application.

### How Unified Hosting Works

`CircuitApi` serves both the API and the Blazor frontend from the same address. You do not need a separate frontend on port `7007` or a hard-coded API address in the browser.

For unified hosting, leave `ApiBaseUrl` empty in `BlazorApp1/wwwroot/appsettings.json`; the client uses the current page address. If you run the frontend and API separately, configure the API address and CORS for both origins.

### Local Database

`Start-Local.ps1` uses **SQLite** by default, creating `App_Data/circuits.db` and the required tables. Saved data survives application restarts as long as the database file is retained.

To use the project's SQL Server LocalDB configuration, install LocalDB and run:

```powershell
.\scripts\Start-Local.ps1 -Database SqlServer
```

The script applies SQL Server migrations. Azure hosting uses **Azure SQL**, not the local SQLite file.

### Health Checks

| Local URL | What It Checks |
| --- | --- |
| [https://localhost:7178/api/health](https://localhost:7178/api/health) | Verify that the API responds. |
| [https://localhost:7178/api/health/database](https://localhost:7178/api/health/database) | Verify read access to the saved circuits and public posts tables. |

The expected successful response is `{"status":"ok"}`. Passing the first check alone does not confirm that the database or Yosys works; also test saving and HDL import.

## Install Yosys for HDL Import

On Windows, use the complete **OSS CAD Suite** package, including Yosys and `read_slang`:

- [Pinned Release: 2026-10-20](https://github.com/YosysHQ/oss-cad-suite-build/releases/tag/2026-10-20).
- [Direct Windows x64 Download](https://github.com/YosysHQ/oss-cad-suite-build/releases/download/2026-10-20/oss-cad-suite-windows-x64-20261020.tgz).

Extract the package so that the following file exists relative to the project root:

`tools/oss-cad-suite/environment.ps1`

Make sure there is no extra directory between `tools` and `oss-cad-suite`. Keep the complete package; copying only `yosys.exe` does not provide the required environment and libraries.

From the project root, open PowerShell and test the installation:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
. .\tools\oss-cad-suite\environment.ps1
yosys -V
yosys -Q -p "help read_slang"
```

You should see the Yosys version and help for `read_slang`. If `No such command` appears, the required frontend is unavailable in that package. The Windows version used by the team is `0.70+176`.

The service automatically checks the local path above. If the package is installed elsewhere, set its path for the current session before starting the application:

```powershell
$env:HdlSynthesis__EnvironmentScript = "D:\Tools\oss-cad-suite\environment.ps1"
.\scripts\Start-Local.ps1
```

Replace the path with the actual file location on your machine. You do not need to replace the entire `appsettings.json` file to configure Yosys. Do not commit the package or machine-specific settings to the repository.

Docker includes the Linux package in the image and does not use the Windows tools directory.

## First Example: Full Adder

For a quick example that does not require Yosys, download [full_adder.circuit.json](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/examples/full_adder.circuit.json) and open it using the circuit file import option in the interface.

To test the complete HDL pipeline, open **Import HDL** and paste the code below, or upload [full_adder.sv](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/examples/full_adder.sv):

```systemverilog
module full_adder(
    input logic a,
    input logic b,
    input logic cin,
    output logic sum,
    output logic cout
);
    assign sum = a ^ b ^ cin;
    assign cout = (a & b) | (cin & (a ^ b));
endmodule
```

Enter `full_adder` in **Top module**, then click **Import HDL**. The top module must match the module name declared in the source, without a file extension.

After importing, toggle the switches and open **Truth Table**. Expected results:

| a | b | cin | sum | cout |
| --- | --- | --- | --- | --- |
| 0 | 0 | 0 | 0 | 0 |
| 0 | 0 | 1 | 1 | 0 |
| 0 | 1 | 0 | 1 | 0 |
| 0 | 1 | 1 | 0 | 1 |
| 1 | 0 | 0 | 1 | 0 |
| 1 | 0 | 1 | 0 | 1 |
| 1 | 1 | 0 | 0 | 1 |
| 1 | 1 | 1 | 1 | 1 |

Download the circuit as JSON or generate a new HDL file using **Export HDL**. Switches return to their previous states after truth table generation.

## Saving and Sharing

### Saved Circuits

Private saves belong to an anonymous browser session identified by a protected cookie. The client automatically establishes the session. This version does not include registered accounts with passwords.

Another browser, including a phone browser, does not automatically have access to private circuits saved on your computer. Clearing site data or losing session protection keys may prevent access through that session. Keep independent JSON backups of important work.

### Community Sharing

1. Prepare the circuit and enter its name, your display name, and a description.
2. Publish a copy in **Community**.
3. Visitors can browse posts, search by circuit or publisher name, and open an independent copy.
4. Editing an opened copy does not change the original post.
5. The publisher's browser can unpublish its copy.

Published content is public. Display names are visitor-provided and are not verified identities. Opening a shared post link on a phone shares the circuit without transferring ownership of the private save.

## Use a Phone on Your Local Network

Stop the previous instance with `Ctrl+C`, then run from the project root:

```powershell
.\scripts\Start-Local.ps1 -Lan
```

In a second PowerShell window, use `ipconfig` to find the Wi-Fi connection's IPv4 address. On your phone, open `http://COMPUTER-IP:5210`, for example `http://192.168.1.30:5210` only if that is your computer's current address.

- The phone and computer must be on the same network, and both the computer and application must remain running.
- Use the actual Wi-Fi address and recheck it when the connection changes. A VPN or virtual adapter may belong to another network.
- If necessary, allow the application to receive connections on port `5210` through Windows Firewall on your trusted local network.
- If `/api/health` does not open on the phone, check network access, firewall rules, and router client isolation before troubleshooting Blazor.
- On a phone, `localhost` refers to the phone itself. Access from outside the local network requires deployment to a server.

## Project Structure and Key Files

| Directory or File | Responsibility |
| --- | --- |
| [`LogicCore`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/tree/beta-2/LogicCore) | Circuit logic independent of the frontend and API. |
| `Gates` and `Interfaces/IGate.cs` | Definitions for gates, switches, lamps, inputs, and outputs. |
| [`CircuitManager.cs`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/LogicCore/Management/CircuitManager.cs) | Manage gates, wires, simulation, and imported circuit replacement. |
| `Connections/Wire.cs` | Define a connection from one gate output to another gate input. |
| `Validation/CircuitValidator.cs` | Validate connections, circuit structure, and loops. |
| `Analysis/CircuitValidator.cs` | Analysis validation before result generation and export. |
| [`TruthTableGenerator.cs`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/LogicCore/Analysis/TruthTableGenerator.cs) | Enumerate input combinations, collect outputs, and restore switch states. |
| `Analysis/TruthTableRow.cs` | Store input and output values for each truth table row. |
| [`VerilogTranslator.cs`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/LogicCore/Analysis/VerilogTranslator.cs) | Convert circuits to Verilog or SystemVerilog and validate module names. |
| [`YosysNetlistAdapter.cs`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/LogicCore/Analysis/YosysNetlistAdapter.cs) | Read Yosys JSON, build gates and wires, and validate a temporary circuit. |
| `Serialization` | Serialize circuits to JSON and reconstruct them. |
| `Simulation/StepSimulator.cs` | Evaluate circuits step by step for presentation in the interface. |
| [`BlazorApp1`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/tree/beta-2/BlazorApp1) | The browser-based user interface. |
| `Pages/CanvasBoard.razor` | Draw and interact with gates, switches, and wires. |
| `Pages/AnalysisPanel.razor` | Truth table, HDL export, and HDL import dialogs. |
| `Pages/SidebarAndApi.razor` and `Pages/CommunityPanel.razor` | File tools, saved circuits, and community sharing. |
| `Services/HdlImportClient.cs` | Send HDL to the service and handle responses and errors. |
| `Services/CircuitApiClient.cs` | Save, load, and sharing requests, plus browser session initialization. |
| `Presentation/WireLayout.cs` and `Pages/CanvasBoard.Presentation.cs` | Wire routing and additional presentation tools. |
| `Pages/WirePresentation.razor` | Render wires using SVG paths and signal colors. |
| `wwwroot/js` and `wwwroot/css` | Presentation interactions, dialogs, styles, and local frontend assets. |
| [`CircuitApi`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/tree/beta-2/CircuitApi) | HTTP endpoints, database, sessions, Yosys execution, and frontend hosting. |
| `Controllers` and `Contracts` | Service routes and request/response contracts. |
| `Services/HdlSynthesisService.cs` | Run Yosys in a temporary directory, read output, and handle timeouts and errors. |
| `Data` and `Models` and `Migrations` | Database structure, `Circuits` and `SharedCircuits` tables, and SQL Server migrations. |
| [`examples`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/tree/beta-2/examples) | Full Adder examples in HDL and JSON formats. |
| [`tests`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/tree/beta-2/tests) | Logic, API, and presentation checks. |
| [`scripts`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/tree/beta-2/scripts) | Local startup, checks, and Azure deployment. |
| [`Dockerfile`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/Dockerfile) and [`deployment/main.bicep`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/deployment/main.bicep) | Build the Linux image and define Azure resources and configuration. |

## HDL Import Pipeline and API Contract

The frontend sends source code to the service, which runs Yosys to generate a JSON netlist. `YosysNetlistAdapter` reads the output and builds a temporary circuit. The current circuit is replaced only after successful conversion and validation.

**Endpoint:** `POST /api/hdl/synthesize`

Example standalone request for an AND circuit:

```json
{
  "source": "module demo(input a, input b, output y); assign y = a & b; endmodule",
  "topModule": "demo"
}
```

| Response Field | Type | Meaning |
| --- | --- | --- |
| `netlistJson` | string | The JSON document produced by Yosys, serialized as a string within the API response. |
| `diagnostics` | string | Diagnostic messages when processing fails; empty in the current successful response path. |

The service runs these Yosys commands:

```text
read_slang input.sv
hierarchy -top <topModule>
proc
flatten
opt
techmap
opt
write_json netlist.json
```

Exact cell type names accepted after `techmap`:

| Cell Type | Gate | Inputs | Output |
| --- | --- | --- | --- |
| `$_AND_` | AND | `A`, `B` | `Y` |
| `$_OR_` | OR | `A`, `B` | `Y` |
| `$_NOT_` | NOT | `A` | `Y` |
| `$_NAND_` | NAND | `A`, `B` | `Y` |
| `$_NOR_` | NOR | `A`, `B` | `Y` |
| `$_XOR_` | XOR | `A`, `B` | `Y` |

The adapter reads `modules[topModule].ports` and `modules[topModule].cells`. Connections use the bit identifiers in `bits` and `connections`; `type` identifies the gate. An automatically generated cell name is not its gate type. Each supported port or terminal contains an array with one numeric bit identifier.

Default service limits are a **30-second** processing timeout, **100,000 source characters**, and a **10 MB** netlist file. Up to two synthesis jobs run concurrently per application process. These are operational limits, not a guarantee that every design within them is supported.

### Other Main Endpoints

| Method | Endpoint | Purpose |
| --- | --- | --- |
| `GET` | `/api/session` | Create or restore the browser session. |
| `GET` | `/api/circuits` | List circuits saved by the session. |
| `POST` | `/api/circuits` | Save a circuit. |
| `GET` | `/api/circuits/{id}` | Load a saved circuit. |
| `PUT` | `/api/circuits/{id}` | Update a saved circuit. |
| `DELETE` | `/api/circuits/{id}` | Delete a saved circuit. |
| `GET` | `/api/community?search=...&page=1` | Browse and search public posts. |
| `GET` | `/api/community/{id}` | Read a public post and its circuit data. |
| `POST` | `/api/community` | Publish a public copy within the browser session. |
| `DELETE` | `/api/community/{id}` | Unpublish a post owned by the current session. |

Private saves and publishing use the session cookie. The included client initializes and sends it automatically.

## Run with Docker

Start Docker Desktop in **Linux containers** mode. `Dockerfile` builds the frontend and API and includes the Linux Yosys package. `compose.yaml` runs a separate SQL Server container with persistent volumes for database data and session keys.

Create a local `.env` file in the project root:

```powershell
notepad .env
```

Add a line beginning with `SQL_PASSWORD=` followed by a private, strong password that meets SQL Server requirements, then save the file. Do not use a semicolon in the password because it is included in the connection string. Do not commit this file to Git. Do not assume an `.env.example` file exists in this repository version.

Then run:

```powershell
docker compose up --build -d
```

Open **[http://localhost:8080](http://localhost:8080)** after the application and database finish starting. To inspect logs or stop the containers:

```powershell
docker compose logs --tail 100 simulator
docker compose down
```

Running `down` without `--volumes` retains the volumes. Back up any data you need to keep.

## Deploy to Azure

Full walkthrough: [AZURE_AR.md](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/docs/AZURE_AR.md). The student deployment workflow uses [`Deploy-Azure-Student.ps1`](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/scripts/Deploy-Azure-Student.ps1) to build the image with local Docker and upload it to ACR.

### Deployment Resources

| Resource | Role |
| --- | --- |
| Azure App Service on Linux, B1 Plan | Run the application container and serve the frontend and API over HTTPS. |
| Azure Container Registry Basic | Store the application image, including Yosys. |
| Azure SQL Database Basic | Persist circuits and posts instead of using a local database. |
| Managed Identity and AcrPull Permission | Allow the application to pull its image from ACR. |
| Data Protection Key Storage | Keep browser sessions readable after restarts. |

These are paid resources and may consume your student subscription credit. Review your subscription, balance, permissions, and available regions before deployment. `-CheckOnly` validates local tools and the template; it does not guarantee that your subscription permits every resource in the selected region.

Install [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli-windows), reopen PowerShell, sign in, and check your subscription:

```powershell
az login
az account show --query "{Name:name,State:state}" --output table
docker info --format '{{.OSType}}'
```

The subscription must be `Enabled`, and Docker should print `linux`. Then, from the project root:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Deploy-Azure-Student.ps1 -Prefix logiccircuitbeta2 -ResourceGroup logic-circuit-rg -Location westeurope -CheckOnly
```

After the checks pass and you select appropriate resource values, deploy:

```powershell
.\scripts\Deploy-Azure-Student.ps1 -Prefix logiccircuitbeta2 -ResourceGroup logic-circuit-rg -Location westeurope
```

The script prompts for the Azure SQL administrator password using hidden input. Use a private password of **at least 12 characters** that meets SQL Server complexity requirements, without semicolons, double quotes, or line breaks. Retain it for retries against the same resources. Do not put it in the README, Git, or team messages.

Keep your computer and Docker running during the build and upload. Deployment is not complete until `Application ready: https://...` appears after the database health check succeeds. Once deployed, the Azure site runs independently of your personal computer.

The script builds the image before requesting Azure resources. If it fails after resource creation, inspect the resources and errors before retrying; provisioned resources may remain and incur charges. Uploading a Docker image and provisioning Azure resources alone do not verify all application features.

### What to Test on the Public URL

1. Open the application and verify `/api/health` and `/api/health/database`.
2. Save, load, update, and delete a circuit using Azure SQL.
3. Publish a circuit in Community, open a copy in another browser, and unpublish it from the publisher's browser.
4. Import `full_adder.sv` with `full_adder` as the top module and verify all eight truth table rows.
5. Restart the application and verify that saved circuits and session ownership persist.
6. Open the URL on a physical phone and test touch interaction, file uploads, and truth tables in portrait and landscape orientations.

Pushing code to GitHub does not automatically run the site. GitHub Actions deployment can be added later. This guide uses the manual deployment script and does not assume an enabled workflow.

## Checks and Documentation

Run engine and presentation checks from the project root:

```powershell
.\scripts\Run-Checks.ps1
dotnet run --project tests/PresentationChecks
```

For API checks, start the application first and use a **separate test database**:

```powershell
.\scripts\Run-Checks.ps1 -ApiBaseUrl https://localhost:7178/
```

API checks create, update, and delete records. HDL-related checks require Yosys. Do not run them directly against a database containing important data.

| Document | Contents |
| --- | --- |
| [Arabic Explanation (Word)](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/docs/LogicCircuitSimulator_Explanation_AR.docx) | Project structure, files, functions, startup steps, and changes. |
| [TEST_RESULTS.md](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/docs/TEST_RESULTS.md) | Recorded check results, environments, and limitations. |
| [CHANGES.md](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/docs/CHANGES.md) | Integration, presentation, and runtime changes. |
| [Azure Guide (Arabic)](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/docs/AZURE_AR.md) | Deployment tools, resources, and public URL checks. |
| [Azure Guide (English)](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/docs/AZURE.md) | Deployment configuration and architecture details. |
| [THIRD_PARTY_NOTICES.md](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/blob/beta-2/THIRD_PARTY_NOTICES.md) | Third-party library and template notices. |

Test reports document results from specific environments. Their presence does not certify that the current Azure deployment, Azure SQL, or a physical phone has been tested. Verify these separately on the deployed service. This README does not announce a hosted URL or claim deployment is complete.

## Troubleshooting

| Problem | Action |
| --- | --- |
| `dotnet` not recognized or incorrect SDK | Install .NET 10 SDK, open a new terminal, and verify with `dotnet --info`. |
| Local HTTPS certificate is not trusted | Run `dotnet dev-certs https --trust` and accept the confirmation. |
| `CircuitDatabase connection string is missing` | Use `Start-Local.ps1` or ensure `ConnectionStrings:CircuitDatabase` is configured in the service; preserve other configuration sections. |
| `Unable to reach the HDL API` | Start with `/api/health` and unified hosting on `7178`. For separate hosting, check `ApiBaseUrl`, HTTPS certificates, and CORS. |
| HTTP 405 on `/api/hdl/synthesize` | This endpoint accepts `POST`. Test it with Import HDL instead of opening it as a page, and ensure requests reach CircuitApi. |
| `Module demo not found` | Match Top module to the module declared in the source; the Full Adder example requires `full_adder`. |
| Yosys fails to start or cannot find `read_slang` | Check the complete package, the `environment.ps1` path, and output from `yosys -V` and `help read_slang`. |
| `Unsupported cell type` | Check adapter limitations and the six supported cell types; flip-flops and buses are not supported in this version. |
| Database health check returns 503 | Check the database provider, connection string, service availability, tables, and migrations. |
| Circuits saved on the computer do not appear on the phone | Each browser has its own ownership session. Transfer JSON or publish a copy in Community. |
| Phone keeps loading on the local network | Check `-Lan` mode, the current Wi-Fi address, port `5210`, firewall rules, and router client isolation. |
| `docker` produces no output | Run `Get-Command docker -All`. If another program shadows Docker, use the actual `docker.exe` path as shown below. |
| Docker reports `unable to start` or the build stops with EOF | Check disk space, Docker logs, and engine status. Restart Docker if needed. Save work in WSL distributions before stopping WSL. Do not assume deleting images or volumes is the solution. |
| Azure deployment fails because of region or permission restrictions | Read the deployment error, identify the required region or permission for your subscription, and inspect existing resources before retrying. |

If Docker is installed in its standard Windows location but another command takes precedence, select the correct executable for the current PowerShell session:

```powershell
Set-Alias -Name docker -Value "C:\Program Files\Docker\Docker\resources\bin\docker.exe" -Scope Global
docker --version
docker info --format '{{.OSType}}'
```

This alias applies only to the current session. Recreate it in a new terminal if the issue persists.

## Team Workflow

- `main` is the repository entry point and overview page. The version described here is on `beta-2`.
- Start from the latest `beta-2` and create a separate branch for each change. Target pull requests at `beta-2` while it remains the active development branch.
- Preserve team branches and contribution history. Updating this README does not require deleting branches or rewriting Git history.
- Do not commit `tools`, `bin`, `obj`, `.vs`, local databases, `.env`, certificates, passwords, or machine-specific settings.
- Review changes before committing and record the source version you build or deploy.

[Team Repository](https://github.com/LogicCircuit-Team/LogicCircuitSimulator) · [Contributors](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/graphs/contributors) · [Start with Beta 2](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/tree/beta-2)
