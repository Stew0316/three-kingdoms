using Godot;
using System;
using System.Collections.Generic;

/// <summary>首城对话与交互编排，成长结果仍交由 AdventureProgress 处理。</summary>
public partial class FirstCity
{
    public void Interact(CityInteractable target)
    {
        if(Dialogue.IsOpen || Navigation.IsOpen || _training || _smashRemaining>0 || _switching || target!=FindNearest()) return;
        Player.StopMoving();
        switch(target.ContentId)
        {
            case "snow-mountain": Dialogue.Open("雪山麓 · 山脚入口","草色在此褪尽，风雪沿岩隙涌出。\n山体陡峭，前路被冰雪封住，只能在山脚驻足远望。"); break;
            case "captain": CaptainDialogue(); break;
            case "inn": Dialogue.Open("阿宁 · 槐安驿站","远行先歇脚。驿站可免费恢复全部生命。现在只开通槐安这一处驿站，待游历更多城池再通驿路。",
                ("歇息 · 恢复全部生命",()=> { Player.Health.ResetHealth(); Commit(); Toast("驿站休息完毕 · 生命已恢复"); }),
                ("领取行旅火把",()=> { bool received=Progress.ReceiveTorch(); Commit(); Toast(received ? "火把已收入行囊 · 按 I 使用，可点亮旧驿道暗窟的壁烛" : "行囊中已有火把 · 按 I 使用"); })); break;
            case "smith": Dialogue.Open("石叔 · 铁匠铺",Progress.WeaponForged ? "兵刃已开锋。这份整备会随你带入演武场。" : $"替兵刃开锋需要 {Config.ForgeCost} 铜钱，永久增加 {Config.ForgePower:P0} 伤害。本次只可开锋一次。",
                (Progress.WeaponForged ? "查看已整备的兵刃" : $"开锋 · {Config.ForgeCost} 铜钱",()=>
                {
                    if(Progress.Forge(Config)) { Player.ProgressionDamageMultiplier=Progress.PowerMultiplier(Config); Commit(); Toast("开锋完成 · 伤害 +10%"); }
                    else Toast(Progress.WeaponForged ? "兵刃已经开锋" : "铜钱不足，先去完成演武任务或寻找旧驿道木匣");
                })); break;
            case "resident": Dialogue.Open("槐婆 · 槐下民居","城门旁那块巨石，寻常刀剑打不动。韩教头会教你崩击。石后是废弃的旧驿道，据说还留着一只木匣。\n东边出城是演武场；南边马厩的两匹马脚程可不一样。"); break;
            case "groom": Dialogue.Open("小满 · 马厩",$"枣红马性子稳，青骢马脚程快。走进马厩，靠近想骑的马按 {Session.Bindings.Label(InputActions.Interact)} 上马；按 X 下马，马匹会送回原栏。\n骑马可以在城里赶路，进演武场时先下马。",
                ($"枣红马 {Config.BrownHorseSpeed:0} / 青骢马 {Config.WhiteHorseSpeed:0} 像素每秒",()=>Toast($"进马厩靠近马匹，按 {Session.Bindings.Label(InputActions.Interact)} 选择坐骑"))); break;
            case "horse-brown": HorseDialogue(1); break;
            case "horse-white": HorseDialogue(2); break;
            case "gate": Dialogue.Open("东门 · 演武场",Progress.Trial==AdventureProgress.QuestStage.Available ? "城外演武场默认魏延、关羽、刘备三将同场。建议先去军营接任务，击败魏延后即可回城交付。" : "进入四人演武场，与魏延、关羽、刘备切磋。击败魏延即可按“返回槐安”回城交任务，也可继续挑战其余两将；失败可回驿站恢复。",
                ("出城 · 挑战魏延",EnterArena)); break;
            case "boulder":
                Dialogue.Open("城门边的巨石",Progress.CanSmash ? "裂隙清晰可见，教头传授的崩击正好能破开这块巨石。" : "巨石纹丝不动。你还没有“崩击”能力，去军营找韩教头接下演武任务。",
                    (Progress.CanSmash ? "施展崩击 · 破开巨石" : "试着击打",()=>
                    {
                        Dismount(); Player.Face(Vector2.Down); Player.TryAction(CombatAction.Basic);
                        if(Progress.CanSmash) _smashRemaining=.5f; else Toast("寻常攻击无法破坏巨石 · 需要崩击");
                    })); break;
            case "cache": if(Progress.ClaimCache()) { Map.CacheOpen=true; Map.QueueRedraw(); Commit(); Dialogue.Open("旧驿道木匣","你沿着巨石后的小路找到遗失的行囊。获得 80 铜钱，可带回铁匠铺整备兵刃。"); } break;
            case "cave":
                if(!Progress.BoulderBroken) { Toast("先用崩击打开旧驿道"); break; }
                Dialogue.Open("旧驿道 · 暗窟","洞内没有点燃的蜡烛。需要在行囊中使用火把，再靠近壁烛逐盏点燃；每盏照亮一片区域。洞口备有行旅火把。",
                    ("进入洞窟",()=> { Dismount(); _switching=true; if(Session.EnterCave(GetTree(),Player)!=Error.Ok) { _switching=false; Toast("洞窟加载失败，请重试"); } })); break;
        }
    }
    private void CaptainDialogue()
    {
        string task=Progress.Trial switch
        {
            AdventureProgress.QuestStage.Available=>"去东门外的演武场击败魏延，再回来见我。我先传你“崩击”，可破开东门边的巨石。凯旋后赠你《行军要诀》，增加伤害。",
            AdventureProgress.QuestStage.Accepted=>"任务：前往东门演武场击败魏延。崩击已经传你，城门巨石也可以先去试试。",
            AdventureProgress.QuestStage.Completed=>"打得好！你已击败魏延。收下这卷《行军要诀》和100铜钱，再准备下一段旅程。",
            _=>"《行军要诀》已收入行囊并研习，伤害永久 +10%。路还长，别忘了整备与锻炼。"
        };
        int n=Progress.TrainingCount;
        var choices=new List<(string,Action)>();
        if(Progress.Trial==AdventureProgress.QuestStage.Available) choices.Add(("接下演武任务 · 学会崩击",()=> { Progress.AcceptTrial(); Commit(); Toast("已接任务：击败魏延 · 已学会地形能力：崩击",5); }));
        if(Progress.Trial==AdventureProgress.QuestStage.Completed) choices.Add(("交付任务 · 领取兵书与100铜钱",()=> { if(Progress.ClaimTrial(Config)) { Player.ProgressionDamageMultiplier=Progress.PowerMultiplier(Config); Commit(); Toast("获得《行军要诀》 · 伤害 +10% · 铜钱 +100",6); } }));
        if(n<Config.TrainingHealth.Length) choices.Add(($"锻炼第{n+1}次 · 生命 +{Config.TrainingHealth[n]} / 伤害 +{Config.TrainingPower[n]:P0}",BeginTraining));
        else task+="\n本城三次锻炼已经完成，继续游历吧。";
        Dialogue.Open("韩正 · 军营教头",task,choices.ToArray());
    }
}
