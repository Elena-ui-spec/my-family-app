using FamilyApp.API.Models;
using FamilyApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Threading.Tasks;

namespace FamilyApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [EnableCors("AllowSpecificOrigin")] // Ensure CORS is applied specifically to this controller
    public class MediaController : ControllerBase
    {
        private readonly MediaService _mediaService;

        public MediaController(MediaService mediaService)
        {
            _mediaService = mediaService;
        }

        // Add preflight request handler for OPTIONS
        [HttpOptions]
        public IActionResult HandlePreflightRequest()
        {
            return Ok();
        }

        [HttpPost("add")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AddMedia([FromForm] string description, [FromForm] string persons, [FromForm] string story, [FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file attached.");

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);

                var media = await _mediaService.AddMediaAsync(
                    description,
                    new List<string>(persons.Split(',')),
                    stream.ToArray(),
                    file.FileName,
                    file.ContentType,
                    story
                );

                return Ok(media);
            }
        }

        [HttpGet("stream/{id}")]
        [Authorize]
        public async Task<IActionResult> StreamMedia(string id)
        {
            // Fetch media by file path (since id is not an ObjectId)
            var media = await _mediaService.GetMediaByIdAsync(id);

            if (media == null)
                return NotFound();

            var fileStream = await _mediaService.GetMediaStreamAsync(media.FilePath);

            if (fileStream == null)
                return NotFound();

            return File(fileStream, media.FileType, enableRangeProcessing: true);
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAllMedia([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 9)
        {
            var mediaList = await _mediaService.GetPaginatedMediaAsync(pageNumber, pageSize);
            return Ok(mediaList);
        }

        [HttpGet("search")]
        [Authorize]
        public async Task<IActionResult> SearchMediaByPerson([FromQuery] string person, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var mediaList = await _mediaService.SearchMediaByPersonAsync(person, pageNumber, pageSize);
            return Ok(mediaList);
        }

        [HttpGet("download/{id}")]
        [Authorize]
        public async Task<IActionResult> DownloadMedia(string id)
        {
            // Fetch media by file path instead of ObjectId
            var media = await _mediaService.GetMediaByIdAsync(id);

            if (media == null)
                return NotFound();

            var fileStream = await _mediaService.GetMediaStreamAsync(media.FilePath);

            if (fileStream == null)
                return NotFound();

            return File(fileStream, media.FileType, Path.GetFileName(media.FilePath));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMedia(string id)
        {
            await _mediaService.DeleteMediaAsync(id);
            return NoContent();
        }
    }
}
