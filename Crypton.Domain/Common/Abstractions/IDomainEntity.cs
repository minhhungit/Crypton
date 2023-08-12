﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using MediatR;

namespace Crypton.Domain.Common.Abstractions;

public interface IDomainEntity
{
    [NotMapped]
    [JsonIgnore]
    protected ICollection<INotification> ProtectedDomainEvents { get; set; }

    [NotMapped]
    [JsonIgnore]
    public IReadOnlyCollection<INotification> DomainEvents => this.ProtectedDomainEvents.ToList().AsReadOnly();

    public void AddDomainEvent(INotification domainEvent)
    {
        this.ProtectedDomainEvents.Add(domainEvent);
    }

    public void RemoveDomainEvent(INotification domainEvent)
    {
        this.ProtectedDomainEvents.Remove(domainEvent);
    }

    public void ClearDomainEvents()
    {
        this.ProtectedDomainEvents.Clear();
    }
}