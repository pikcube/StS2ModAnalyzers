using BaseLib.Patches.Content;
using MegaCrit.Sts2.Core.Entities.Cards;

namespace ModAnalyzers.Sample;

public class EnumTest
{
    [CustomEnum]
    public static CardKeyword MyKeyword;
    
    [CustomEnum]
    public CardKeyword Wrong;

    [CustomEnum] 
    public static PileType CustomPileType;
}