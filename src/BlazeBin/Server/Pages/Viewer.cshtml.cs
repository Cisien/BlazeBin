
using System.Threading.Tasks;

using BlazeBin.Client.Services;
using BlazeBin.Shared;

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlazeBin.Server.Pages;

public class ViewerModel : PageModel
{
    private readonly IUploadService _upload;

    public int FileIndex { get; set; } = 0;
    public string? ServerId { get; set; }
    public FileBundle? Bundle { get; set; }

    public ViewerModel(IUploadService uploadSvc)
    {
        _upload = uploadSvc;
    }

    public async Task OnGet(string serverId, int? fileIndex)
    {
        FileIndex = fileIndex ?? 0;
        ServerId = serverId;

        if (string.IsNullOrWhiteSpace(serverId))
        {
            Response.Redirect("/basic");
            return;
        }

        var bundleResult = await _upload.Get(ServerId);
        if (!bundleResult.Successful)
        {
            Response.Redirect("/basic");
            return;
        }

        if (FileIndex > bundleResult.Value.Files.Count-1)
        {
            Response.Redirect("/basic");
            return;
        }

        Bundle = bundleResult.Value;
    }
}

