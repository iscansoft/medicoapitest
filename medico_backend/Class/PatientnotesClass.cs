using Dapper;
using Dapper.Contrib.Extensions;
using Npgsql;
using System.Data;
using Medico_Backend.Model;

namespace Medico_Backend.Class
{
    public class PatientVoiceNoteClass
    {
        private readonly string db_conn;

        public PatientVoiceNoteClass(IConfiguration configuration)
        {
            db_conn = configuration.GetConnectionString("conn");
        }

        // ─────────────────────────────────────────
        // UPLOAD SESSION
        // ─────────────────────────────────────────
        public async Task CreateSession(AudioUploadSessionModel s)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"INSERT INTO audio_upload_session
                (upload_id, dcode, custid, tenant_code, text_message,
                 original_file_name, content_type, total_chunks,
                 uploaded_chunks, status, temp_path, created_date)
                VALUES
                (@upload_id, @dcode, @custid, @tenant_code, @text_message,
                 @original_file_name, @content_type, @total_chunks,
                 0, 'UPLOADING', @temp_path, now())
                ON CONFLICT (upload_id) DO NOTHING";

            await db.ExecuteAsync(sql, s);
        }

        public async Task<AudioUploadSessionModel?> GetSession(Guid uploadId)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT * FROM audio_upload_session WHERE upload_id = @uploadId";
            return await db.QueryFirstOrDefaultAsync<AudioUploadSessionModel>(sql, new { uploadId });
        }

        // Atomic increment — safe even if chunks arrive in parallel
        public async Task<int> IncrementUploadedChunks(Guid uploadId)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"UPDATE audio_upload_session
                            SET uploaded_chunks = uploaded_chunks + 1
                            WHERE upload_id = @uploadId
                            RETURNING uploaded_chunks";

            return await db.ExecuteScalarAsync<int>(sql, new { uploadId });
        }

        public async Task CompleteSession(Guid uploadId, string finalPath)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"UPDATE audio_upload_session
                            SET status = 'COMPLETED',
                                final_path = @finalPath,
                                completed_date = now()
                            WHERE upload_id = @uploadId";

            await db.ExecuteAsync(sql, new { uploadId, finalPath });
        }

        public async Task FailSession(Guid uploadId, string reason)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"UPDATE audio_upload_session
                            SET status = 'FAILED'
                            WHERE upload_id = @uploadId";

            await db.ExecuteAsync(sql, new { uploadId });
        }

        // For resume support: GET /upload/{uploadId}/status
        public async Task<List<int>> GetUploadedChunkNumbers(string tempPath, int totalChunks)
        {
            var result = new List<int>();
            if (!Directory.Exists(tempPath)) return result;

            for (int i = 1; i <= totalChunks; i++)
            {
                if (File.Exists(Path.Combine(tempPath, $"chunk_{i:D6}")))
                    result.Add(i);
            }
            return result;
        }

        // ─────────────────────────────────────────
        // FINAL VOICE NOTE RECORD
        // ─────────────────────────────────────────
        public async Task<long> InsertVoiceNote(PatientVoiceNoteModel data)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            data.entereddate = DateTime.UtcNow;
            data.deleted = false;

            var id = await db.InsertAsync(data);
            return id;
        }

        public async Task<PatientVoiceNoteModel?> GetById(long voiceNoteId, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT * FROM patient_voice_notes
                            WHERE voice_note_id = @voiceNoteId
                            AND tenant_code = @tenant_code
                            AND deleted = false";

            return await db.QueryFirstOrDefaultAsync<PatientVoiceNoteModel>(
                sql, new { voiceNoteId, tenant_code });
        }

        public async Task<List<PatientVoiceNoteModel>> GetByCust(int custid, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT * FROM patient_voice_notes
                            WHERE custid = @custid
                            AND tenant_code = @tenant_code
                            AND deleted = false
                            ORDER BY entereddate DESC";

            var res = await db.QueryAsync<PatientVoiceNoteModel>(sql, new { custid, tenant_code });
            return res.ToList();
        }

        public async Task<List<PatientVoiceNoteModel>> GetByDoctor(int dcode, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"SELECT * FROM patient_voice_notes
                            WHERE dcode = @dcode
                            AND tenant_code = @tenant_code
                            AND deleted = false
                            ORDER BY entereddate DESC";

            var res = await db.QueryAsync<PatientVoiceNoteModel>(sql, new { dcode, tenant_code });
            return res.ToList();
        }

        public async Task<string> Delete(long voiceNoteId, string tenant_code)
        {
            using IDbConnection db = new NpgsqlConnection(db_conn);

            string sql = @"UPDATE patient_voice_notes
                            SET deleted = true
                            WHERE voice_note_id = @voiceNoteId
                            AND tenant_code = @tenant_code";

            var rows = await db.ExecuteAsync(sql, new { voiceNoteId, tenant_code });
            return rows > 0 ? "Success" : "No data found";
        }
    }
}