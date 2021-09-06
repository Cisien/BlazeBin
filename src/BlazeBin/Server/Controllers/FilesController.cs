using BlazeBin.Server.Services;
using BlazeBin.Shared;
using BlazeBin.Shared.Services;

using Microsoft.AspNetCore.Mvc;

using System.Buffers;
using System.Text;

namespace BlazeBin.Server.Controllers;

[ApiController]
[Route("")]
public class FilesController : ControllerBase
{
    private readonly ILogger<FilesController> _logger;
    private readonly IStorageService _fileService;
    private readonly IKeyGeneratorService _keygen;
    private readonly UTF8Encoding _encoder;
    private static readonly string _getRouteTemplate;

    public FilesController(ILogger<FilesController> logger, IStorageService fileService, IKeyGeneratorService keygen)
    {
        _logger = logger;
        _fileService = fileService;
        _keygen = keygen;
        _encoder = new UTF8Encoding(false, true);
    }

    [HttpPost("submit")]
    [RequestFormLimits(MultipartBodyLengthLimit = 409_600)]
    [RequestSizeLimit(409_600)]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> PostData(IFormFile file)
    {
        // buffers the contents in memory, but the RequestFormLimitsAttribute is caping that at 4mb
        string? submitData = await GetUtf8Contents(file);

        if (string.IsNullOrWhiteSpace(submitData))
        {
            return BadRequest(new { Error = "The file contained byte sequences that are not valid Utf-8." });
        }

        var key = _keygen.GenerateKey(12).ToString();
        var data = new FileData(key, file.FileName, submitData);
        data = await _fileService.WriteDataAsync(data);

        var filePath = BuildGetUriFromAction(data);
        return Created(filePath, data);
    }

    [HttpGet("raw/{filename}")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult> GetFileAsync(string filename)
    {
        var data = await _fileService.ReadDataAsync(filename);

        if (data?.Data == null)
        {
            return NotFound();
        }

        Response.Headers.Add("Content-Disposition", $"attachment; filename=\"{data.Filename}\"");
        return Content(data.Data, "application/json", Encoding.UTF8);
    }

    private Uri BuildGetUriFromAction(FileData data)
    {
        var template = _getRouteTemplate.Replace("{filename}", data.Id);
        var getUrl = new UriBuilder(Request.Scheme, Request.Host.Host, Request.Host.Port.GetValueOrDefault(Request.Scheme == "https" ? 443 : 80), template);

        return getUrl.Uri;
    }

    private async Task<string?> GetUtf8Contents(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        var bufferSize = (int)stream.Length;
        var backingBuffer = ArrayPool<byte>.Shared.Rent(bufferSize);

        var buffer = new Memory<byte>(backingBuffer, 0, bufferSize);
        var read = await stream.ReadAsync(buffer);

        try
        {
            return _encoder.GetString(buffer.Span);
        }
        catch (ArgumentException ae)
        {
            _logger.LogCritical(ae, "Unable to read contents");
            // Horray for logic via exception handling!
            // The encoder signals that the data passed to it is invalid utf8 by throwing an argument exception
            return null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(backingBuffer);
        }
    }

    static FilesController()
    {
        var actionAttributes = typeof(FilesController)
            .GetMethod(nameof(GetFileAsync))?
            .GetCustomAttributes(typeof(HttpGetAttribute), false)
            .Cast<HttpGetAttribute>();

        var actionTemplate = actionAttributes?.Single(a => a.Template != null).Template!;

        var controllerAttributes = typeof(FilesController)
            .GetCustomAttributes(typeof(RouteAttribute), false)
            .Cast<RouteAttribute>();
        var routeTemplate = controllerAttributes.SingleOrDefault(a => !string.IsNullOrWhiteSpace(a.Template))?.Template;

        _getRouteTemplate = routeTemplate == null ? actionTemplate : $"{routeTemplate}/{actionTemplate}";
    }
}
