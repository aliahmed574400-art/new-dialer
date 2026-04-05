using NewDialer.Application.Models;
using NewDialer.Domain.Entities;

namespace NewDialer.Application.Abstractions;

public interface ISubscriptionAccessEvaluator
{
    SubscriptionAccessDecision Evaluate(TenantSubscription subscription, DateTimeOffset nowUtc);
}
