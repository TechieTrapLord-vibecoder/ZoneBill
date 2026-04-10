using System;

namespace ZoneBill_Lloren.Helpers
{
    public static class PhilippineTime
    {
        private static readonly TimeZoneInfo TimeZone = ResolveTimeZone();

        public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);

        public static DateTime ToDateTime(DateTime value)
        {
            var utcValue = value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };

            return TimeZoneInfo.ConvertTimeFromUtc(utcValue, TimeZone);
        }

        public static DateTime? ToDateTime(DateTime? value)
        {
            return value.HasValue ? ToDateTime(value.Value) : null;
        }

        private static TimeZoneInfo ResolveTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                return TimeZoneInfo.FindSystemTimeZoneById("Asia/Manila");
            }
        }
    }
}
