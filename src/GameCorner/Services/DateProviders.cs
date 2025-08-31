using System.Text.Json;
using Microsoft.JSInterop;

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