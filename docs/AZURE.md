# Azure deployment

This deployment hosts the Blazor client and API together. A phone opens the same HTTPS address as a desktop, and all API requests stay on that origin. The Linux image includes .NET 10 and the complete OSS CAD Suite. The database is Azure SQL, not LocalDB or SQLite.

## Resources

`deployment/main.bicep` creates an App Service Linux B1 plan, a web app, an Azure SQL server with a Basic database, and an ACR Basic registry. These resources incur charges while provisioned. The web app uses a managed identity to pull its container. The SQL connection string is a server setting; it is not embedded in the browser files.

The template enables the Azure-services SQL firewall rule. After deployment, review that network access in the subscription and narrow it if required by the hosting environment. Browser ownership keys are stored in `/home/data-protection-keys` with App Service persistent storage enabled. Keep these keys when restarting or updating the application.

## Before running

Install Azure CLI and sign in to the intended subscription. Choose a short lowercase prefix containing letters and digits, for example `logiccircuit26`. Use a new resource group for this project. You do not need to upload the source to GitHub.

```powershell
az login
az account list --output table
az account set --subscription "YOUR-SUBSCRIPTION-ID"
az account show --output table
```

From the project root:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
$databasePassword = Read-Host "Azure SQL administrator password" -AsSecureString
.\scripts\Deploy-Azure.ps1 -Prefix logiccircuit26 -ResourceGroup logic-circuit-rg -Location westeurope -SqlPassword $databasePassword
```

Use a strong SQL password that meets Azure SQL policy. Do not put it into a source file or chat message. The script supplies a temporary parameter file to Azure and removes it in `finally`.

## What the script does

1. Confirms that Azure CLI is signed in.
2. Creates the selected resource group and deploys the Bicep infrastructure.
3. Builds the container in ACR from the local project directory.
4. Runs an AND design through `read_slang` during image construction, so an unusable Yosys image fails the build.
5. Restarts the web app and waits for `/api/health/database`.
6. Prints the application URL only after that health check succeeds.

The database migrations run on application startup. The source includes `InitialCreate` and `AddCommunitySharing`. For a separate migration operation, the API also accepts `--migrate-only=true`. SQLite preview uses `EnsureCreated` on a fresh preview file; it does not execute SQL Server migrations.

## Verify the deployed application

Use the real URL printed by the script. A successful template deployment alone does not establish that the application works.

| Check | Expected result |
| --- | --- |
| `/api/health` | JSON with `status: ok` |
| `/api/health/database` | JSON with `status: ok` |
| Import `examples/full_adder.sv` | Three switches, two lamps, five logic gates and twelve wires |
| Generate its truth table | Eight rows with correct sum and carry |
| Save, reload the page, load | The same circuit returns |
| Restart the web app, load again | Saved data and browser ownership remain |
| Publish, open its link in another browser | Public card is visible; Open copy produces a working circuit |
| Edit the opened copy | The original publication stays unchanged |
| Unpublish as its owner | Publication disappears while an already opened copy remains |
| Physical phone in portrait and landscape | Panels fit, gates move, wires connect, switches toggle and HDL file selection works |

Download a circuit JSON backup before testing updates. Do not mark the public deployment complete until these checks pass on the hosted SQL provider and a physical phone.

## Logs and updates

```powershell
az webapp log config --name YOUR-APP-NAME --resource-group logic-circuit-rg --docker-container-logging filesystem
az webapp log tail --name YOUR-APP-NAME --resource-group logic-circuit-rg
```

To update application code, rerun the script with the same prefix and resource group. Keep the SQL administrator credentials consistent. The script rebuilds the image tagged `final`, then restarts the app. Do not delete the database or key folder during an ordinary update.

After a successful hosted test, publish the reviewed source to the team's GitHub repository. Keep cloud credentials and locally downloaded tools out of that commit.

## Deployment status of this package

The Bicep template compiles and the PowerShell scripts parse. A Release publication was built. The actual Azure resources, Azure SQL runtime, container image build in ACR and physical-phone test still require a signed-in Azure subscription. The editing environment had no Docker engine; its persistence tests used a real SQLite file, not a claim of Azure SQL execution.

## References

- [Azure App Service custom containers](https://learn.microsoft.com/azure/app-service/configure-custom-container)
- [ASP.NET Core with Azure SQL](https://learn.microsoft.com/azure/app-service/tutorial-dotnetcore-sqldb-app)
- [ASP.NET Core data protection in containers](https://learn.microsoft.com/aspnet/core/security/data-protection/configuration/overview)
- [OSS CAD Suite releases](https://github.com/YosysHQ/oss-cad-suite-build/releases)
