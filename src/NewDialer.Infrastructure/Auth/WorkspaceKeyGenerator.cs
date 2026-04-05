using System.Text;
using NewDialer.Application.Abstractions;

namespace NewDialer.Infrastructure.Auth;

public sealed class WorkspaceKeyGenerator : IWorkspaceKeyGenerator
{
    public string Generate(string companyName)
    {
        var builder = new StringBuilder();
        var pendingDash = false;

        foreach (var character in companyName.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(character);
                pendingDash = false;
            }
            else
            {
                pendingDash = builder.Length > 0;
            }
        }

        return builder.Length == 0 ? "tenant" : builder.ToString();
    }
}
