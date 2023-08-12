﻿using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using MediatR;

namespace Crypton.Domain.Common.Abstractions;

public abstract class BaseAuditableDomainEntity : IAuditableDomainEntity
{
    [NotMapped]
    [JsonIgnore]
    public ICollection<INotification> ProtectedDomainEvents { get; set; } = new List<INotification>();

    public DateTime Created { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? LastModified { get; set; }

    public string? LastModifiedBy { get; set; }
}