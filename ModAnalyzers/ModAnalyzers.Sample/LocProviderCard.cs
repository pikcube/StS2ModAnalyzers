using System.Collections.Generic;
using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace ModAnalyzers.Sample;

[Pool(typeof(ColorlessCardPool))]
public class LocProviderCardA() : CustomCardModel(1, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
{
    public override List<(string, string)>? Localization =>
    [
        ("title", "asdf"),
        ("description", "asdf")
    ];
}
[Pool(typeof(ColorlessCardPool))]
public class LocProviderCardB() : CustomCardModel(1, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
{
    public override List<(string, string)>? Localization {
        get
        {
            return new List<(string, string)>
            {
                ("title", "asdf"),
                ("description", "asdf")
            };
        }
    }
}
[Pool(typeof(ColorlessCardPool))]
public class LocProviderCardC() : CustomCardModel(1, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
{
    public override List<(string, string)>? Localization 
    {
        get => 
        [
            ("title", "asdf"),
            ("description", "asdf")
        ];
    }
}
[Pool(typeof(ColorlessCardPool))]
public class LocProviderCardD() : CustomCardModel(1, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
{
    public override List<(string, string)>? Localization => new CardLoc("asdf", "asdf");
}