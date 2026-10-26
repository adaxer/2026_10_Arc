using System;
using System.Collections.Generic;
using System.Linq;

namespace EventSourcingSolution;

// DOMAIN: immutable facts. Stream order is defined by the store, not timestamps.
public abstract record DomainEvent(Guid AggregateId, DateTime Timestamp);
public sealed record AccountOpenedEvent(Guid AggregateId, decimal InitialBalance)
    : DomainEvent(AggregateId, DateTime.UtcNow);
public sealed record MoneyDepositedEvent(Guid AggregateId, decimal Amount)
    : DomainEvent(AggregateId, DateTime.UtcNow);
public sealed record MoneyWithdrawnEvent(Guid AggregateId, decimal Amount)
    : DomainEvent(AggregateId, DateTime.UtcNow);
public sealed record AccountClosedEvent(Guid AggregateId)
    : DomainEvent(AggregateId, DateTime.UtcNow);

// DOMAIN: aggregate root. No I/O, console, or persistence dependency.
public sealed class BankAccount
{
    private readonly List<DomainEvent> _uncommitted = new();
    private bool _opened;
    private bool _closed;

    public Guid Id { get; }
    public decimal Balance { get; private set; }
    public bool IsClosed => _closed;
    public int Version { get; private set; } // number of committed events
    public IReadOnlyList<DomainEvent> UncommittedEvents => _uncommitted.AsReadOnly();

    private BankAccount(Guid id) => Id = id;

    public static BankAccount Open(Guid id, decimal initialBalance)
    {
        if (id == Guid.Empty) throw new ArgumentException("Invalid account ID", nameof(id));
        if (initialBalance < 0) throw new ArgumentOutOfRangeException(nameof(initialBalance));
        var account = new BankAccount(id);
        account.Raise(new AccountOpenedEvent(id, initialBalance));
        return account;
    }

    public static BankAccount Rehydrate(Guid id, IEnumerable<DomainEvent> history)
    {
        var account = new BankAccount(id);
        foreach (var e in history)
        {
            if (e.AggregateId != id) throw new InvalidOperationException("Wrong aggregate ID in event stream");
            account.Apply(e);
            account.Version++;
        }
        if (!account._opened) throw new InvalidOperationException("Account not found or invalid event stream");
        return account;
    }

    public void Deposit(decimal amount)
    {
        EnsureActive();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        checked { _ = Balance + amount; }
        Raise(new MoneyDepositedEvent(Id, amount));
    }

    public void Withdraw(decimal amount)
    {
        EnsureActive();
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (amount > Balance) throw new InvalidOperationException("Insufficient funds");
        Raise(new MoneyWithdrawnEvent(Id, amount));
    }

    public void Close()
    {
        EnsureActive();
        Raise(new AccountClosedEvent(Id));
    }

    private void EnsureActive()
    {
        if (!_opened) throw new InvalidOperationException("Account not opened");
        if (_closed) throw new InvalidOperationException("Account closed");
    }

    private void Raise(DomainEvent e)
    {
        Apply(e);
        _uncommitted.Add(e);
    }

    private void Apply(DomainEvent e)
    {
        switch (e)
        {
            case AccountOpenedEvent opened when !_opened:
                if (opened.InitialBalance < 0) throw new InvalidOperationException("Invalid opening balance");
                Balance = opened.InitialBalance;
                _opened = true;
                break;
            case MoneyDepositedEvent deposited when _opened && !_closed && deposited.Amount > 0:
                Balance = checked(Balance + deposited.Amount);
                break;
            case MoneyWithdrawnEvent withdrawn when _opened && !_closed && withdrawn.Amount > 0 && withdrawn.Amount <= Balance:
                Balance -= withdrawn.Amount;
                break;
            case AccountClosedEvent when _opened && !_closed:
                _closed = true;
                break;
            default:
                throw new InvalidOperationException($"Invalid event sequence: {e.GetType().Name}");
        }
    }

    // Called by repository only after successful append.
    public void MarkCommitted()
    {
        Version += _uncommitted.Count;
        _uncommitted.Clear();
    }
}

// APPLICATION PORT: storage abstraction.
public interface IEventStore
{
    IReadOnlyList<DomainEvent> Read(Guid aggregateId);
    void Append(Guid aggregateId, int expectedVersion, IReadOnlyList<DomainEvent> events);
}

public interface IBankAccountRepository
{
    BankAccount Load(Guid id);
    void Save(BankAccount account);
    IReadOnlyList<DomainEvent> History(Guid id);
}

// INFRASTRUCTURE: in-memory append-only event streams.
public sealed class InMemoryEventStore : IEventStore
{
    private readonly Dictionary<Guid, List<DomainEvent>> _streams = new();
    private readonly object _gate = new();

    public IReadOnlyList<DomainEvent> Read(Guid aggregateId)
    {
        lock (_gate)
            return _streams.TryGetValue(aggregateId, out var events)
                ? events.ToArray() : Array.Empty<DomainEvent>();
    }

    public void Append(Guid aggregateId, int expectedVersion, IReadOnlyList<DomainEvent> events)
    {
        lock (_gate)
        {
            var currentVersion = _streams.TryGetValue(aggregateId, out var stream) ? stream.Count : 0;
            if (currentVersion != expectedVersion)
                throw new InvalidOperationException("Concurrency conflict: event stream changed");
            if (events.Any(e => e.AggregateId != aggregateId))
                throw new InvalidOperationException("Event belongs to another aggregate");
            if (events.Count == 0) return;
            if (stream is null) _streams[aggregateId] = stream = new List<DomainEvent>();
            stream.AddRange(events);
        }
    }
}

// INFRASTRUCTURE: translates event streams to/from domain aggregates.
public sealed class BankAccountRepository(IEventStore store) : IBankAccountRepository
{
    public BankAccount Load(Guid id) => BankAccount.Rehydrate(id, store.Read(id));
    public IReadOnlyList<DomainEvent> History(Guid id) => store.Read(id);

    public void Save(BankAccount account)
    {
        var pending = account.UncommittedEvents.ToArray();
        if (pending.Length == 0) return;
        store.Append(account.Id, account.Version, pending);
        account.MarkCommitted();
    }
}

// APPLICATION: coordinates use cases and transaction boundaries.
public sealed class BankAccountService(IBankAccountRepository repository)
{
    public Guid Open(decimal initialBalance)
    {
        var id = Guid.NewGuid();
        repository.Save(BankAccount.Open(id, initialBalance));
        return id;
    }

    public void Deposit(Guid id, decimal amount)
    {
        var account = repository.Load(id);
        account.Deposit(amount);
        repository.Save(account);
    }

    public void Withdraw(Guid id, decimal amount)
    {
        var account = repository.Load(id);
        account.Withdraw(amount);
        repository.Save(account);
    }

    public void Close(Guid id)
    {
        var account = repository.Load(id);
        account.Close();
        repository.Save(account);
    }

    public (decimal Balance, IReadOnlyList<DomainEvent> History) Statement(Guid id)
    {
        var account = repository.Load(id);
        return (account.Balance, repository.History(id));
    }
}

// PRESENTATION: console only.
internal static class Program
{
    private static void Main()
    {
        IEventStore store = new InMemoryEventStore();
        IBankAccountRepository repository = new BankAccountRepository(store);
        var service = new BankAccountService(repository);
        Guid currentId = Guid.Empty;

        while (true)
        {
            Console.WriteLine("\n1. Konto eröffnen\n2. Einzahlen\n3. Abheben\n4. Kontoauszug\n5. Konto schließen\n0. Beenden");
            var choice = Console.ReadLine();
            if (choice == "0") return;
            try
            {
                switch (choice)
                {
                    case "1":
                        currentId = service.Open(ReadAmount("Startguthaben: "));
                        Console.WriteLine($"Konto eröffnet: {currentId}");
                        break;
                    case "2":
                        service.Deposit(RequireAccount(), ReadAmount("Einzahlungsbetrag: "));
                        PrintBalance();
                        break;
                    case "3":
                        service.Withdraw(RequireAccount(), ReadAmount("Abhebungsbetrag: "));
                        PrintBalance();
                        break;
                    case "4":
                        PrintBalance(includeHistory: true);
                        break;
                    case "5":
                        service.Close(RequireAccount());
                        Console.WriteLine("Konto geschlossen.");
                        break;
                    default:
                        Console.WriteLine("Unbekannte Eingabe.");
                        break;
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
            {
                Console.WriteLine($"Fehler: {ex.Message}");
            }
        }

        Guid RequireAccount() => currentId != Guid.Empty
            ? currentId : throw new InvalidOperationException("Bitte zuerst ein Konto eröffnen.");

        void PrintBalance(bool includeHistory = false)
        {
            var statement = service.Statement(RequireAccount());
            Console.WriteLine($"Aktueller Kontostand: {statement.Balance} €");
            if (!includeHistory) return;
            Console.WriteLine("Auszug:");
            foreach (var e in statement.History)
                Console.WriteLine(e switch
                {
                    AccountOpenedEvent x => $"{x.Timestamp:g} | +{x.InitialBalance,8} € Konto eröffnet",
                    MoneyDepositedEvent x => $"{x.Timestamp:g} | +{x.Amount,8} € Einzahlung",
                    MoneyWithdrawnEvent x => $"{x.Timestamp:g} | -{x.Amount,8} € Abhebung",
                    AccountClosedEvent x => $"{x.Timestamp:g} |             Konto geschlossen",
                    _ => "Unbekanntes Ereignis"
                });
        }
    }

    private static decimal ReadAmount(string prompt)
    {
        Console.Write(prompt);
        return decimal.Parse(Console.ReadLine() ?? "");
    }
}
