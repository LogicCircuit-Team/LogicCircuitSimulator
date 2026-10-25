using CircuitApi.Contracts;
using CircuitApi.Data;
using CircuitApi.Models;
using LogicCore.Serialization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
namespace CircuitApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CircuitsController : ControllerBase
    {
        private readonly CircuitDbContext _db;
        // Receives the database context from the application.
        public CircuitsController(CircuitDbContext db)
        {
            _db = db;
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
                // Checks that the JSON can be read as a circuit.
                CircuitSerializer.FromJsonToManager(request.JsonData);
            }

            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException) 
            {
            
                return BadRequest(new { message = ex.Message });
            }


            var now= DateTime.UtcNow;

            // Creates the database record.
            var circuit = new Circuit
            {
                Name = request.Name.Trim(),
                JsonData = request.JsonData,
                CreatedAt = now,
                UpdatedAt = now,
            };

            _db.Circuits.Add(circuit);

            await _db.SaveChangesAsync(cancellationToken);
            return Created($"/api/circuits/{circuit.CircuitId}", circuit);



        }

        // Gets all saved circuits.
        [HttpGet]
        public async Task<ActionResult<List<Circuit>>> GetAll(
            CancellationToken cancellationToken)
        {
            // Reads circuits without tracking database changes.
            var circuits = await _db.Circuits
                .AsNoTracking()
                .OrderByDescending(circuit => circuit.UpdatedAt)
                .ToListAsync(cancellationToken);

            return Ok(circuits);
        }


        [HttpGet("{id:int}")]
        public async Task<ActionResult<Circuit>> GetById(
    int id,
    CancellationToken cancellationToken)
        {
            // Finds the circuit without tracking database changes.
            var circuit = await _db.Circuits
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.CircuitId == id, cancellationToken);

            
            if (circuit is null)
                return NotFound();

          
            return Ok(circuit);
        }



        // Updates an existing circuit.
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
                .FirstOrDefaultAsync(item => item.CircuitId == id, cancellationToken);

            if (circuit is null)
                return NotFound();

            try
            {
                // Checks that the JSON contains valid circuit data.
                CircuitSerializer.FromJsonToManager(request.JsonData);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
            {
                return BadRequest(new { message = ex.Message });
            }

            // Updates the saved circuit information.
            circuit.Name = request.Name.Trim();
            circuit.JsonData = request.JsonData;
            circuit.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }


        // Deletes a circuit by its ID.
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(
            int id,
            CancellationToken cancellationToken)
        {
            var circuit = await _db.Circuits
                .FirstOrDefaultAsync(item => item.CircuitId == id, cancellationToken);

            if (circuit is null)
                return NotFound();

            _db.Circuits.Remove(circuit);
            await _db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

    }
}
