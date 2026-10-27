using System.ComponentModel.DataAnnotations;
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
        await db.Database.EnsureCreatedAsync();
        personen = await SeedAsync(db);
#endif
        //var personen = await db.Personen.ToListAsync();
        foreach (var person in personen.Take(20))
        {
            Console.WriteLine($"{person.Vorname} {person.Nachname} - {person.Job.Titel}");
        }
    }

    private static async Task<List<Person>> SeedAsync(PersonDb db)
    {
        var personenJson = await File.ReadAllTextAsync("Personen.json");
        var personen = System.Text.Json.JsonSerializer.Deserialize<List<Person>>(personenJson);
        return personen ?? new List<Person>();
        // personen.ForEach(p => p.Job.ID = p.ID);
        // if (personen != null && !db.Personen.Any())
        // {
        //     db.Personen.AddRange(personen);
        //     await db.SaveChangesAsync();
        // }
    }
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

public record Beruf
{
    [Key]
    public int ID { get; set; }
    public string Titel { get; set; } = string.Empty;
    public int Gehalt { get; set; }
    public DateTime Einstellungsdatum { get; set; }
}

public class PersonDb : DbContext
{
    public DbSet<Person> Personen { get; set; }
    public DbSet<Beruf> Berufe { get; set; }

    public PersonDb(DbContextOptions<PersonDb> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {

    }
}