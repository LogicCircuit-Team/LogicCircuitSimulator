# Logic Circuit Simulator — Beta 1

A web-based digital logic circuit simulator built with **C#**, **Blazor WebAssembly**, and **ASP.NET Core**.

The project allows users to create and connect logic gates, simulate circuits, generate truth tables, export **Verilog/SystemVerilog**, import combinational HDL circuits through **Yosys**, and save circuits locally or in **SQL Server**.

| Item | Value |
| --- | --- |
| Repository | [LogicCircuit-Team/LogicCircuitSimulator](https://github.com/LogicCircuit-Team/LogicCircuitSimulator) |
| Integrated Branch | `beta-1` |
| Release Tag | `v0.1.0-beta.1` |
| Release | [Beta 1](https://github.com/LogicCircuit-Team/LogicCircuitSimulator/releases/tag/v0.1.0-beta.1) |
| Framework | `.NET 10` |
| Status | Beta / Pre-release |

This README documents **Beta 1**, where the frontend and API run as two separate projects. The integrated version uses the third member's sidebar, the first member's canvas and simulation work, and the second member's analysis and HDL features.

---

## Project Structure

| Project | Responsibility |
| --- | --- |
| `BlazorApp1` | User interface, canvas, gate tools, truth table, HDL export/import requests, and circuit saving |
| `LogicCore` | Gates, wires, simulation logic, circuit validation, JSON serialization, and netlist conversion |
| `CircuitApi` | HTTP API for SQL Server persistence and server-side Yosys execution |

All workspace components share a single `CircuitManager` instance.

When a circuit is loaded or imported successfully, it is first built and validated in a temporary circuit before replacing the currently active circuit. The canvas, counters, and analysis state are then refreshed.

---

## Requirements

- **.NET 10 SDK**
- **Visual Studio 2026 version 18.0 or later**
- ASP.NET and web development workload
- **Git**
- **OSS CAD Suite** for HDL import
- A Yosys build that supports `read_slang`
- SQL Server or SQL Server Express LocalDB for database persistence
- `dotnet-ef` for Entity Framework migrations

> Simulation, truth-table generation, local JSON files, and HDL export do **not** require SQL Server or Yosys.

> HDL import requires `CircuitApi` and Yosys, but does not require SQL Server.

---

## Quick Start on Windows

Run all commands from the directory containing:

```text
LogiclyInspiredSimulator.slnx
BlazorApp1/
LogicCore/
CircuitApi/
```

### 1. Restore Packages

```bat
dotnet restore LogiclyInspiredSimulator.slnx
```

### 2. Configure the API URL

Open:

```text
BlazorApp1/wwwroot/appsettings.json
```

Set:

```json
{
  "ApiBaseUrl": "https://localhost:7178/"
}
```

The Blazor frontend normally runs at:

```text
https://localhost:7007/
```

The API normally runs at:

```text
https://localhost:7178/
```

Server-side settings such as SQL Server, CORS, and Yosys are stored in:

```text
CircuitApi/appsettings.json
```

---

## Run CircuitApi

Open a terminal in the solution directory and run:

```bat
dotnet run --project CircuitApi --launch-profile https
```

Wait until you see:

```text
Now listening on: https://localhost:7178
```

Keep this terminal open.

---

## Run the Blazor Frontend

Open another terminal in the same directory and run:

```bat
dotnet run --project BlazorApp1 --launch-profile https
```

Then open:

```text
https://localhost:7007/
```

Running `BlazorApp1` alone does **not** start `CircuitApi`.

---

## Verify the Connection

| Purpose | URL | Expected Result |
| --- | --- | --- |
| Open the UI | `https://localhost:7007/` | Circuit Workspace page |
| API health check | `https://localhost:7178/api/health` | `{"status":"ok"}` |
| Frontend configuration | `https://localhost:7007/appsettings.json` | Correct `ApiBaseUrl` |
| HDL synthesis | `POST https://localhost:7178/api/hdl/synthesize` | JSON response |
| Circuit API | `https://localhost:7178/api/circuits` | Circuit data when SQL Server is configured |

After changing frontend configuration, restart the frontend and press:

```text
Ctrl + Shift + R
```

---

## Connection Troubleshooting

### HTTP 405 During HDL Import

If the request is sent to:

```text
https://localhost:7007/api/hdl/synthesize
```

then the request is going to the Blazor frontend instead of `CircuitApi`.

Check:

1. `BlazorApp1/wwwroot/appsettings.json`
2. `https://localhost:7007/appsettings.json`
3. `BlazorApp1/Program.cs`
4. `HdlImportClient`
5. The API port

The correct API address should be:

```text
https://localhost:7178/
```

---

## Unable to Reach the HDL API

Open:

```text
https://localhost:7178/api/health
```

If it fails, make sure `CircuitApi` is running.

If the browser reports a localhost certificate issue, run:

```bat
dotnet dev-certs https --trust
```

Then restart the browser and both projects.

If the health endpoint works but the frontend request fails, inspect the browser Console and Network tabs using `F12`.

Check the CORS settings in:

```text
CircuitApi/appsettings.json
```

Example:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://localhost:7007",
      "http://localhost:5232"
    ]
  }
}
```

Restart `CircuitApi` after changing CORS settings.

---

## Configure Yosys on Windows

Beta 1 loads OSS CAD Suite through `environment.ps1`.

Configure:

```text
CircuitApi/appsettings.json
```

Example:

```json
{
  "HdlSynthesis": {
    "EnvironmentScript": "C:\\Tools\\oss-cad-suite\\environment.ps1",
    "TimeoutSeconds": 30,
    "MaxSourceCharacters": 100000
  }
}
```

The path above is only an example.

Use the real path on the machine running the API.

### Verify Yosys

Open PowerShell:

```powershell
. "C:\Tools\oss-cad-suite\environment.ps1"
yosys -V
yosys -Q -p "help read_slang"
```

Yosys must recognize:

```text
read_slang
```

---

## HDL Synthesis Flow

The API uses this Yosys flow:

```text
read_slang input.sv
hierarchy -top TOP_MODULE
proc
flatten
opt
techmap
opt
write_json netlist.json
```

Default limits:

| Setting | Value |
| --- | --- |
| Yosys timeout | 30 seconds |
| Frontend client timeout | 45 seconds |
| Maximum HDL source length | 100,000 characters |
| Maximum HTTP request body | 1,000,000 bytes |

---

## HDL Import

To import HDL:

1. Open a `.sv` or `.v` file in a text editor.
2. Copy the source code.
3. Paste it into the **HDL code** field.
4. Enter the top-level module name.
5. Click **Import HDL**.
6. Generate a truth table if needed.

Example:

```text
Top module: full_adder
```

The sidebar file picker is intended for circuit `.json` files.

It does not directly upload `.sv` or `.v` files in Beta 1.

---

## HDL Export

Enter a module name.

Optionally enable:

```text
Use SystemVerilog (.sv)
```

Then click:

```text
Generate Verilog
```

Extensions:

- `.v` for Verilog
- `.sv` for SystemVerilog

Changing the module name, format, or circuit invalidates old generated HDL until it is generated again.

---

## Local Circuit Save Files

Use:

```text
Download Circuit
```

to export the current circuit as JSON.

The format contains:

```text
schemaVersion
gates
wires
```

This is different from Yosys netlist JSON.

---

## Full Adder Example

### Local Test

Load:

```text
examples/full_adder.circuit.json
```

Then click:

```text
Generate Truth Table
```

This test does not require SQL Server or Yosys.

### HDL Test

Use:

```text
examples/full_adder.sv
```

Set:

```text
Top module: full_adder
```

Then click:

```text
Import HDL
```

The example contains:

- 3 switches
- 2 XOR gates
- 2 AND gates
- 1 OR gate
- 2 lamps
- 10 total gates
- 12 wires

Expected truth table:

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

---

## Supported Circuit Scope

HDL import currently supports combinational circuits using single-bit ports.

Supported Yosys cell types:

| Simulator Gate | Yosys Cell Type | Inputs | Output |
| --- | --- | --- | --- |
| AND | `$_AND_` | A, B | Y |
| OR | `$_OR_` | A, B | Y |
| NOT | `$_NOT_` | A | Y |
| NAND | `$_NAND_` | A, B | Y |
| NOR | `$_NOR_` | A, B | Y |
| XOR | `$_XOR_` | A, B | Y |

The adapter uses:

- Cell type
- Connections
- Bit identifiers

It does not rely on auto-generated Yosys cell names.

---

## Current Limitations

- Flip-flops are not supported
- Latches are not supported
- Memory is not supported
- Clocked logic is not supported
- Timing simulation is not supported
- Multi-bit ports are not supported
- Unsupported Yosys cell types are rejected
- Truth-table generation supports up to **8 switches**
- Structural cycles are rejected
- Multiple signal sources connected to one input are rejected
- Analysis and HDL export require complete gate inputs
- Original switch states are restored after truth-table generation
- Community and account features are not included in Beta 1

---

## SQL Server Setup

Configure:

```text
ConnectionStrings:CircuitDatabase
```

inside:

```text
CircuitApi/appsettings.json
```

Default LocalDB example:

```json
{
  "ConnectionStrings": {
    "CircuitDatabase": "Server=(localdb)\\MSSQLLocalDB;Database=CircuitDb;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

If you use another SQL Server instance, replace the connection string.

---

## Entity Framework Setup

Install EF CLI:

```bat
dotnet tool install --global dotnet-ef --version 10.0.13
```

If already installed:

```bat
dotnet tool update --global dotnet-ef --version 10.0.13
```

Apply migrations:

```bat
dotnet ef database update --project CircuitApi --startup-project CircuitApi
```

After the database is ready, the application supports:

- Save
- Refresh saved circuits
- Load selected circuit
- Update selected circuit
- Delete selected circuit

---

## HDL API Contract

Endpoint:

```text
POST /api/hdl/synthesize
```

Example request:

```json
{
  "source": "module demo(input a, input b, output y); assign y = a & b; endmodule",
  "topModule": "demo"
}
```

The response contains:

```text
netlistJson
diagnostics
```

The frontend passes `netlistJson` to:

```text
YosysNetlistAdapter.ImportFromJson
```

A successful `/api/health` response does not guarantee successful HDL synthesis.

Always test synthesis with a simple circuit after configuring Yosys.

---

## Integration and Performance Improvements

Beta 1 includes:

- Shared `CircuitManager`
- Dedicated API clients
- Automatic canvas refresh
- Automatic analysis invalidation
- Circuit replacement only after validation
- Batched canvas commands
- `requestAnimationFrame` redraw scheduling
- Reusable simulation evaluation plans
- Temporary circuit construction before replacement
- Optimized truth-table generation
- Original switch state restoration
- Deterministic identifier ordering
- Circuit JSON schema validation
- Blob/Object URL downloads
- Duplicate CRUD request prevention
- English UI messages

No specific FPS or benchmark claims are made for this release.

---

## Important Files

| File / Folder | Role |
| --- | --- |
| `BlazorApp1/Pages/Home.razor` | Shared circuit manager and workspace integration |
| `BlazorApp1/Pages/CanvasBoard.razor` | Canvas drawing, interaction, and simulation |
| `BlazorApp1/Pages/SidebarAndApi.razor` | Gate tools, save/load, and API actions |
| `BlazorApp1/Pages/AnalysisPanel.razor` | Truth table and HDL tools |
| `BlazorApp1/Program.cs` | HTTP client registration |
| `BlazorApp1/Services/CircuitApiClient.cs` | Circuit CRUD client |
| `BlazorApp1/Services/HdlImportClient.cs` | HDL synthesis client |
| `BlazorApp1/wwwroot/appsettings.json` | API URL |
| `BlazorApp1/wwwroot/js/circuit-ui.js` | UI helper functions |
| `CircuitApi/CircuitApi.csproj` | API dependencies |
| `CircuitApi/Program.cs` | API configuration |
| `CircuitApi/Controllers/CircuitsController.cs` | Circuit CRUD endpoints |
| `CircuitApi/Controllers/HdlSynthesisController.cs` | HDL synthesis endpoint |
| `CircuitApi/Services/HdlSynthesisService.cs` | Yosys execution |
| `CircuitApi/appsettings.json` | SQL, CORS, and Yosys settings |
| `LogicCore/Management/CircuitManager.cs` | Circuit management and simulation |
| `LogicCore/Validation/CircuitValidator.cs` | Circuit validation |
| `LogicCore/Analysis/TruthTableGenerator.cs` | Truth-table generation |
| `LogicCore/Analysis/VerilogTranslator.cs` | HDL generation |
| `LogicCore/Analysis/YosysNetlistAdapter.cs` | Yosys netlist import |
| `LogicCore/Serialization/CircuitSerializer.cs` | JSON serialization |
| `LogicCore/Simulation/StepSimulator.cs` | Step simulation |
| `examples/full_adder.circuit.json` | Local circuit example |
| `examples/full_adder.sv` | HDL example |

---

## Team Workflow

Place this file as:

```text
README.md
```

next to:

```text
LogiclyInspiredSimulator.slnx
```

Commit it using:

```bat
git add README.md
git commit -m "Add Beta 1 setup and troubleshooting guide"
git push origin beta-1
```

The release tag:

```text
v0.1.0-beta.1
```

points to the code snapshot that existed when the release was created.

Later commits to `beta-1` do not automatically change the existing release archive.

---

## Beta Status

Beta 1 is a pre-release version.

Recommended validation steps:

1. Start `CircuitApi`
2. Test `/api/health`
3. Start `BlazorApp1`
4. Load a local circuit
5. Generate a truth table
6. Test HDL import
7. Configure SQL Server
8. Test save/load/update/delete

If NuGet reports:

```text
NU1902
NU1903
```

review the affected dependency and upgrade to a patched compatible version.

Do not disable vulnerability scanning just to hide the warning.

---

## References

- [.NET installation on Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows)
- [.NET SDK and Visual Studio compatibility](https://learn.microsoft.com/en-us/dotnet/core/porting/versioning-sdk-msbuild-vs)
- [Blazor configuration](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/configuration?view=aspnetcore-10.0)
- [Calling web APIs from Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/call-web-api?view=aspnetcore-10.0)
- [ASP.NET Core HTTPS](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl)
- [Entity Framework Core CLI](https://learn.microsoft.com/en-us/ef/core/cli/dotnet)
- [OSS CAD Suite](https://github.com/YosysHQ/oss-cad-suite-build)
- [Excubo.Blazor.Canvas](https://github.com/excubo-ag/Blazor.Canvas)
- [GitHub Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository)
