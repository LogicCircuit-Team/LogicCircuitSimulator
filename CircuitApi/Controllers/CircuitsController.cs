using CircuitApi.Contracts;
using CircuitApi.Data;
using CircuitApi.Models;
using LogicCore.Serialization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
namespace CircuitApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    [RequestSizeLimit(2000000)]
    public class CircuitsController : ControllerBase
    {
        private readonly CircuitDbContext _db;
        private readonly IWebHostEnvironment _environment;

        public CircuitsController(CircuitDbContext db, IWebHostEnvironment environment)
        {
            _db = db;
            _environment = environment;
        }

        [HttpPost]
        public async Task<ActionResult<Circuit>> Create
            (
            [FromBody] SaveCircuitRequest request,
            CancellationToken cancellationToken)
        {

            if (request == null)
                return BadRequest("The request is missing.");

            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Circuit name is required.");

            if (request.Name.Trim().Length>100)
                return BadRequest("Circuit name cannot exceed 100 characters.");
            if (string.IsNullOrWhiteSpace(request.JsonData))
                return BadRequest("Circuit JSON is required.");
            try
            {

                CircuitSerializer.FromJsonToManager(request.JsonData);
            }

            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
            {

                return BadRequest(new { message = ex.Message });
            }

            var now= DateTime.UtcNow;

            var circuit = new Circuit
            {
                Name = request.Name.Trim(),
                OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                JsonData = request.JsonData,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _db.Circuits.Add(circuit);

            await _db.SaveChangesAsync(cancellationToken);
            return Created($"/api/circuits/{circuit.CircuitId}", circuit);

        }

        [HttpGet]
        public async Task<ActionResult<List<Circuit>>> GetAll(
            CancellationToken cancellationToken)
        {

            var circuits = await _db.Circuits
                .AsNoTracking()
                .Where(circuit => circuit.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier)
                    || (_environment.IsDevelopment() && circuit.OwnerId == null))
                .OrderByDescending(circuit => circuit.UpdatedAt)
                .Select(circuit => new Circuit
                {
                    CircuitId = circuit.CircuitId,
                    Name = circuit.Name,
                    CreatedAt = circuit.CreatedAt,
                    UpdatedAt = circuit.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return Ok(circuits);
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<Circuit>> GetById(
    int id,
    CancellationToken cancellationToken)
        {

            var circuit = await _db.Circuits
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.CircuitId == id
                    && (item.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier)
                        || (_environment.IsDevelopment() && item.OwnerId == null)), cancellationToken);

            if (circuit is null)
                return NotFound();

            return Ok(circuit);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] SaveCircuitRequest request,
            CancellationToken cancellationToken)
        {
            if (request is null || string.IsNullOrWhiteSpace(request.Name))
                return BadRequest("Circuit name is required.");

            if (request.Name.Trim().Length > 100)
                return BadRequest("Circuit name cannot exceed 100 characters.");

            if (string.IsNullOrWhiteSpace(request.JsonData))
                return BadRequest("Circuit JSON is required.");

            var circuit = await _db.Circuits
                .FirstOrDefaultAsync(item => item.CircuitId == id
                    && (item.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier)
                        || (_environment.IsDevelopment() && item.OwnerId == null)), cancellationToken);

            if (circuit is null)
                return NotFound();

            try
            {

                CircuitSerializer.FromJsonToManager(request.JsonData);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
            {
                return BadRequest(new { message = ex.Message });
            }

            circuit.Name = request.Name.Trim();
            circuit.OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            circuit.JsonData = request.JsonData;
            circuit.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(
            int id,
            CancellationToken cancellationToken)
        {
            var circuit = await _db.Circuits
                .FirstOrDefaultAsync(item => item.CircuitId == id
                    && (item.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier)
                        || (_environment.IsDevelopment() && item.OwnerId == null)), cancellationToken);

            if (circuit is null)
                return NotFound();

            _db.Circuits.Remove(circuit);
            await _db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

    }
}
