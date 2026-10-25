using Microsoft.AspNetCore.Mvc;
using CircuitApi.Contracts;
using CircuitApi.Services;

namespace CircuitApi.Controllers
{
    [Route("api/hdl")]
    [ApiController]
    public class HdlSynthesisController : ControllerBase
    {
        private readonly IHdlSynthesisService _synthesisService;

        public HdlSynthesisController(IHdlSynthesisService synthesisService)
        {
            _synthesisService = synthesisService;
        }

        [HttpPost("synthesize")]
        public async Task<ActionResult<HdlSynthesisResponse>> Synthesize(
            [FromBody] HdlSynthesisRequest request,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Source))
            {
                return BadRequest(new HdlSynthesisResponse
                {
                    Diagnostics = "The source code is required."
                });
            }

            if (string.IsNullOrWhiteSpace(request.TopModule))
            {
                return BadRequest(new HdlSynthesisResponse
                {
                    Diagnostics = "The top module name is required."
                });
            }

            var response = await _synthesisService.SynthesizeAsync(
                request, cancellationToken);

            // Returns an error if synthesis did not produce a netlist.
            if (string.IsNullOrWhiteSpace(response.NetlistJson))
            {
                return UnprocessableEntity(response);
            }

            return Ok(response);
        }
    }
}