/// <summary>马骨架的固定关节语义与父链；不保存实例变换、动作时钟或可变蒙皮。</summary>
public static class HorseSkeletonDefinition
{
    public const int BoneCount=32,FirstLegBone=16,SegmentsPerLeg=4;
    private static readonly string[] Names={"胸廓","骨盆","远肩胛","近肩胛","下颈","上颈","头","远耳","近耳","尾根","尾中","尾梢","上鬃","下鬃","鞍座","行囊",
        "远后大腿","远后胫骨","远后管骨","远后蹄","近后大腿","近后胫骨","近后管骨","近后蹄",
        "远前上臂","远前前臂","远前管骨","远前蹄","近前上臂","近前前臂","近前管骨","近前蹄"};
    private static readonly int[] Parents={-1,0,0,0,0,4,5,6,6,1,9,10,5,4,0,1,
        1,16,17,18,1,20,21,22,2,24,25,26,3,28,29,30};
    public static string Name(int bone) => Names[bone];
    public static int Parent(int bone) => Parents[bone];
}
