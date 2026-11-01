using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Sqlite;

internal class Program
{
    private static async Task Main(string[] args)
    {
        var options = new DbContextOptionsBuilder<PersonDb>()
            .UseSqlite("Data Source=person.db")
            .Options;

        using var db = new PersonDb(options);
        List<Person> personen = new();
#if DEBUG
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        personen = await SeedAsync(db);
#endif
        personen = await db.Personen.ToListAsync();
        InitDump(personen);

        // Übung 1: Alle über 60
        foreach (var person in personen.Where(p => p.Alter >= 60))
        {
            Dump(person);
        }

        // ...
    }

    #region Helpers
    static int[] widths = [];

    static void InitDump(IEnumerable<Person> personen)
    {
        var list = personen.ToList();

        widths =
        [
            Math.Max(7, list.Max(p => p.Vorname.Length)),
        Math.Max(8, list.Max(p => p.Nachname.Length)),
        Math.Max(5, list.Max(p => p.Job.Titel.Length))
        ];
    }

    static void Dump(Person p)
    {
        Console.WriteLine(
            $"{p.ID,4}  " +
            $"{p.Vorname.PadRight(widths[0])}  " +
            $"{p.Nachname.PadRight(widths[1])}  " +
            $"{p.Geburtsdatum:dd.MM.yyyy}  " +
            $"{p.Alter,3}");

        Console.WriteLine(
            $"      Job: {p.Job.Titel.PadRight(widths[2])}  " +
            $"{p.Job.Gehalt,8:N0} €  " +
            $"{p.Job.Einstellungsdatum:dd.MM.yyyy}");

        Console.WriteLine(
            $"      Hobbies: {string.Join(", ", p.Hobbies)}\r\n");
    }

    private static async Task<List<Person>> SeedAsync(PersonDb db)
    {
        var personenJson = await File.ReadAllTextAsync("Personen.json");
        var personen = System.Text.Json.JsonSerializer.Deserialize<List<Person>>(personenJson)?? new List<Person>();
        personen.First().ID=1000; // Mit ID 0 kann EF Core die Personen nicht speichern
        if (personen != null && !db.Personen.Any())
        {
            db.Personen.AddRange(personen);
            await db.SaveChangesAsync();
        }
        return personen!;
    }
    #endregion
}

public record Person
{
    [Key]
    public int ID { get; set; }
    public string Vorname { get; set; } = string.Empty;
    public string Nachname { get; set; } = string.Empty;
    public DateTime Geburtsdatum { get; set; }
    public int Alter { get; set; }
    public Beruf Job { get; set; } = new();
    public List<string> Hobbies { get; set; } = [];
}

[ComplexType] // Damit wird Beruf als zusätzliche Spalten in der Person-Tabelle gespeichert
public record Beruf
{
    public string Titel { get; set; } = string.Empty;
    public int Gehalt { get; set; }
    public DateTime Einstellungsdatum { get; set; }
}

public class PersonDb : DbContext
{
    public DbSet<Person> Personen { get; set; }

    public PersonDb(DbContextOptions<PersonDb> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {

    }
}