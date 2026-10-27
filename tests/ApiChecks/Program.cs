using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

string baseUrl = args.Length > 0 ? args[0].TrimEnd('/') + "/" : "http://localhost:5210/";
string root = AppContext.BaseDirectory;
while (!File.Exists(Path.Combine(root, "LogiclyInspiredSimulator.slnx")))
    root = Directory.GetParent(root)?.FullName ?? throw new Exception("Run the checks from the project checkout.");
string jsonData = File.ReadAllText(Path.Combine(root, "examples/full_adder.circuit.json"));
string source = File.ReadAllText(Path.Combine(root, "examples/full_adder.sv"));
int checks = 0;
void Check(string name, bool condition)
{
    if (!condition) throw new Exception(name);
    checks++;
    Console.WriteLine("PASS " + name);
}
HttpClient NewClient() => new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer() })
    { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(50) };
using var alice = NewClient();
using var bob = NewClient();
using var guest = NewClient();
int savedId = 0;
int copyId = 0;
int sharedId = 0;
string name = "Full Adder " + Guid.NewGuid().ToString("N")[..8];
try
{
    using var health = await alice.GetAsync("api/health/database");
    Check("Database health", health.IsSuccessStatusCode);
    using var anonymousSave = await guest.PostAsJsonAsync("api/circuits", new { Name = name, JsonData = jsonData });
    Check("Private writes require a browser session", anonymousSave.StatusCode == HttpStatusCode.Unauthorized);
    foreach (var client in new[] { alice, bob })
    {
        using var session = await client.GetAsync("api/session");
        Check("Browser session", session.IsSuccessStatusCode);
    }
    using var saved = await alice.PostAsJsonAsync("api/circuits", new { Name = "  " + name + "  ", JsonData = jsonData });
    Check("Save circuit", saved.StatusCode == HttpStatusCode.Created);
    var record = (await saved.Content.ReadFromJsonAsync<JsonObject>())!;
    savedId = record["circuitId"]!.GetValue<int>();
    Check("Saved name is trimmed", record["name"]!.GetValue<string>() == name);
    var loaded = await alice.GetFromJsonAsync<JsonObject>($"api/circuits/{savedId}");
    Check("Load preserves the circuit JSON", loaded!["jsonData"]!.GetValue<string>() == jsonData);
    var otherList = await bob.GetFromJsonAsync<JsonArray>("api/circuits");
    Check("Other visitors do not see private circuits", !otherList!.Any(item => item!["circuitId"]!.GetValue<int>() == savedId));
    using var forbiddenRead = await bob.GetAsync($"api/circuits/{savedId}");
    Check("Another visitor cannot open the private circuit", forbiddenRead.StatusCode == HttpStatusCode.NotFound);
    using var forbiddenUpdate = await bob.PutAsJsonAsync($"api/circuits/{savedId}", new { Name = "Changed", JsonData = jsonData });
    Check("Another visitor cannot update the private circuit", forbiddenUpdate.StatusCode == HttpStatusCode.NotFound);
    using var updated = await alice.PutAsJsonAsync($"api/circuits/{savedId}", new { Name = name + " updated", JsonData = jsonData });
    Check("Update circuit", updated.StatusCode == HttpStatusCode.NoContent);
    using var publication = await alice.PostAsJsonAsync("api/community", new
    {
        Name = name, AuthorName = "Project checks", Description = "Full adder integration check.", JsonData = jsonData
    });
    Check("Publish circuit", publication.StatusCode == HttpStatusCode.Created);
    var shared = (await publication.Content.ReadFromJsonAsync<JsonObject>())!;
    sharedId = shared["sharedCircuitId"]!.GetValue<int>();
    Check("Published circuit belongs to its publisher", shared["isMine"]!.GetValue<bool>());
    var publicCircuit = await guest.GetFromJsonAsync<JsonObject>($"api/community/{sharedId}");
    Check("Visitors can read a published copy", publicCircuit!["jsonData"]!.GetValue<string>() == jsonData);
    var search = await guest.GetFromJsonAsync<JsonArray>("api/community?search=" + Uri.EscapeDataString(name));
    Check("Search finds the published circuit", search!.Any(item => item!["sharedCircuitId"]!.GetValue<int>() == sharedId));
    Check("Browse results omit the large circuit payload", search!.All(item => item!["jsonData"]!.GetValue<string>() == ""));
    using var unauthorizedRemoval = await bob.DeleteAsync($"api/community/{sharedId}");
    Check("Another visitor cannot unpublish the circuit", unauthorizedRemoval.StatusCode == HttpStatusCode.NotFound);
    using var copy = await bob.PostAsJsonAsync("api/circuits", new { Name = name + " copy", JsonData = publicCircuit!["jsonData"]!.GetValue<string>() });
    Check("A visitor can save an independent copy", copy.StatusCode == HttpStatusCode.Created);
    copyId = (await copy.Content.ReadFromJsonAsync<JsonObject>())!["circuitId"]!.GetValue<int>();
    var editedJson = JsonNode.Parse(jsonData)!;
    editedJson["gates"]![0]!["x"] = 123.5;
    using var editedCopy = await bob.PutAsJsonAsync($"api/circuits/{copyId}", new { Name = name + " edited copy", JsonData = editedJson.ToJsonString() });
    Check("Edit the independent copy", editedCopy.StatusCode == HttpStatusCode.NoContent);
    var unchanged = await guest.GetFromJsonAsync<JsonObject>($"api/community/{sharedId}");
    Check("Editing the copy leaves the publication unchanged", unchanged!["jsonData"]!.GetValue<string>() == jsonData);
    using var invalid = await alice.PostAsJsonAsync("api/community", new { Name = "Invalid", AuthorName = "Checks", Description = "", JsonData = "{}" });
    Check("Malformed circuit JSON is rejected", invalid.StatusCode == HttpStatusCode.BadRequest);
    using var synthesized = await alice.PostAsJsonAsync("api/hdl/synthesize", new { Source = source, TopModule = "full_adder" });
    Check("Real Yosys synthesis", synthesized.StatusCode == HttpStatusCode.OK);
    var synthesis = (await synthesized.Content.ReadFromJsonAsync<JsonObject>())!;
    Check("Synthesis returns a netlist", !string.IsNullOrWhiteSpace(synthesis["netlistJson"]!.GetValue<string>()));
    using var wrongTop = await alice.PostAsJsonAsync("api/hdl/synthesize", new { Source = source, TopModule = "missing_module" });
    Check("A missing top module returns a synthesis error", wrongTop.StatusCode == HttpStatusCode.UnprocessableEntity);
    using var removed = await alice.DeleteAsync($"api/community/{sharedId}");
    Check("Publisher can unpublish", removed.StatusCode == HttpStatusCode.NoContent);
    sharedId = 0;
    using var deleted = await alice.DeleteAsync($"api/circuits/{savedId}");
    Check("Delete saved circuit", deleted.StatusCode == HttpStatusCode.NoContent);
    savedId = 0;
    Console.WriteLine($"API_CHECKS_PASSED={checks}");
}
finally
{
    if (sharedId > 0) await alice.DeleteAsync($"api/community/{sharedId}");
    if (savedId > 0) await alice.DeleteAsync($"api/circuits/{savedId}");
    if (copyId > 0) await bob.DeleteAsync($"api/circuits/{copyId}");
}
