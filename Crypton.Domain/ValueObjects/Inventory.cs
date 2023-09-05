﻿using Crypton.Domain.Common.Abstractions;
using Crypton.Domain.Entities;

namespace Crypton.Domain.ValueObjects;

public sealed class Inventory : List<Item>, IValueObject
{
    public bool HasItemWithId(Guid itemId)
    {
        return this.Any(x => x.Id == itemId);
    }

    public void Transfer(Inventory other, Guid itemId, User newOwner)
    {
        if (!HasItemWithId(itemId))
            throw new InvalidOperationException("Invalid item.");

        var item = this.Single(x => x.Id == itemId);

        Remove(item);
        other.Add(item);

        item.OwnerId = newOwner.Id;
        item.Owner = newOwner;
    }
}