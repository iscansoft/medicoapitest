using Dapper;
using System;
using System.Data;

namespace medico_backend.Services
{
    public static class AppTimeZoneHelper
    {
        public static readonly TimeZoneInfo LocalAppTimeZone = ResolveAppTimeZone();

        private static TimeZoneInfo ResolveAppTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("IST", TimeSpan.FromHours(5.5), "India Standard Time", "India Standard Time");
                }
            }
        }

        public static DateTime ToLocalTimeSafe(DateTime dt)
        {
            if (dt == default || dt == DateTime.MinValue || dt == DateTime.MaxValue)
                return dt;

            DateTime utcDt = dt.Kind == DateTimeKind.Utc
                ? dt
                : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

            return TimeZoneInfo.ConvertTimeFromUtc(utcDt, LocalAppTimeZone);
        }

        public static DateTime? ToLocalTimeSafe(DateTime? dt)
        {
            if (!dt.HasValue) return null;
            return ToLocalTimeSafe(dt.Value);
        }

        public static DateTime ToUtcFromLocal(DateTime localDt)
        {
            if (localDt == default || localDt == DateTime.MinValue || localDt == DateTime.MaxValue)
                return localDt;

            DateTime unspec = localDt.Kind == DateTimeKind.Unspecified
                ? localDt
                : DateTime.SpecifyKind(localDt, DateTimeKind.Unspecified);

            return TimeZoneInfo.ConvertTimeToUtc(unspec, LocalAppTimeZone);
        }

        public static DateTime? ToUtcFromLocal(DateTime? localDt)
        {
            if (!localDt.HasValue) return null;
            return ToUtcFromLocal(localDt.Value);
        }
    }

    // Ensures every DateTime written to Postgres 'timestamptz' columns is UTC.
    public class DateTimeUtcHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime value)
        {
            parameter.Value = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        public override DateTime Parse(object value)
        {
            var dt = (DateTime)value;
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
    }

    public class NullableDateTimeUtcHandler : SqlMapper.TypeHandler<DateTime?>
    {
        public override void SetValue(IDbDataParameter parameter, DateTime? value)
        {
            if (value is null)
            {
                parameter.Value = DBNull.Value;
                return;
            }

            parameter.Value = value.Value.Kind switch
            {
                DateTimeKind.Utc => value.Value,
                DateTimeKind.Local => value.Value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
            };
        }

        public override DateTime? Parse(object value)
        {
            if (value is null || value is DBNull) return null;
            var dt = (DateTime)value;
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
    }

    public class DateTimeOffsetUtcHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
        {
            parameter.Value = value.ToUniversalTime();
        }

        public override DateTimeOffset Parse(object value)
        {
            if (value is DateTimeOffset dto)
                return dto.ToUniversalTime();
            if (value is DateTime dt)
                return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            return DateTimeOffset.Parse(value.ToString()!).ToUniversalTime();
        }
    }

    public class NullableDateTimeOffsetUtcHandler : SqlMapper.TypeHandler<DateTimeOffset?>
    {
        public override void SetValue(IDbDataParameter parameter, DateTimeOffset? value)
        {
            if (value is null)
            {
                parameter.Value = DBNull.Value;
                return;
            }

            parameter.Value = value.Value.ToUniversalTime();
        }

        public override DateTimeOffset? Parse(object value)
        {
            if (value is null || value is DBNull) return null;
            if (value is DateTimeOffset dto)
                return dto.ToUniversalTime();
            if (value is DateTime dt)
                return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));
            return DateTimeOffset.Parse(value.ToString()!).ToUniversalTime();
        }
    }
}