using Medico_Backend.Class;
using Medico_Backend.Model;
using Medico_Backend.Services;
using medico_backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Medico_Backend.Controllers
{
    [Route("api/patient-voice-notes")]
    [ApiController]
    public class PatientVoiceNotesController : ControllerBase
    {
        private readonly AudioUploadService _service;
        private readonly PatientVoiceNoteClass _repo;
        private readonly S3ImageService _s3;

        public PatientVoiceNotesController(
            AudioUploadService service,
            PatientVoiceNoteClass repo,
            S3ImageService s3)
        {
            _service = service;
            _repo = repo;
            _s3 = s3;
        }

        // ─── Single upload endpoint — every chunk posts here ───
        [HttpPost("upload")]
        [RequestSizeLimit(20_000_000)] // ~20MB ceiling, above your ~5-10MB chunk size
        public async Task<IActionResult> Upload([FromForm] AudioChunkUploadRequest request)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            try
            {
                var result = await _service.UploadChunkAsync(request, tenant);
                return Ok(result);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new { message = ex.Message });
            }
        }

        // ─── Resume support ───
        [HttpGet("upload/{uploadId}/status")]
        public async Task<IActionResult> GetStatus(Guid uploadId)
        {
            try
            {
                var result = await _service.GetStatusAsync(uploadId);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        // ─── Get one voice note's metadata ───
        [HttpGet("{voiceNoteId}")]
        public async Task<IActionResult> Get(long voiceNoteId)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var note = await _repo.GetById(voiceNoteId, tenant);

            if (note == null) return NotFound();
            return Ok(note);
        }

        // ─── Get a temporary playback URL (no audio bytes go through the API) ───
        [HttpGet("{voiceNoteId}/audio-url")]
        public async Task<IActionResult> GetAudioUrl(long voiceNoteId)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            var note = await _repo.GetById(voiceNoteId, tenant);

            if (note == null) return NotFound();

            var url = await _s3.GetPresignedUrlAsync(note.audio_file_path);
            if (url == null) return NotFound(new { message = "Audio file not found in storage." });

            return Ok(new AudioUrlResponse { Url = url, ExpiresInSeconds = 3600 });
        }

        // ─── Get all voice notes for a patient ───
        [HttpGet("by-cust")]
        public async Task<IActionResult> GetByCust(int custid)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _repo.GetByCust(custid, tenant));
        }

        // ─── Get all voice notes for a doctor ───
        [HttpGet("by-doctor")]
        public async Task<IActionResult> GetByDoctor(int dcode)
        {
            var tenant = Request.Headers["tenant_code"].ToString();
            return Ok(await _repo.GetByDoctor(dcode, tenant));
        }

        // ─── Soft delete ───
        [HttpGet("delete")]
        public async Task<IActionResult> Delete(long voiceNoteId)
        {
            var tenant = Request.Headers["tenant_code"].ToString();

            var existing = await _repo.GetById(voiceNoteId, tenant);
            var res = await _repo.Delete(voiceNoteId, tenant);

            if (res != "Success") return NotFound(new { message = res });

            if (existing != null)
                await _s3.DeleteAsync(existing.audio_file_path);

            return Ok(new { message = "Voice note deleted successfully" });
        }
    }
}