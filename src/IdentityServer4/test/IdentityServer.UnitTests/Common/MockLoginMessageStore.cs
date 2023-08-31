using System.Collections.Generic;
using System.Threading.Tasks;
using IdentityServer4.Models;
using IdentityServer4.Stores;

namespace IdentityServer.UnitTests.Common;

public class MockLoginMessageStore : ILoginMessageStore
{
    public Dictionary<string, Message<LoginResponse>> Messages { get; set; } = new Dictionary<string, Message<LoginResponse>>();

    public Task DeleteAsync(string id)
    {
        if (id != null && Messages.ContainsKey(id))
        {
            Messages.Remove(id);
        }
        return Task.CompletedTask;
    }

    public Task<Message<LoginResponse>> ReadAsync(string id)
    {
        Message<LoginResponse> val = null;
        if (id != null)
        {
            Messages.TryGetValue(id, out val);
        }
        return Task.FromResult(val);
    }

    public Task WriteAsync(string id, Message<LoginResponse> message)
    {
        Messages[id] = message;
        return Task.CompletedTask;
    }
}