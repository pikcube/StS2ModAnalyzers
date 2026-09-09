using System.Collections.Generic;
using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace ModAnalyzers.Sample;

public abstract class Guy : CharacterModel, ICustomModel
{
    public override List<string> GetArchitectAttackVfx()
    {
        throw new System.NotImplementedException();
    }

    //public override Color NameColor { get; }
    public override CharacterGender Gender { get; }
    protected override CharacterModel? UnlocksAfterRunAs { get; }
    public override int StartingHp { get; }
    public override int StartingGold { get; }
    public override CardPoolModel CardPool { get; }
    public override RelicPoolModel RelicPool { get; }
    public override PotionPoolModel PotionPool { get; }
    public override IEnumerable<CardModel> StartingDeck { get; }
    public override IReadOnlyList<RelicModel> StartingRelics { get; }
    public override float AttackAnimDelay { get; }
    public override float CastAnimDelay { get; }
}