namespace NewDialer.Application.Abstractions;

public interface IWorkspaceKeyGenerator
{
    string Generate(string companyName);
}
