using System.Threading.Tasks;
using Asynkron.OtelReceiver.Data;
using Grpc.Core;
using Tracelens.Proto.V1;

namespace Asynkron.OtelReceiver.Services;

/// <summary>
/// Exposes TraceLens search and metadata operations over gRPC so external tools can query stored telemetry.
/// </summary>
public class DataServiceImpl(ModelRepo modelRepo) : DataService.DataServiceBase
{
    private readonly ModelRepo _modelRepo = modelRepo;

    public override Task<GetSearchDataResponse> GetSearchData(GetSearchDataRequest request, ServerCallContext context)
        => _modelRepo.GetSearchData(request);

    public override Task<GetValuesForTagResponse> GetValuesForTag(GetValuesForTagRequest request, ServerCallContext context)
        => _modelRepo.GetValuesForTag(request);

    public override Task<SearchTracesResponse> SearchTraces(SearchTracesRequest request, ServerCallContext context)
        => _modelRepo.SearchTraces(request);

    public override Task<GetServiceMapComponentsResponse> GetServiceMapComponents(GetServiceMapComponentsRequest request, ServerCallContext context)
        => _modelRepo.GetServiceMapComponents(request);

    public override Task<GetComponentMetadataResponse> GetComponentMetadata(GetComponentMetadataRequest request, ServerCallContext context)
        => _modelRepo.GetComponentMetadata(request);

    public override Task<SetComponentMetadataResponse> SetComponentMetadata(SetComponentMetadataRequest request, ServerCallContext context)
        => _modelRepo.SetComponentMetadata(request);

    public override Task<GetMetadataForComponentResponse> GetMetadataForComponent(GetMetadataForComponentRequest request, ServerCallContext context)
        => _modelRepo.GetMetadataForComponent(request);

    public override Task<GetSnapshotResponse> GetSnapshot(GetSnapshotRequest request, ServerCallContext context)
        => _modelRepo.GetSnapshot(request);

    public override Task<GetMetricNamesResponse> GetMetricNames(GetMetricNamesRequest request, ServerCallContext context)
        => _modelRepo.GetMetricNames(request);

    public override Task<GetMetricResponse> GetMetric(GetMetricRequest request, ServerCallContext context)
        => _modelRepo.GetMetric(request);
}
