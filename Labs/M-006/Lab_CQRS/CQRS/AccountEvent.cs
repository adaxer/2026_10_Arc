namespace Lab_CQRS;

public sealed record AccountEvent(AccountEventType EventType, decimal Amount, DateTime Timestamp);
public enum AccountEventType
{
    Opened,
    Closed,
    Deposited,
    Withdrawn
}
