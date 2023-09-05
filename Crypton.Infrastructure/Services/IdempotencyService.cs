﻿using Crypton.Infrastructure.Services.Interfaces;

namespace Crypton.Infrastructure.Services;

/// <inheritdoc />
public sealed class IdempotencyService : IIdempotencyService
{
    private static readonly TimeSpan InvalidationTime = TimeSpan.FromMinutes(10);
    private readonly HashSet<Guid> keys = new();

    public bool ContainsKey(Guid key) => keys.Contains(key);

    public void AddKey(Guid key)
    {
        keys.Add(key);

        Task.Delay(InvalidationTime)
            .ContinueWith(_ => keys.Remove(key));
    }
}