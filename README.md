# Logic Circuit Simulator

A circuit workspace built with C#, Blazor WebAssembly and ASP.NET Core. Place gates, connect wires, simulate combinational circuits, generate truth tables and move between circuit JSON and Verilog or SystemVerilog.

The application uses the team's original `LogicCore` engine. The final integration adds community publishing, browser-owned saved circuits, mobile controls and a container deployment for Azure.

## What it does

- Draw and edit AND, OR, NOT, NAND, NOR and XOR circuits with switches and lamps.
- Reject invalid connections, duplicate input drivers and combinational cycles.
- Generate a truth table for up to eight switches, then restore their previous states.
- Import and download circuit JSON.
- Export `.v` or `.sv` files and import HDL through Yosys with `read_slang`.
- Save, load, update and delete circuits in a database.
- Publish a circuit snapshot, browse and search community circuits, and open an independent copy.
- Open Truth Table in a wide centered window, with expand, close and keyboard controls.
- Open Import HDL and Export HDL in matching windows, and return to the same circuit.
- Collapse or resize side panels on desktop, and use sliding panels on a phone.
- Navigate with horizontal and vertical sliders and touch controls. Wires use separate orthogonal routes with visible crossover clearance.

The HDL adapter accepts single-bit combinational cells `$_AND_`, `$_OR_`, `$_NOT_`, `$_NAND_`, `$_NOR_` and `$_XOR_`. Flip-flops, memories, buses and constant-bit connections are outside this version. An unsupported import leaves the current circuit intact. The existing Step controls expose signal transfer and gate evaluation; they are not a clocked sequential simulator.

## Run locally on Windows

Install the .NET 10 SDK. For HDL import, also install the complete OSS CAD Suite as described in [tools/README.md](tools/README.md).

From the folder containing `LogiclyInspiredSimulator.slnx`, open PowerShell and run:

```powershell
dotnet dev-certs https --trust
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\scripts\Start-Local.ps1
```

Open **https://localhost:7178**. `CircuitApi` serves both the API and the compiled Blazor client, so this route needs one running project and no separate API URL. The script selects the SQLite preview database at `App_Data/circuits.db` and creates its tables. Restarting the application preserves that file.

To use the team's SQL Server LocalDB configuration instead:

```powershell
.\scripts\Start-Local.ps1 -Database SqlServer
```

SQL Server LocalDB must be installed. The script applies the SQL Server migrations. SQLite is an explicit local preview option; the Azure deployment uses Azure SQL.

### Try it on a phone on the same network

```powershell
.\scripts\Start-Local.ps1 -Lan
```

Keep that window running. Open a second PowerShell window and run `ipconfig` to find the Wi-Fi IPv4 address. Open `http://YOUR-PC-IPv4:5210` on the phone, with both devices on the same Wi-Fi. Allow the application through Windows Firewall on that private network if Windows asks. `localhost` on the phone refers to the phone itself. Keep the PC and application running. Public hosting uses HTTPS and a server URL instead.

## Try a full adder

Import [examples/full_adder.sv](examples/full_adder.sv), or paste its contents into Import HDL. The top module is `full_adder`. Generate the truth table to see its eight input combinations. The example JSON can also be loaded without Yosys.

For a different pasted design, Top module must match the HDL module declaration. The interface suggests a name and preserves a manually entered choice.

## Saved circuits and community

Saved circuits belong to an anonymous browser session identified by a protected cookie. Another browser cannot list, change or delete them. This is browser ownership, not a registered user account. Clearing site data, using another browser or losing the server's data-protection keys loses that browser's ownership. Download JSON copies for work you want to keep independently.

Community publications are public snapshots. Editing an opened copy does not change its publication. The publishing browser can unpublish its snapshot. Display names are entered by visitors and are not verified identities. Old saved rows without an owner remain accessible in Development only; production does not expose them.

## Projects

| Folder | Responsibility |
| --- | --- |
| `LogicCore` | Gates, wires, validation, simulation, serialization, truth tables and HDL adapters |
| `BlazorApp1` | Canvas, component controls, analysis and community interface |
| `CircuitApi` | Saved circuits, browser sessions, public sharing, database and Yosys process |
| `examples` | Full adder and other circuit samples |
| `tests` | Core and API regression checks |
| `deployment` | Azure infrastructure definition |
| `scripts` | Local startup, checks and Azure deployment |

The interface adapts the supplied **Logic Space Classic Bulb** template. Its white and green workbench, component library, Bootstrap 5.3.3 controls and Bootstrap Icons 1.11.3 are bundled locally. Phone panels use Bootstrap Offcanvas. Analysis dialogs use the browser's native dialog element with Bootstrap styling, so their content is not clipped by a sidebar. The existing C# generation and import methods are reused. The existing C# canvas renderer and logic engine remain in use; the template's independent JavaScript simulator and Konva are not included. The separate presentation extension supplies wire routes and matching hit testing without changing the original renderer or any original C# file or Razor `@code` block. Pan and zoom affect the view, not the saved gate coordinates. Shared-source leads can meet; separated crossings are not electrical junctions. Dense or overlapping gate arrangements can still need manual gate placement. Only the twenty used icons are packaged in a small local font. Third-party notices are retained in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

## Deploy before publishing the repository

Start with the detailed Arabic guide [docs/AZURE_AR.md](docs/AZURE_AR.md). The new `scripts/Deploy-Azure-Student.ps1` builds on local Docker and pushes to ACR, avoiding ACR Tasks on free-credit accounts. [docs/AZURE.md](docs/AZURE.md) covers a single HTTPS application with Azure SQL, persistent browser-session keys and a Linux container containing Yosys. Deployment builds directly from these local files; GitHub is not required.

The Azure script creates **paid** App Service B1, Azure SQL Basic and Container Registry Basic resources in your selected subscription. Review the subscription and resource group before running it. No cloud URL or completed Azure test is claimed in this package.

For Docker on a development machine:

```powershell
Copy-Item .env.example .env
notepad .env
docker compose up --build -d
```

Set a strong `SQL_PASSWORD` in `.env`, then open `http://localhost:8080`. Docker builds the complete Linux Yosys suite into the image; it does not use the Windows `tools` folder. This container route requires Docker and was not executed in the editing environment.

## Checks and documentation

```powershell
.\scripts\Run-Checks.ps1
dotnet run --project tests/PresentationChecks
.\scripts\Run-Checks.ps1 -ApiBaseUrl https://localhost:7178/
```

Use a disposable database for API checks because they create, update and remove test records. See [docs/TEST_RESULTS.md](docs/TEST_RESULTS.md) for the checks actually run and the remaining deployment checks. [docs/CHANGES.md](docs/CHANGES.md) records the integration changes. The Arabic Word guide in `docs` explains the project from the beginning, its files and methods, and the removed source comments.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| `Module demo not found` | Set Top module to the declared name, such as `full_adder`. |
| HTTP 405 from the HDL route | Open the integrated application at port 7178. If using a separate client, point `ApiBaseUrl` to `CircuitApi`, not the client development server. |
| API cannot be reached | Confirm `/api/health` works. For separate hosts, check HTTPS trust, the API address and explicit CORS origins. |
| Database health returns 503 | Check the selected database provider, connection string, service availability and migrations. |
| Yosys cannot start | Keep the complete suite and check `tools/oss-cad-suite/environment.ps1`, or set `HdlSynthesis:EnvironmentScript` to its real Windows path. |
| Unsupported HDL cell | Use the supported combinational subset. A successful HDL compilation does not guarantee that every synthesized cell is supported by the adapter. |

Do not commit `tools` binaries, `bin`, `obj`, `.env`, databases, certificates, private settings or published build output. The source archive leaves these out. Keep their local copies outside version control.
