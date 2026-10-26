# Tutorial für CQRS

In diesem Tutorial erweiterst du die Übung **Bank Account** aus dem Kapitel **Event Sourcing** um CQRS. Schreibende Aktionen werden als **Commands**, lesende Zugriffe als **Queries** modelliert. Der Web-API-Controller übersetzt HTTP-Anfragen in diese Nachrichten und sendet sie über MediatR an die zuständigen Handler.

Am Ende unterstützt die API folgende Anwendungsfälle:

- Konto eröffnen
- Geld einzahlen
- Geld abheben
- Kontostand abfragen
- Kontohistorie abfragen
- Konto schließen

## 1. MediatR installieren und registrieren

Füge dem Projekt `Lab_CQRS` das NuGet-Package **MediatR** hinzu:

```powershell
dotnet add package MediatR
```

Registriere MediatR anschließend in `Program.cs`. MediatR soll die aktuelle Assembly nach Handlern durchsuchen:

```csharp
using System.Reflection;

builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
```

Der vorhandene `EventStore` speichert seine Daten im Arbeitsspeicher. Damit alle HTTP-Anfragen dieselbe Instanz und damit dieselben Kontodaten verwenden, registrierst du ihn als Singleton:

```csharp
builder.Services.AddSingleton<EventStore>();
```

Lege für Commands, Queries, Handler und Read Models im Projekt einen Ordner `CQRS` an.

> **Architekturhinweis:** Registriere keinen `IHttpContextAccessor` für die Handler. Ein Handler soll weder den HTTP-Kontext noch Route Values kennen. Der Controller liest die Daten aus Route und Body und erzeugt daraus einen vollständigen Command oder eine vollständige Query. So bleibt die CQRS-Schicht unabhängig von ASP.NET Core und kann leichter getestet oder von einem anderen Frontend verwendet werden.

## 2. Commands und Queries definieren

Commands verändern den Zustand des Kontos. Queries lesen Daten, ohne den Zustand zu verändern.

Erstelle zunächst den `OpenAccountCommand`. Beim erfolgreichen Öffnen erzeugt der Handler eine neue Konto-ID und gibt sie zurück. Deshalb implementiert der Command `IRequest<Guid>`:

```csharp
using MediatR;

namespace Lab_CQRS;

public sealed record OpenAccountCommand(decimal InitialBalance) : IRequest<Guid>;
```

Definiere anschließend die übrigen Nachrichten analog:

| Nachricht | Definition |
|---|---|
| `CloseAccountCommand` | `IRequest` mit `Guid AccountId` |
| `DepositCommand` | `IRequest` mit `Guid AccountId` und `decimal Amount` |
| `WithdrawCommand` | `IRequest` mit `Guid AccountId` und `decimal Amount` |
| `GetBalanceQuery` | `IRequest<decimal>` mit `Guid AccountId` |
| `GetHistoryQuery` | `IRequest<AccountEvent[]>` mit `Guid AccountId` |

> **Hint:** Die Konto-ID gehört in die Nachricht, obwohl sie bei HTTP-Aufrufen aus der Route kommt. Dadurch enthält jede Nachricht alle Informationen, die ihr Handler zur Ausführung benötigt.

> **Hint:** Verwende für zustandsändernde Nachrichten Verben wie `Open`, `Deposit`, `Withdraw` und `Close`. Lesende Nachrichten lassen sich mit Namen wie `GetBalanceQuery` und `GetHistoryQuery` klar erkennen.

## 3. Read Model für die Kontohistorie anlegen

Die HTTP-Antwort für die History soll nicht direkt aus den internen Domain-Event-Typen bestehen. Lege deshalb ein eigenes Read Model an:

```csharp
public sealed record AccountEvent(
    AccountEventType EventType,
    decimal Amount,
    DateTime Timestamp);

public enum AccountEventType
{
    Opened,
    Closed,
    Deposited,
    Withdrawn
}
```

Der Query-Handler bildet später jedes Domain Event auf einen `AccountEvent` ab. Für das Schließen eines Kontos gibt es keinen Betrag; verwende dafür in diesem Lab `0m`.

> **Hint:** Das Read Model gehört zur lesenden Seite von CQRS. Es darf gezielt für die Anforderungen der API gestaltet werden und muss nicht dieselbe Struktur wie das Domain Model besitzen.

## 4. Command-Handler implementieren

Erstelle einen `BankAccountCommandHandler`, der die vier Command-Handler-Schnittstellen implementiert:

```csharp
public sealed class BankAccountCommandHandler :
    IRequestHandler<OpenAccountCommand, Guid>,
    IRequestHandler<DepositCommand>,
    IRequestHandler<WithdrawCommand>,
    IRequestHandler<CloseAccountCommand>
{
    // Implementierung
}
```

Injiziere den `EventStore` über den Konstruktor. Der Handler für das Öffnen eines Kontos soll:

1. eine neue `Guid` erzeugen,
2. ein `BankAccount` mit dem `EventStore` und dieser ID anlegen,
3. `Open` mit dem Startguthaben aufrufen und
4. die neue Konto-ID zurückgeben.

Als Beispiel dient die Implementierung für eine Einzahlung:

```csharp
public Task Handle(
    DepositCommand request,
    CancellationToken cancellationToken)
{
    var account = _store.GetAccountView(request.AccountId)
        ?? throw new KeyNotFoundException("Konto nicht gefunden.");

    account.Deposit(request.Amount);
    return Task.CompletedTask;
}
```

Implementiere `WithdrawCommand` und `CloseAccountCommand` nach demselben Muster.

> **Hint:** Greife ausschließlich über `request.AccountId` auf die Konto-ID zu. Der Handler benötigt dadurch keine Kenntnis darüber, ob der Command von einem Controller, einer Konsolenanwendung oder einem Message Consumer gesendet wurde.

> **Hint:** Das Laden eines Kontos und die Behandlung eines nicht vorhandenen Kontos wiederholen sich. Du kannst dafür eine private Hilfsmethode im Handler verwenden.

## 5. Query-Handler implementieren

Erstelle einen `BankAccountQueryHandler`, der beide Queries behandelt:

```csharp
public sealed class BankAccountQueryHandler :
    IRequestHandler<GetBalanceQuery, decimal>,
    IRequestHandler<GetHistoryQuery, AccountEvent[]>
{
    // Implementierung
}
```

Injiziere auch hier den `EventStore`.

Der Handler für `GetBalanceQuery` soll das Konto über `request.AccountId` laden und den Rückgabewert von `account.GetBalance()` liefern.

Der Handler für `GetHistoryQuery` soll:

1. das Konto laden,
2. über dessen gespeicherte Domain Events iterieren,
3. jedes Event auf einen `AccountEvent` abbilden und
4. das Ergebnis als Array zurückgeben.

Für die Abbildung eignet sich eine Switch Expression:

```csharp
private static AccountEvent ToAccountEvent(Event @event)
    => @event switch
    {
        AccountOpenedEvent opened => new AccountEvent(
            AccountEventType.Opened,
            opened.InitialBalance,
            opened.Timestamp),

        // MoneyDepositedEvent, MoneyWithdrawnEvent und
        // AccountClosedEvent ergänzen

        _ => throw new ArgumentOutOfRangeException(nameof(@event))
    };
```

> **Hint:** Bei einem `MoneyWithdrawnEvent` kannst du den tatsächlichen Betrag im Feld `Amount` belassen. Dass es sich um eine Abbuchung handelt, ist bereits durch `AccountEventType.Withdrawn` eindeutig.

> **Hint:** Verwende `Select(ToAccountEvent).ToArray()`, um die Domain Events in das Read Model zu projizieren.

## 6. Controller mit MediatR verbinden

Injiziere `IMediator` in den vorhandenen `BankAccountController`. Der Controller enthält keine Geschäftslogik. Er übernimmt nur folgende Aufgaben:

1. Werte aus Route und Request-Body entgegennehmen,
2. daraus einen Command oder eine Query erzeugen,
3. die Nachricht mit `_mediator.Send(...)` versenden und
4. das Ergebnis in eine passende HTTP-Antwort übersetzen.

Der Endpoint zum Öffnen eines Kontos ist vollständig vorgegeben:

```csharp
[HttpPost("open")]
public async Task<ActionResult<Guid>> OpenAccount(
    [FromBody] OpenAccountCommand command,
    CancellationToken cancellationToken)
{
    var accountId = await _mediator.Send(command, cancellationToken);
    return Ok(accountId);
}
```

Für Ein- und Auszahlungen enthält der Request-Body nur den Betrag. Definiere dafür ein Transportmodell:

```csharp
public sealed record AmountRequest(decimal Amount);
```

Der Controller kombiniert anschließend die Konto-ID aus der Route mit dem Betrag aus dem Body:

```csharp
[HttpPost("{accountId:guid}/deposit")]
public async Task<IActionResult> Deposit(
    Guid accountId,
    [FromBody] AmountRequest request,
    CancellationToken cancellationToken)
{
    await _mediator.Send(
        new DepositCommand(accountId, request.Amount),
        cancellationToken);

    return NoContent();
}
```

Implementiere die restlichen Endpoints nach diesem Muster:

| HTTP-Methode und Route | An MediatR gesendete Nachricht | Antwort |
|---|---|---|
| `POST /BankAccount/{accountId}/withdraw` | `WithdrawCommand` | `NoContent()` |
| `GET /BankAccount/{accountId}/balance` | `GetBalanceQuery` | `Ok(balance)` |
| `GET /BankAccount/{accountId}/history` | `GetHistoryQuery` | `Ok(history)` |
| `POST /BankAccount/{accountId}/close` | `CloseAccountCommand` | `NoContent()` |

> **Hint:** Übergib den `CancellationToken` des Controllers an jeden Aufruf von `_mediator.Send(...)`.

> **Hint:** Für Queries und den Close-Command gibt es keinen Request-Body. Erzeuge die jeweilige Nachricht direkt mit der `accountId` aus der Route, zum Beispiel `new GetBalanceQuery(accountId)`.

> **Hint:** Der Route Constraint `{accountId:guid}` verhindert bereits, dass ungültige GUIDs an die Action weitergereicht werden.

## 7. Web-API testen

Starte die Web-API und öffne `Lab_CQRS.http`. Die Datei enthält passende Aufrufe für alle Controller-Routen und verwendet Variablen für die Host-Adresse und die Konto-ID.

Führe zuerst den Request zum Öffnen eines Kontos aus. Kopiere die zurückgegebene GUID in die Variable `accountId` am Anfang der HTTP-Datei. Führe anschließend die Requests in dieser Reihenfolge aus:

1. Konto mit einem Startguthaben von `1000` eröffnen
2. `250` einzahlen
3. `100` abheben
4. Kontostand abfragen
5. History abfragen
6. Konto schließen

Nach diesen Ein- und Auszahlungen muss die Balance `1150` betragen. Vor dem Schließen enthält die History die Events `Opened`, `Deposited` und `Withdrawn`. Führe den History-Request nach dem Schließen erneut aus; dann muss zusätzlich ein `Closed`-Eintrag vorhanden sein.

Was passiert bei ungültigen Eingaben? Probiere es aus, indem Du
- auf einen geschlossenen BankAccount einzahlst
- eine query mit einer ungültigen accountId ausführst
- andere Fehlerfälle ausprobierst

> **Hint:** Der `EventStore` arbeitet nur im Arbeitsspeicher. Nach einem Neustart der Web-API sind zuvor erzeugte Konten nicht mehr vorhanden. Öffne dann ein neues Konto und aktualisiere die Variable `accountId`.

> **Hint:** Wenn MediatR keinen Handler findet, prüfe zuerst, ob der Handler `public` ist, die richtige `IRequestHandler`-Schnittstelle implementiert und MediatR mit der aktuellen Assembly registriert wurde.

> **Hint:** Wenn ein Request mit „Konto nicht gefunden“ fehlschlägt, kontrolliere, ob die `accountId` in `Lab_CQRS.http` der GUID aus dem Open-Request entspricht und ob die API seitdem nicht neu gestartet wurde.
