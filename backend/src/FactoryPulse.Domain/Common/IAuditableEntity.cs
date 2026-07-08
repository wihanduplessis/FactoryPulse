using System;
using System.Collections.Generic;
using System.Text;

namespace FactoryPulse.Domain.Common;

public interface IAuditableEntity
{
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; }
}
