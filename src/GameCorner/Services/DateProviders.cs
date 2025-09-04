using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Globalization;
using System.Text.Json;

namespace GameCorner.Services;

public interface IDateProvider
{
    DateOnly Today { get; }
}

public class SystemDateProvider : IDateProvider
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);
}

public class FixedDateProvider : IDateProvider
{
    private readonly DateOnly _fixedDate;
    public FixedDateProvider(DateOnly fixedDate) => _fixedDate = fixedDate;
    public DateOnly Today => _fixedDate;
}

//public sealed class UrlDateProvider : IDateProvider
//{
//    private readonly NavigationManager _nav;

//    public UrlDateProvider(NavigationManager nav) => _nav = nav;

//    public DateOnly Today
//    {
//        get
//        {
//            var uri = new Uri(_nav.Uri);

//            // 1) Query: ?date=2025-09-04
//            var dateStr = GetQueryParam(uri, "date");
//            if (dateStr is not null && TryParseDate(dateStr, out var d))
//                return d;

//            // 2) Path: /hexicon/2025-09-04
//            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
//            if (segments.Length > 1 && TryParseDate(segments[^1], out var tail))
//                return tail;

//            // 3) Fallback: system date
//            return DateOnly.FromDateTime(DateTime.Now);
//        }
//    }

//    private static bool TryParseDate(string s, out DateOnly date) =>
//        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
//                               DateTimeStyles.None, out date);

//    private static string? GetQueryParam(Uri uri, string key)
//    {
//        var query = uri.Query.TrimStart('?');
//        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
//        {
//            var kv = part.Split('=', 2);
//            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
//                return Uri.UnescapeDataString(kv[1]);
//        }
//        return null;
//    }
//}


public sealed class UrlDateProvider : IDateProvider
{
    private readonly NavigationManager _nav;
    private readonly Func<DateOnly> _clock;        // e.g., () => DateOnly.FromDateTime(DateTime.Now)
    private readonly bool _allowFutureDates;

    public UrlDateProvider(
        NavigationManager nav,
        Func<DateOnly> clock,
        bool allowFutureDates = false)
    {
        _nav = nav;
        _clock = clock;
        _allowFutureDates = allowFutureDates;
    }

    public DateOnly Today
    {
        get
        {
            var today = _clock();

            // 1) Try query ?date=YYYY-MM-DD
            var uri = new Uri(_nav.Uri);
            var dateStr = GetQueryParam(uri, "date");
            if (dateStr is not null && TryParseDate(dateStr, out var fromQuery))
                return Clamp(fromQuery, today);

            // 2) Try trailing path /.../YYYY-MM-DD
            var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1 && TryParseDate(segments[^1], out var fromPath))
                return Clamp(fromPath, today);

            // 3) Fallback to "today"
            return today;
        }
    }

    private DateOnly Clamp(DateOnly requested, DateOnly today)
        => _allowFutureDates ? requested : (requested > today ? today : requested);

    private static bool TryParseDate(string s, out DateOnly date) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                               DateTimeStyles.None, out date);

    // tiny query parser to avoid extra package
    private static string? GetQueryParam(Uri uri, string key)
    {
        var query = uri.Query.TrimStart('?');
        foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && string.Equals(kv[0], key, StringComparison.OrdinalIgnoreCase))
                return Uri.UnescapeDataString(kv[1]);
        }
        return null;
    }
}