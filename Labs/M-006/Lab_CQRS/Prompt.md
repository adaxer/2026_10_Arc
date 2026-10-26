# Prompt zur Erzeugung des CQRS-Labs

Du arbeitest in einem vorhandenen ASP.NET-Core-Web-API-Projekt für die Übung **Bank Account** aus einem Kapitel über Event Sourcing. Das Projekt enthält bereits das Domain Model `BankAccount`, einen In-Memory-`EventStore`, Domain Events und einen leeren oder teilweise implementierten `BankAccountController`.

Erzeuge daraus ein vollständiges, übersichtliches CQRS-Lab mit MediatR sowie eine deutschsprachige Übungsanleitung. Prüfe zuerst die vorhandenen Dateien und passe Namespaces, vorhandene Typen und das Target Framework an das Projekt an. Erhalte die bestehende Event-Sourcing-Implementierung und ändere sie nur, wenn dies für die CQRS-Integration zwingend erforderlich ist.

## Architekturvorgaben

- Commands verändern den Zustand des Bankkontos.
- Queries lesen den Kontostand oder die Kontohistorie.
- Controller übersetzen HTTP-Daten in vollständige Commands und Queries.
- Handler dürfen nicht von ASP.NET Core, `HttpContext`, Route Values oder `IHttpContextAccessor` abhängen.
- Die Konto-ID wird vom Controller aus der Route gelesen und als Bestandteil des Commands beziehungsweise der Query übergeben.
- Der `EventStore` bleibt ein In-Memory-Store und wird als Singleton registriert.
- Der Controller enthält keine Geschäftslogik.

## MediatR einrichten

1. Füge das NuGet-Package `MediatR` hinzu.
2. Registriere MediatR in `Program.cs`.
3. MediatR soll die aktuelle Assembly nach Handlern durchsuchen:

```csharp
builder.Services.AddMediatR(configuration =>
    configuration.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly()));
```

4. Registriere den vorhandenen `EventStore`:

```csharp
builder.Services.AddSingleton<EventStore>();
```

5. Registriere keinen `IHttpContextAccessor`.

## CQRS-Typen

Lege die CQRS-Typen im Unterordner `CQRS` an. Verwende den Namespace des vorhandenen Projekts.

Definiere folgende Commands:

```csharp
public sealed record OpenAccountCommand(decimal InitialBalance) : IRequest<Guid>;
public sealed record CloseAccountCommand(Guid AccountId) : IRequest;
public sealed record DepositCommand(Guid AccountId, decimal Amount) : IRequest;
public sealed record WithdrawCommand(Guid AccountId, decimal Amount) : IRequest;
```

Definiere folgende Queries:

```csharp
public sealed record GetBalanceQuery(Guid AccountId) : IRequest<decimal>;
public sealed record GetHistoryQuery(Guid AccountId) : IRequest<AccountEvent[]>;
```

Definiere für die lesende Seite dieses Read Model:

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

## Command-Handler

Erstelle einen `BankAccountCommandHandler`, der folgende Schnittstellen implementiert:

```csharp
IRequestHandler<OpenAccountCommand, Guid>
IRequestHandler<DepositCommand>
IRequestHandler<WithdrawCommand>
IRequestHandler<CloseAccountCommand>
```

Der Handler erhält den `EventStore` per Dependency Injection.

- `OpenAccountCommand`: Neue GUID erzeugen, ein `BankAccount` anlegen, `Open` aufrufen und die GUID zurückgeben.
- `DepositCommand`: Konto mit `request.AccountId` laden und `Deposit` aufrufen.
- `WithdrawCommand`: Konto mit `request.AccountId` laden und `Withdraw` aufrufen.
- `CloseAccountCommand`: Konto mit `request.AccountId` laden und `Close` aufrufen.
- Wenn ein Konto nicht existiert, wirf eine `KeyNotFoundException` mit einer verständlichen Meldung.

## Query-Handler

Erstelle einen gemeinsamen `BankAccountQueryHandler`, der beide Query-Schnittstellen implementiert:

```csharp
IRequestHandler<GetBalanceQuery, decimal>
IRequestHandler<GetHistoryQuery, AccountEvent[]>
```

Der Handler erhält ebenfalls den `EventStore` per Dependency Injection.

- `GetBalanceQuery`: Konto über `request.AccountId` laden und `GetBalance()` zurückgeben.
- `GetHistoryQuery`: Die Domain Events des Kontos in ein `AccountEvent[]` projizieren.
- Bilde `AccountOpenedEvent`, `MoneyDepositedEvent`, `MoneyWithdrawnEvent` und `AccountClosedEvent` mit einer Switch Expression auf den passenden `AccountEventType` ab.
- Verwende für `AccountClosedEvent` den Betrag `0m`.
- Wirf bei unbekannten Event-Typen eine aussagekräftige Exception.

## Controller

Vervollständige den vorhandenen `BankAccountController`. Injiziere `IMediator` und implementiere genau diese Routen:

| Methode | Route | Verhalten |
|---|---|---|
| POST | `/BankAccount/open` | `OpenAccountCommand` senden und die neue GUID mit `Ok` zurückgeben |
| POST | `/BankAccount/{accountId:guid}/deposit` | `DepositCommand` senden und `NoContent` zurückgeben |
| POST | `/BankAccount/{accountId:guid}/withdraw` | `WithdrawCommand` senden und `NoContent` zurückgeben |
| GET | `/BankAccount/{accountId:guid}/balance` | `GetBalanceQuery` senden und das Ergebnis mit `Ok` zurückgeben |
| GET | `/BankAccount/{accountId:guid}/history` | `GetHistoryQuery` senden und das Ergebnis mit `Ok` zurückgeben |
| POST | `/BankAccount/{accountId:guid}/close` | `CloseAccountCommand` senden und `NoContent` zurückgeben |

Verwende für Ein- und Auszahlung ein HTTP-Transportmodell:

```csharp
public sealed record AmountRequest(decimal Amount);
```

Der Controller liest `accountId` aus der Route und `Amount` aus dem Request-Body und erzeugt daraus den jeweiligen Command. Übergib den `CancellationToken` an jeden Aufruf von `IMediator.Send`.

## HTTP-Testdatei

Passe die vorhandene Datei `Lab_CQRS.http` an. Verwende Variablen für Host und Konto-ID:

```http
@host = http://localhost:5143
@accountId = 00000000-0000-0000-0000-000000000000
```

Füge ausführbare Requests für alle sechs Controller-Routen hinzu:

1. Konto mit `initialBalance: 1000.00` öffnen
2. `250.00` einzahlen
3. `100.00` abheben
4. Kontostand abfragen
5. History abfragen
6. Konto schließen

Weise in einem Kommentar darauf hin, dass die vom Open-Request zurückgegebene GUID in die Variable `accountId` kopiert werden muss.

## Übungsanleitung

Erstelle oder aktualisiere die Datei `lab.md` mit der Überschrift:

```markdown
# Tutorial für CQRS
```

Die Anleitung soll auf Deutsch verfasst sein und den Lernenden schrittweise durch folgende Aufgaben führen:

1. MediatR installieren und mit der aktuellen Assembly registrieren.
2. Den `EventStore` als Singleton registrieren.
3. Den Ordner `CQRS` anlegen.
4. Commands und Queries definieren.
5. Das `AccountEvent`-Read-Model definieren.
6. Den gemeinsamen Command-Handler implementieren.
7. Den gemeinsamen Query-Handler implementieren.
8. `IMediator` in den Controller injizieren und alle Endpoints ergänzen.
9. Die API mit `Lab_CQRS.http` testen.

Gib als vollständige Beispiele mindestens den `OpenAccountCommand`, die Verarbeitung des `DepositCommand`, den Open-Endpoint und den Deposit-Endpoint vor. Lass bei den übrigen Teilen sinnvolle Arbeitsaufgaben bestehen.

Füge hilfreiche Hinweise ein zu:

- dem Unterschied zwischen Commands und Queries,
- der Konto-ID als Bestandteil der Nachricht,
- der Unabhängigkeit der Handler von HTTP,
- dem Mapping von Domain Events auf das Read Model,
- `Select(...).ToArray()`,
- Route Constraints mit `{accountId:guid}`,
- der Weitergabe des `CancellationToken`,
- der Singleton-Lebensdauer des In-Memory-`EventStore`,
- typischen Ursachen, wenn MediatR keinen Handler findet,
- der manuellen Übernahme der erzeugten Konto-ID in die HTTP-Datei.

Die Anleitung darf keinen Zugriff auf `HttpContext`, `RouteValues` oder `IHttpContextAccessor` als Lösung empfehlen.

## Prüfung

Baue das Projekt und behebe alle durch die Änderungen verursachten Compilerfehler. Teste danach nach Möglichkeit den vollständigen HTTP-Ablauf.

Bei Startguthaben `1000`, Einzahlung `250` und Abhebung `100` muss der Kontostand `1150` betragen. Vor dem Schließen muss die History `Opened`, `Deposited` und `Withdrawn` enthalten. Nach dem Schließen muss zusätzlich ein `Closed`-Event vorhanden sein.

Ändere keine fachlich unabhängigen Dateien und überschreibe keine bereits vorhandenen, nicht zu dieser Aufgabe gehörenden Änderungen.
