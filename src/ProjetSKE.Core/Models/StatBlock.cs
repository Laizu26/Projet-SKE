namespace ProjetSKE.Core.Models;

public sealed class StatBlock
{
    public int MaxHp { get; set; }
    public int MaxMana { get; set; }
    public int Attack { get; set; }
    public int Defense { get; set; }
    public int Magic { get; set; }
    public int Speed { get; set; }

    public StatBlock()
    {
    }

    public StatBlock(int MaxHp = 0, int MaxMana = 0, int Attack = 0, int Defense = 0, int Magic = 0, int Speed = 0)
    {
        this.MaxHp = MaxHp;
        this.MaxMana = MaxMana;
        this.Attack = Attack;
        this.Defense = Defense;
        this.Magic = Magic;
        this.Speed = Speed;
    }

    public static StatBlock operator +(StatBlock a, StatBlock b) => new(
        a.MaxHp + b.MaxHp,
        a.MaxMana + b.MaxMana,
        a.Attack + b.Attack,
        a.Defense + b.Defense,
        a.Magic + b.Magic,
        a.Speed + b.Speed);

    public StatBlock Times(int factor) => new(
        MaxHp * factor,
        MaxMana * factor,
        Attack * factor,
        Defense * factor,
        Magic * factor,
        Speed * factor);

    /// <summary>Texte court des bonus non nuls, ex : "+3 ATQ +1 VIT".</summary>
    public string ToBonusString()
    {
        var parts = new List<string>();
        void Add(int value, string label)
        {
            if (value != 0) parts.Add($"{(value > 0 ? "+" : "")}{value} {label}");
        }
        Add(MaxHp, "PV");
        Add(MaxMana, "PM");
        Add(Attack, "ATQ");
        Add(Defense, "DEF");
        Add(Magic, "MAG");
        Add(Speed, "VIT");
        return string.Join(" ", parts);
    }
}
