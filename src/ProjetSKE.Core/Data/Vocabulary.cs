using ProjetSKE.Core.Models;

namespace ProjetSKE.Core.Data;

/// <summary>
/// Tous les textes de l'interface qui peuvent être renommés depuis le mode développeur
/// (onglets, monnaie, statistiques, actions de combat...). Rien n'est figé : chaque texte a une valeur
/// par défaut, remplacée par celle du contenu (<see cref="WorldSettings.Texts"/>) si elle existe.
/// </summary>
public static class Vocabulary
{
    public sealed record Entry(string Key, string Default, string Group);

    public static readonly IReadOnlyList<Entry> Entries =
    [
        new("tab.camp", "Camp", "Onglets"),
        new("tab.map", "Carte", "Onglets"),
        new("tab.quests", "Quêtes", "Onglets"),
        new("tab.encyclopedia", "Encyclopédie", "Onglets"),
        new("tab.shop", "Shop", "Onglets"),
        new("tab.journal", "Journal", "Onglets"),
        new("tab.menu", "Menu", "Onglets"),

        new("title.camp", "Campement", "Titres d'écran"),
        new("title.map", "Carte", "Titres d'écran"),
        new("title.shop", "Boutique", "Titres d'écran"),
        new("title.story", "Histoire", "Titres d'écran"),
        new("title.narration", "Récit", "Titres d'écran"),

        new("money", "or", "Valeurs"),
        new("hp", "PV", "Valeurs"),
        new("mp", "PM", "Valeurs"),
        new("xp", "XP", "Valeurs"),
        new("level", "Niveau", "Valeurs"),
        new("stat.atk", "ATQ", "Valeurs"),
        new("stat.def", "DEF", "Valeurs"),
        new("stat.mag", "MAG", "Valeurs"),
        new("stat.spd", "VIT", "Valeurs"),

        new("loc.city", "Ville", "Types de lieu"),
        new("loc.wild", "Nature", "Types de lieu"),
        new("loc.dungeon", "Donjon", "Types de lieu"),

        new("enc.characters", "Personnages", "Encyclopédie"),
        new("enc.monsters", "Monstres", "Encyclopédie"),
        new("enc.locations", "Lieux", "Encyclopédie"),
        new("enc.weapons", "Armes", "Encyclopédie"),
        new("enc.relics", "Reliques", "Encyclopédie"),

        new("battle.title", "Combat", "Combat"),
        new("battle.attack", "Attaque", "Combat"),
        new("battle.defend", "Défense", "Combat"),
        new("battle.item", "Objet", "Combat"),
        new("battle.flee", "Fuite", "Combat"),
        new("battle.victory", "Victoire", "Combat"),
        new("battle.defeat", "Défaite", "Combat"),

        new("camp.people", "Persos", "Campement"),
        new("camp.manage", "Gestion", "Campement"),
        new("camp.resources", "Ressources", "Campement"),
        new("camp.places", "Lieux", "Campement"),
        new("camp.treasure", "Trésor du camp", "Campement"),
        new("camp.log", "Journal du camp", "Campement"),
        new("camp.rest", "Au repos", "Campement"),
        new("camp.available", "Disponibles", "Campement"),

        new("party", "Équipe", "Divers"),
        new("bag", "Sac", "Divers"),
        new("inn", "Auberge", "Divers"),
        new("speaker.ask", "Qui prend la parole ?", "Divers"),
    ];

    private static readonly Dictionary<string, string> Defaults = Entries.ToDictionary(e => e.Key, e => e.Default);

    /// <summary>Texte affiché pour une clé : valeur du contenu, sinon valeur par défaut, sinon la clé.</summary>
    public static string Get(GameContent content, string key) =>
        content.World.Texts.TryGetValue(key, out var custom) && !string.IsNullOrWhiteSpace(custom)
            ? custom
            : Defaults.GetValueOrDefault(key, key);
}
