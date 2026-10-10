using System;
using System.Collections.Generic;
using System.Text;

namespace CIS.Application.Abstractions.Authentication
{
    public interface ICurrentUser
    {
        string? UserId { get; }
    }
}
