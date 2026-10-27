using CircuitApi.Contracts;
using System.Threading;
using System.Threading.Tasks;

namespace CircuitApi.Services
{
    public interface IHdlSynthesisService
    {
        Task<HdlSynthesisResponse> SynthesizeAsync(
            HdlSynthesisRequest request,
            CancellationToken cancellationToken);
    }
}
