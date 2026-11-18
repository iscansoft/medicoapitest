using Medico_Backend.Class;
using Medico_Backend.Model;
using medico_backend.Services;

namespace Medico_Backend.Services
{
    public class AudioUploadService
    {
        private readonly PatientVoiceNoteClass _repo;
        private readonly S3ImageService _s3;
        private readonly ILogger<AudioUploadService> _logger;

        private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "audio-temp");

        public AudioUploadService(
            PatientVoiceNoteClass repo,
            S3ImageService s3,
            ILogger<AudioUploadService> logger)
        {
            _repo = repo;
            _s3 = s3;
            _logger = logger;
        }

        public async Task<AudioChunkUploadResponse> UploadChunkAsync(
            AudioChunkUploadRequest req, string tenant_code)
        {
            if (req.File == null || req.File.Length == 0)
                throw new ArgumentException("File chunk is empty.");

            if (req.ChunkNumber < 1 || req.TotalChunks < 1 || req.ChunkNumber > req.TotalChunks)
                throw new ArgumentException("Invalid chunkNumber/totalChunks.");

            // ── Step 1: resolve or create the upload session ──
            Guid uploadId;
            AudioUploadSessionModel? session;

            if (req.UploadId == null)
            {
                if (req.ChunkNumber != 1)
                    throw new InvalidOperationException("First request must be chunk 1 with no UploadId, or include an existing UploadId.");

                uploadId = Guid.NewGuid();
                var tempPath = Path.Combine(_tempRoot, tenant_code, uploadId.ToString());
                Directory.CreateDirectory(tempPath);

                await _repo.CreateSession(new AudioUploadSessionModel
                {
                    upload_id = uploadId,
                    dcode = req.Dcode ?? throw new ArgumentException("Dcode required on first chunk."),
                    custid = req.CustId ?? throw new ArgumentException("CustId required on first chunk."),
                    tenant_code = tenant_code,
                    text_message = req.TextMessage,
                    original_file_name = req.FileName ?? "recording.webm",
                    content_type = req.ContentType ?? "audio/webm",
                    total_chunks = req.TotalChunks,
                    temp_path = tempPath
                });

                session = await _repo.GetSession(uploadId);
            }
            else
            {
                uploadId = req.UploadId.Value;
                session = await _repo.GetSession(uploadId);

                if (session == null)
                    throw new InvalidOperationException($"Upload session {uploadId} not found.");

                if (session.status == "COMPLETED")
                {
                    // Already finished — idempotent response for a late/duplicate request
                    return new AudioChunkUploadResponse
                    {
                        UploadId = uploadId,
                        Completed = true,
                        UploadedChunks = session.total_chunks,
                        TotalChunks = session.total_chunks,
                        Message = "Upload already completed"
                    };
                }
            }

            // ── Step 2: save this chunk to disk (streamed, never buffered in RAM) ──
            var chunkFile = Path.Combine(session!.temp_path!, $"chunk_{req.ChunkNumber:D6}");

            if (!File.Exists(chunkFile))
            {
                var tmpChunkFile = chunkFile + ".part";
                using (var fs = new FileStream(tmpChunkFile, FileMode.Create))
                {
                    await req.File.CopyToAsync(fs);
                }
                File.Move(tmpChunkFile, chunkFile, overwrite: true);

                await _repo.IncrementUploadedChunks(uploadId);
                session = await _repo.GetSession(uploadId);
            }

            // ── Step 3: not all chunks in yet -> just ack ──
            if (session!.uploaded_chunks < session.total_chunks)
            {
                return new AudioChunkUploadResponse
                {
                    UploadId = uploadId,
                    Completed = false,
                    UploadedChunks = session.uploaded_chunks,
                    TotalChunks = session.total_chunks,
                    Message = "Chunk received"
                };
            }

            // ── Step 4: final chunk arrived -> combine, upload to S3, save DB record ──
            try
            {
                var combinedPath = Path.Combine(session.temp_path!, "combined_" + session.original_file_name);

                await using (var output = new FileStream(combinedPath, FileMode.Create))
                {
                    for (int i = 1; i <= session.total_chunks; i++)
                    {
                        var part = Path.Combine(session.temp_path!, $"chunk_{i:D6}");
                        if (!File.Exists(part))
                            throw new InvalidOperationException($"Missing chunk {i} for upload {uploadId}.");

                        await using var input = new FileStream(part, FileMode.Open);
                        await input.CopyToAsync(output); // streamed copy, RAM stays flat
                    }
                }

                var objectKey = _s3.BuildKey(
                    tenant_code,
                    "voicenotes",
                    session.custid,
                    uploadId.ToString("N").Substring(0, 8),
                    session.original_file_name ?? "recording.webm");

                string finalPath;
                await using (var uploadStream = new FileStream(combinedPath, FileMode.Open))
                {
                    finalPath = await _s3.UploadStreamAsync(uploadStream, objectKey, session.content_type);
                }

                var audioSize = new FileInfo(combinedPath).Length;

                await _repo.CompleteSession(uploadId, finalPath);

                var voiceNote = new PatientVoiceNoteModel
                {
                    dcode = session.dcode,
                    custid = session.custid,
                    tenant_code = tenant_code,
                    text_message = session.text_message,
                    audio_file_name = session.original_file_name,
                    audio_file_path = finalPath,
                    audio_content_type = session.content_type,
                    audio_size = audioSize,
                    upload_id = uploadId
                };

                var voiceNoteId = await _repo.InsertVoiceNote(voiceNote);

                // ── Step 5: cleanup temp chunks ──
                try
                {
                    Directory.Delete(session.temp_path!, recursive: true);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Temp cleanup failed for {UploadId}", uploadId);
                }

                return new AudioChunkUploadResponse
                {
                    UploadId = uploadId,
                    Completed = true,
                    UploadedChunks = session.total_chunks,
                    TotalChunks = session.total_chunks,
                    VoiceNoteId = voiceNoteId,
                    Message = "Audio uploaded successfully"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to finalize upload {UploadId}", uploadId);
                await _repo.FailSession(uploadId, ex.Message);
                throw;
            }
        }

        // For resume support
        public async Task<object> GetStatusAsync(Guid uploadId)
        {
            var session = await _repo.GetSession(uploadId)
                ?? throw new InvalidOperationException("Upload session not found.");

            var uploaded = await _repo.GetUploadedChunkNumbers(session.temp_path!, session.total_chunks);
            var nextChunk = Enumerable.Range(1, session.total_chunks).Except(uploaded).FirstOrDefault();

            return new
            {
                uploadId,
                totalChunks = session.total_chunks,
                uploadedChunks = uploaded,
                nextChunk = nextChunk == 0 ? (int?)null : nextChunk,
                status = session.status
            };
        }
    }
}