using BlazeBin.Server.Services;
using BlazeBin.Shared;
using BlazeBin.Shared.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlazeBin.Server.Controllers;

[ApiController]
[Route("")]
public class FilesController : ControllerBase
{
    private readonly ILogger<FilesController> _logger;
    private readonly IStorageService _fileService;
    private readonly IKeyGeneratorService _keygen;
    private readonly UTF8Encoding _encoder;
    private readonly BlazeBinConfiguration _config;

    private static readonly string _getRouteTemplate;

    public FilesController(ILogger<FilesController> logger, IStorageService fileService, IKeyGeneratorService keygen, BlazeBinConfiguration config)
    {
        _logger = logger;
        _fileService = fileService;
        _keygen = keygen;
        _encoder = new UTF8Encoding(false, true);
        _config = config;

    }

    [HttpPost("submit")]
    [RequestFormLimits(MultipartBodyLengthLimit = 409_600)]
    [RequestSizeLimit(409_600)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostData(IFormFile file)
    {
        string? submitData = await GetUtf8Contents(file);

        if (string.IsNullOrWhiteSpace(submitData))
        {
            return BadRequest(new { Error = "The file contained byte sequences that are not valid Utf-8." });
        }

        var (location, result) = await WriteData(file.FileName, submitData);
        return Created(location, result);
    }

    [HttpPost("submit/basic")]
    [RequestFormLimits(MultipartBodyLengthLimit = 409_600)]
    [RequestSizeLimit(409_600)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BasicSubmit([FromForm] string file)
    {
        var bundleId = _keygen.GenerateKey(12).ToString();
        var fileId = _keygen.GenerateKey(12).ToString();
        FileBundle bundle = new(bundleId, new List<FileData>());
        var lang = CrudeLanguageDetection(file);
        bundle.Files.Add(new(fileId, $"basic-post.{lang}", file));

        var serialized = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var (_, data) = await WriteData("hastebin-post", serialized);

        var getUrl = new UriBuilder(Request.Scheme, Request.Host.Host, Request.Host.Port.GetValueOrDefault(Request.IsHttps ? 443 : 80), $"basic/viewer/{data.Id}/0").Uri.ToString();

        return Redirect(getUrl);
    }

    [HttpGet("raw/{filename}")]
    [ResponseCache(Location = ResponseCacheLocation.Any, Duration = 2_592_000 /*30 days*/)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GetFileAsync(string filename)
    {
        var data = await _fileService.ReadDataAsync(filename);

        if (data?.Data == null)
        {
            return NotFound();
        }

        Response.Headers.ContentDisposition = $"attachment; filename=\"{data.Filename}\"";
        return Content(data.Data, "application/json", Encoding.UTF8);
    }

    // to maintain compatibility with the hastebin api in use on paste.mod.gg
    [HttpPost("documents")]
    [RequestSizeLimit(4_096_000)]
    [Consumes("text/plain")]
    public async Task<IActionResult> HastebinFilePost()
    {
        if (!_config.HasteShim.Enabled)
        {
            return BadRequest(new { Error = "This feature is disabled" });
        }

        string? callerIp;
        if (HttpContext.Connection.RemoteIpAddress != null)
        {
            callerIp = HttpContext.Connection.RemoteIpAddress.ToString();
        }
        else if (!Request.Headers.ContainsKey("client-ip"))
        {
            callerIp = Request.Headers["client-ip"];
        }
        else
        {
            _logger.LogWarning("Unable to determine the caller ip for hastebin shim request");
            return BadRequest();
        }

        if (!_config.HasteShim.AllowedClientIps.Contains(callerIp ?? string.Empty))
        {
            _logger.LogWarning("{ip} is not in the allowed list of ip addresses for hastebin shim request", callerIp);
            return BadRequest();
        }

        var bodyResult = await Request.BodyReader.ReadAsync();
        if (bodyResult.Buffer.Length == 0)
        {
            _logger.LogWarning("body empty for hastebin shim request");
            return BadRequest();
        }

        var body = _encoder.GetString(bodyResult.Buffer);

        var bundleId = _keygen.GenerateKey(12).ToString();
        var fileId = _keygen.GenerateKey(12).ToString();
        FileBundle bundle = new(bundleId, new List<FileData>());
        var lang = CrudeLanguageDetection(body);
        bundle.Files.Add(new(fileId, $"hastebin-post.{lang}", body));

        var serialized = JsonSerializer.Serialize(bundle, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var (_, data) = await WriteData("hastebin-post", serialized);

        return Ok(new { Key = $"{data.Id}/0" });
    }

    private string CrudeLanguageDetection(ReadOnlySpan<char> code)
    {
        var partial = code.Trim()[0..10];

        if (partial.StartsWith("using"))
        {
            return "cs";
        }
        if (partial[0] == '{' || partial[0] == '[')
        {
            return "json";
        }
        if (partial[0] == '@')
        {
            return "cshtml";
        }
        if (partial.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase))
        {
            return "html";
        }
        if (partial[0] == '<')
        {
            return "xml";
        }
        return "txt";
    }

    private async Task<(Uri location, FileData result)> WriteData(string filename, string submitData)
    {
        var key = _keygen.GenerateKey(12).ToString();
        var data = new FileData(key, filename, submitData);
        data = await _fileService.WriteDataAsync(data);

        var filePath = BuildGetUriFromAction(data);
        return (filePath, data);
    }

    private Uri BuildGetUriFromAction(FileData data)
    {
        var template = _getRouteTemplate.Replace("{filename}", data.Id);
        var getUrl = new UriBuilder(Request.Scheme, Request.Host.Host, Request.Host.Port.GetValueOrDefault(Request.IsHttps ? 443 : 80), template);

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
