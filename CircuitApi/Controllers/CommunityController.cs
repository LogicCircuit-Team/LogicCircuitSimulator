using CircuitApi.Contracts;
using CircuitApi.Data;
using CircuitApi.Models;
using LogicCore.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

namespace CircuitApi.Controllers
{
    [ApiController]
    [Route("api/community")]
    public class CommunityController : ControllerBase
    {
        private readonly CircuitDbContext db;

        public CommunityController(CircuitDbContext db)
        {
            this.db = db;
        }

        [HttpGet]
        public async Task<ActionResult<List<SharedCircuitResponse>>> GetAll(
            string? search, int page = 1, CancellationToken cancellationToken = default)
        {
            if (page < 1 || page > 10000 || search?.Length > 100)
                return BadRequest(new { message = "The search or page number is invalid." });
            var query = db.SharedCircuits.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(search))
            {
                string text = search.Trim();
                query = query.Where(circuit => circuit.Name.Contains(text) || circuit.AuthorName.Contains(text));
            }
            string? ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Ok(await query.OrderByDescending(circuit => circuit.PublishedAt)
                .ThenByDescending(circuit => circuit.SharedCircuitId).Skip((page - 1) * 12).Take(12)
                .Select(circuit => new SharedCircuitResponse
                {
                    SharedCircuitId = circuit.SharedCircuitId,
                    Name = circuit.Name,
                    AuthorName = circuit.AuthorName,
                    Description = circuit.Description,
                    PublishedAt = circuit.PublishedAt,
                    IsMine = circuit.OwnerId == ownerId
                }).ToListAsync(cancellationToken));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<SharedCircuitResponse>> GetById(int id, CancellationToken cancellationToken)
        {
            var circuit = await db.SharedCircuits.AsNoTracking()
                .FirstOrDefaultAsync(item => item.SharedCircuitId == id, cancellationToken);
            if (circuit == null)
                return NotFound();
            return Ok(ToResponse(circuit));
        }

        [HttpPost]
        [Authorize]
        [RequestSizeLimit(2000000)]
        public async Task<ActionResult<SharedCircuitResponse>> Publish(
            PublishCircuitRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
                return BadRequest(new { message = "Enter a circuit name of up to 100 characters." });
            if (string.IsNullOrWhiteSpace(request.AuthorName) || request.AuthorName.Trim().Length > 60)
                return BadRequest(new { message = "Enter a display name of up to 60 characters." });
            if (request.Description == null || request.Description.Length > 500)
                return BadRequest(new { message = "The description cannot exceed 500 characters." });
            try
            {
                var manager = CircuitSerializer.FromJsonToManager(request.JsonData);
                if (manager.Gates.Count == 0)
                    return BadRequest(new { message = "Add a circuit before publishing." });
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
            {
                return BadRequest(new { message = ex.Message });
            }
            var circuit = new SharedCircuit
            {
                Name = request.Name.Trim(),
                AuthorName = request.AuthorName.Trim(),
                Description = request.Description.Trim(),
                JsonData = request.JsonData,
                OwnerId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
                PublishedAt = DateTime.UtcNow
            };
            db.SharedCircuits.Add(circuit);
            await db.SaveChangesAsync(cancellationToken);
            return Created($"/api/community/{circuit.SharedCircuitId}", ToResponse(circuit));
        }

        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Unpublish(int id, CancellationToken cancellationToken)
        {
            string? ownerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var circuit = await db.SharedCircuits.FirstOrDefaultAsync(
                item => item.SharedCircuitId == id && item.OwnerId == ownerId, cancellationToken);
            if (circuit == null)
                return NotFound();
            db.SharedCircuits.Remove(circuit);
            await db.SaveChangesAsync(cancellationToken);
            return NoContent();
        }

        private SharedCircuitResponse ToResponse(SharedCircuit circuit)
        {
            return new SharedCircuitResponse
            {
                SharedCircuitId = circuit.SharedCircuitId,
                Name = circuit.Name,
                AuthorName = circuit.AuthorName,
                Description = circuit.Description,
                JsonData = circuit.JsonData,
                PublishedAt = circuit.PublishedAt,
                IsMine = circuit.OwnerId == User.FindFirstValue(ClaimTypes.NameIdentifier)
            };
        }
    }
}
