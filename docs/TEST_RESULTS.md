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
