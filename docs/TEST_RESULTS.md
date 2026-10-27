# Final verification

Verification date: 26 October 2026. The project was checked from the supplied source archive. No GitHub branch was changed.

## Executed checks

| Area | Passed checks | Coverage |
| --- | ---: | --- |
| LogicCore | 60 | All six gate truth patterns, actual Yosys full-adder fixture, eight truth rows, JSON round trips, switch and lamp state restoration, rejected cycles, unsupported sequential cells and export |
| HTTP API | 26 | Real database CRUD, separate browser owners, public publication/search/detail, independent copies, unpublishing, malformed circuit rejection and actual Yosys execution |
| API restart | 3 | Private record, public publication and browser ownership survive restart |
| Chrome regression | 33 | `.sv`, `.v` and JSON uploads, full-adder results, HDL export compiled by Yosys, download contents and preservation of the board after a rejected import |
| Community and mobile browser | 21 | Independent visitors, share links, open copy, unpublish isolation, touch wiring/dragging/toggling, mobile import, top-module suggestion and manual override, portrait/landscape layout |
| **Total** | **143** | Functional assertions; build and infrastructure checks are additional |

The HTTP checks and both browser suites passed against the **Release published application**, not only the development build. Chrome reported no JavaScript runtime errors in those successful runs. Gzip published assets were compared with their uncompressed sources: all 56 matched, with no empty published asset.

The Release publication and project build completed without compiler warnings or errors. EF's pending-model check reported no model changes after the included migration. The Bicep template compiled, the PowerShell scripts parsed, and the package vulnerability audit reported no vulnerable packages in the resolved dependency graph at the time of the check. The 21 original LogicCore C# files were compared as syntax tokens and remained identical after comment removal. Final C# and Razor-code inspection found no source comments.

## Environment and persistence

The checks used .NET SDK 10.0.402 / runtime 10.0.13, a real Linux Yosys installation reporting `0.70+219` with `read_slang`, and headless Chrome. Persistence used the implemented **SQLite provider with a real file**, with the API in Production configuration and two independent cookie stores. The API was stopped and restarted for the persistence checks. No in-memory database substitute was used.

The team's Windows installation reports Yosys `0.70+176`. The Windows instructions and Dockerfile pin the complete OSS CAD Suite release dated 2026-10-20. The Docker image build itself remains a deployment check.

## Performance sample

A warmed native .NET simulation of a chain of 1,000 NOT gates, one switch and one lamp completed 200 state updates in **17.96 ms** in the final measured run. The expected lamp state was checked on each update. An earlier run measured 20.40 ms.

This sample measures the core engine on the checking machine. It does not measure browser drawing, phone frame rate, server throughput or a before/after improvement percentage.

## Checks still required on the hosted service

- Build the Linux image in ACR and confirm its embedded AND synthesis check passes.
- Provision Azure SQL and apply the SQL Server migrations.
- Repeat save, load, update, delete and restart persistence checks on that SQL provider.
- Confirm persistent data-protection keys survive a hosted restart.
- Open the real HTTPS URL on a physical phone and try touch editing, file upload, truth tables and community links in portrait and landscape.

SQL Server could not start in the editing environment, so this report does **not** claim an executed SQL Server or Azure SQL test. A Docker engine and an authenticated Azure subscription were not available there. Phone checks used Chrome touch emulation at 390 × 844 and landscape sizes; no physical handset was connected.

## Reproduce the included checks

```powershell
.\scripts\Run-Checks.ps1
.\scripts\Run-Checks.ps1 -ApiBaseUrl https://localhost:7178/
```

Run API checks against a disposable database. They create and remove test records and require the Yosys synthesis service to be working. The bundled C# projects reproduce the core and HTTP assertions. Browser acceptance results above were also checked using automated Chrome sessions during integration.


## Presentation update 26 October 2026

These checks were run for the new Sneat presentation and are separate from the earlier integration results.

- Application build: succeeded with zero warnings and zero errors. The editing host required an external MSBuild task-host workaround; that environment-only file is not included in the project.
- LogicCore console checks: 60 passed.
- Chrome presentation and interaction checks: 42 passed. Covered JSON load, full-adder truth rows, Verilog and SystemVerilog generation and downloads, validation feedback, native accordion controls, SQLite save/load, community publish/open-copy and mobile panels.
- No horizontal document overflow at widths 320, 390, 768, 1024, 1440 and 1600 CSS pixels.
- No JavaScript page errors and no external UI asset requests in that browser run.
- The complete Arabic guide was rendered and its pages reviewed.

See reports/redesign-browser-report.json for the individual browser checks. These checks do not claim a new real-Yosys synthesis run, Azure deployment, Azure SQL test or physical phone test. Those integration boundaries are unchanged by the presentation work.


## Logic Space Classic Bulb presentation update 26 October 2026

These results apply to the supplied template adaptation and are separate from the earlier integration runs.

- Debug application build succeeded with zero warnings and zero errors on .NET SDK 10.0.100. An external in-process MSBuild task override was used because this editing host does not permit the SDK task host's Unix sockets. The workaround is outside the shipped project.
- The existing LogicCore checks passed all 60 assertions.
- Chrome Headless Shell passed 52 browser assertions: full-adder JSON import and all eight truth rows, Verilog and SystemVerilog output and downloads, invalid-name feedback, real SQLite save/load, step mode, community publish/search/open-copy, Bootstrap Offcanvas panels, zoom and file inputs.
- No horizontal document overflow at widths 320, 390, 768, 991, 992, 1024, 1440 and 1600. The phone analysis button remains inside the viewport.
- No JavaScript page errors and no external UI script, CSS or font requests were observed.
- HDL file-input checks use a controlled HTTP response containing the bundled real-Yosys JSON fixture. They check the existing client contract and adapter; they are not a new synthesis service test.
- Byte comparison confirmed that all 49 C# source files, seven Razor @code blocks and circuit-ui.js are unchanged from the previous delivered ZIP. The supplied template's independent simulator is not included.
- The updated 55-page Arabic Word guide was rendered and reviewed.

See reports/classic-browser-report.json and reports/classic-verification.json. Physical phone networking, actual Azure deployment, Azure SQL and a fresh Yosys process run were not tested in this presentation update.


## Canvas controls and Azure preparation 26 October 2026

- Debug build: zero warnings and errors. Release publication succeeded, including trimming. The external task-host override used only by this editing environment is not shipped.
- All original 49 C# files, seven original Razor `@code` blocks and `circuit-ui.js` retain their previous hashes. New presentation types, a partial CanvasBoard class, an SVG component and a separate script provide the additions.
- Core checks: 60 passed. Presentation route checks: 12 passed, including pin endpoints, orthogonal segments, gate avoidance, separate interior lanes in the full-adder sample, repeatability and preserving circuit JSON. These are geometric assertions on defined samples, not a guarantee for every possible layout.
- Browser acceptance: 61 passed. Additional pointer/touch interaction checks: 16 passed. Covered the toolbar Truth Table shortcut, both view sliders, desktop panel dragging and collapse, actual routed-wire selection/deletion, gate dragging, original pin wiring, duplicate-driver rejection and touch toggling.
- The published Release output was started in Production mode with a real local SQLite database. The browser loaded the full-adder JSON, generated eight truth rows, saved it, reloaded the page, refreshed the saved list and loaded it again using its cookie. This is not an Azure SQL test.
- All JSON configuration files and project XML parse. Source scans found no comments in the shipped source formats. No local databases, secrets, compiled output or large tool binaries are included in the source archive.
- The solution dependency audit against NuGet reported no known vulnerable direct or transitive packages for its five projects at the time of this run. See reports/dependency-audit.json.
- Bicep 0.48.1 compiled the template. PowerShell 7.6.9 parsed all four shipped scripts. Thirteen mocked-command assertions verified student-script command order, avoiding ACR Tasks, the CheckOnly path, temporary SQL parameter cleanup and stopping deployment after build failure. Mocked commands do not provision cloud resources.
- This round did not execute a new Yosys synthesis process, a Docker image build, Azure resource provisioning, Azure SQL or a physical phone. These boundaries remain mandatory deployed checks in AZURE_AR.md.

See reports/controls-browser-report.json, reports/controls-interactions-report.json, reports/deployment-script-checks.json and reports/controls-verification.json. The complete updated Word guide includes the added presentation files and the Windows-to-Azure procedure.


## Centered analysis dialogs 26 October 2026

- Debug build succeeded with zero warnings and errors. Release publication succeeded. An external in-process MSBuild override was used for this host's task-host restrictions and is not part of the archive.
- Fifty-five Chrome browser checks passed. They cover the centered Truth Table dialog and all eight full-adder rows, expand/restore, visible footer actions, Escape and backdrop close, focus return and keyboard confinement, collapsed-sidebar opening, export generation and download, name validation, original import contracts, local SQLite save/load and touch simulation.
- Dialog bounds were checked at 320, 390, 768, 991, 992, 1024, 1440 and 1600 CSS pixels. A 256-row truth table retains all rows and scrolls within the dialog while keeping its return action visible. Phone output columns remain accessible through horizontal scrolling.
- No duplicate HTML identifiers, browser page errors or external UI asset requests were observed. Closing a phone dialog leaves no offcanvas backdrop blocking the board. Original switch touch behavior still works.
- Six additional smoke assertions ran against the published Release application in Production mode with SQLite: dialog opening, columns, footer bounds, closing without losing the circuit, style delivery and absence of JavaScript errors. This is local publication testing, not Azure SQL.
- Byte comparisons preserve all 52 existing C# files, eight original Razor code blocks and circuit-ui.js from the previous ZIP. Only presentation markup, the presentation script, index.html, new analysis-dialogs.css and documentation changed.
- The Word manual now has 64 pages. Four changed/new pages were visually inspected; 60 pages match the previous reviewed guide at the rendered-pixel level.

The HDL browser checks use the bundled JSON fixture through a controlled HTTP response; they do not run Yosys again. Touch tests are Chromium emulation, not a physical phone. Docker and Azure resources, database runtime and deployment configuration were not changed or newly exercised in this update.

See reports/modal-browser-report.json, reports/modal-published-report.json, reports/modal-document-review.json and reports/modal-verification.json.
