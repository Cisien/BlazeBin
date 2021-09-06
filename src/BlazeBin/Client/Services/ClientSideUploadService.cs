
using BlazeBin.Shared;

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BlazeBin.Client.Services;
public class ClientSideUploadService : IUploadService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _serializerOpts;

    public ClientSideUploadService(HttpClient http)
    {
        _http = http;
        _serializerOpts = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    public async Task<Result<FileBundle>> Get(string serverId)
    {
        var response = await _http.GetAsync($"raw/{serverId}");
        if (!response.IsSuccessStatusCode)
        {
            return Result<FileBundle>.FromError($"Server responded with {response.StatusCode}");
        }

        try
        {
            var fromApi = await response.Content.ReadFromJsonAsync<FileBundle>();
            if (fromApi == null)
            {
                return Result<FileBundle>.FromError($"Server returned unexpected data");
            }

            if (fromApi.Files.Count == 0)
            {
                return Result<FileBundle>.FromError("Server data contained no files");
            }

            fromApi.LastServerId = serverId;
            return Result<FileBundle>.FromSuccess(fromApi);
        }
        catch (JsonException)
        {
            return Result<FileBundle>.FromError($"Server returned unexpected data");
        }
    }

    public async Task<Result<string>> Set(FileBundle item)
    {
        var body = new MultipartFormDataContent();
        var contentJson = JsonSerializer.Serialize(item, _serializerOpts);

        if (string.IsNullOrWhiteSpace(contentJson))
        {
            return Result<string>.FromError("No data to upload");
        }

        body.Add(new StringContent(contentJson, Encoding.UTF8, "application/json"), "file", item.Id);
       
        var response = await _http.PostAsync("submit", body);
        if (!response.IsSuccessStatusCode)
        {
            var result = response.StatusCode switch
            {
                HttpStatusCode.BadRequest => await BuildBadRequestResult(response),
                _ => Result<string>.FromError($"Server responded with { response.StatusCode }")
            };
            return result;
        }

        var content = await response.Content.ReadFromJsonAsync<FileData>();
        if (content == null)
        {
            return Result<string>.FromError($"Server response was unexpected");
        }

        return Result<string>.FromSuccess(content.Id);
    }

    private static async Task<Result<string>> BuildBadRequestResult(HttpResponseMessage response)
    {
        var content = await response.Content.ReadFromJsonAsync<ApiError>();

        if((content?.Errors.Keys.Count ?? 0) == 0)
        {
            return Result<string>.FromError($"Server responded with { response.StatusCode }");
        }
        var errors = content!.Errors.SelectMany(a => a.Value);

        return Result<string>.FromError($"One or more errors occurred with your request: \n{string.Join("\n", errors)}");
    }

    public void SetAntiforgeryToken(string? token)
    {
        _http.DefaultRequestHeaders.TryAddWithoutValidation("X-XSRF-TOKEN", token);
    }

    private record ApiError(string Type, string Title, int StatusCode, string TraceId, Dictionary<string, List<string>> Errors);
}
