using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace ModAnalyzers.Sample;

[Pool(typeof(IroncladCardPool))]
public abstract class PooledAbstract(
    int baseCost,
    CardType type,
    CardRarity rarity,
    TargetType target,
    bool showInCardLibrary = true,
    bool autoAdd = true)
    : CustomCardModel(baseCost, type, rarity, target, showInCardLibrary, autoAdd);
    
public class InheritedPool() : PooledAbstract(1, CardType.Attack, CardRarity.Ancient, TargetType.AllEnemies)
{
    
}