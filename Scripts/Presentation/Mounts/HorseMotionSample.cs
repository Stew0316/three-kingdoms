using Godot;

/// <summary>宿主运动事实；Distance是碰撞后累计的实际路程，不能用输入速度积分代替。</summary>
public readonly record struct HorseMotionSample(Vector2 WorldPosition,float Distance,Vector2 Velocity,Vector2 Facing,bool Enabled=true);

/// <summary>玩家、NPC或演示驱动均可实现；马组件不查询角色类型、输入、任务或存档。</summary>
public interface IHorseMotionSource { HorseMotionSample HorseMotion { get; } }

/// <summary>绘制时读取最终手脚世界坐标，保证缰绳与马镫连接同帧的实际骑手纹理。</summary>
public interface IHorseRiderContacts
{
    bool HasRiderContacts { get; }
    Vector2 ReinHandWorld { get; }
    Vector2 StirrupFootWorld { get; }
}
