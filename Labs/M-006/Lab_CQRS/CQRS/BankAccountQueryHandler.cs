using MediatR;

namespace Lab_CQRS;

public sealed class BankAccountQueryHandler :
    IRequestHandler<GetBalanceQuery, decimal>,
    IRequestHandler<GetHistoryQuery, AccountEvent[]>
{
    private readonly EventStore _store;

    public BankAccountQueryHandler(EventStore store)
    {
        _store = store;
    }

    public Task<decimal> Handle(
        GetBalanceQuery request,
        CancellationToken cancellationToken)
    {
        var account = GetAccount(request.AccountId);
        return Task.FromResult(account.GetBalance());
    }

    public Task<AccountEvent[]> Handle(
        GetHistoryQuery request,
        CancellationToken cancellationToken)
    {
        var history = GetAccount(request.AccountId)
            .Select(ToAccountEvent)
            .ToArray();

        return Task.FromResult(history);
    }

    private BankAccount GetAccount(Guid accountId)
        => _store.GetAccountView(accountId)
            ?? throw new KeyNotFoundException("Konto nicht gefunden.");

    private static AccountEvent ToAccountEvent(Event @event)
        => @event switch
        {
            AccountOpenedEvent opened => new AccountEvent(
                AccountEventType.Opened,
                opened.InitialBalance,
                opened.Timestamp),
            AccountClosedEvent closed => new AccountEvent(
                AccountEventType.Closed,
                0m,
                closed.Timestamp),
            MoneyDepositedEvent deposited => new AccountEvent(
                AccountEventType.Deposited,
                deposited.Amount,
                deposited.Timestamp),
            MoneyWithdrawnEvent withdrawn => new AccountEvent(
                AccountEventType.Withdrawn,
                withdrawn.Amount,
                withdrawn.Timestamp),
            _ => throw new ArgumentOutOfRangeException(
                nameof(@event),
                @event,
                "Unbekannter Event-Typ.")
        };
}
