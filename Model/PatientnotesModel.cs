using Dapper.Contrib.Extensions;
using Microsoft.AspNetCore.Http;

namespace Medico_Backend.Model
{
    [Table("patient_voice_notes")]
    public class PatientVoiceNoteModel
    {
        [Key]
        public long voice_note_id { get; set; }

        public int dcode { get; set; }
        public int custid { get; set; }
        public string? tenant_code { get; set; }

        public string? text_message { get; set; }

        public string? audio_file_name { get; set; }
        public string? audio_file_path { get; set; }
        public string? audio_content_type { get; set; }
        public long? audio_size { get; set; }

        public Guid? upload_id { get; set; }

        public DateTime entereddate { get; set; }
        public bool deleted { get; set; } = false;
    }

    // Not a DB table via Dapper.Contrib — managed with plain SQL in the repo class
    public class AudioUploadSessionModel
    {
        public Guid upload_id { get; set; }

        public int dcode { get; set; }
        public int custid { get; set; }
        public string? tenant_code { get; set; }

        public string? text_message { get; set; }

        public string? original_file_name { get; set; }
        public string? content_type { get; set; }

        public int total_chunks { get; set; }
        public int uploaded_chunks { get; set; }

        public string status { get; set; } = "UPLOADING";

        public string? temp_path { get; set; }
        public string? final_path { get; set; }

        public DateTime created_date { get; set; }
        public DateTime? completed_date { get; set; }
    }

    // What the single upload endpoint receives on every chunk request
    public class AudioChunkUploadRequest
    {
        public Guid? UploadId { get; set; }        // null on chunk 1, required after
        public int ChunkNumber { get; set; }
        public int TotalChunks { get; set; }

        // Only required on chunk 1
        public int? Dcode { get; set; }
        public int? CustId { get; set; }
        public string? TextMessage { get; set; }
        public string? FileName { get; set; }
        public string? ContentType { get; set; }

        public IFormFile File { get; set; } = null!;
    }

    public class AudioChunkUploadResponse
    {
        public Guid UploadId { get; set; }
        public bool Completed { get; set; }
        public int UploadedChunks { get; set; }
        public int TotalChunks { get; set; }
        public long? VoiceNoteId { get; set; }
        public string Message { get; set; } = "";
    }

    public class AudioUrlResponse
    {
        public string? Url { get; set; }
        public int ExpiresInSeconds { get; set; }
    }
}