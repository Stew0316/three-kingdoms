using Godot;
using System;

/// <summary>12 格为一个昼夜，前六格日光、后六格月光；单位为真实游戏秒，不补算离线时间。</summary>
public sealed class DayNightClock
{
    public const int SegmentCount=12;
    private readonly GameSettings _settings;
    public double Position { get; private set; }
    // 从会话起点累计完整周期，和当前十二格相位分开，调试调时不改变年月。
    public long CompletedCycles { get; private set; }
    private double _elapsedSegments;
    public long Year => Math.Max(1,_settings.CalendarStartYear)+CompletedCycles/12;
    public int Month => (int)(CompletedCycles%12)+1;
    public string CalendarText => $"公元{Year}年{Month}月";
    public int Segment => (int)Position;
    public float Fraction => (float)(Position-Segment);
    public bool IsDay => Segment<6;
    public DayNightClock(GameSettings settings) { _settings=settings; SetPosition(settings.DayNightStartSegment); }
    public void SetPosition(double value) => Position=double.IsFinite(value) ? ((value%12)+12)%12 : 0;
    public void Advance(double seconds)
    {
        if(!double.IsFinite(seconds) || seconds<=0) return;
        double duration=float.IsFinite(_settings.DayNightSecondsPerSegment) ? Math.Max(1,_settings.DayNightSecondsPerSegment) : 300;
        double segments=seconds/duration;
        // 长帧跨越多个周期时也完整记月；从初始格位起走满一圈才计月。
        double elapsed=_elapsedSegments+segments;
        double cycles=Math.Floor(elapsed/SegmentCount);
        if(cycles>long.MaxValue-CompletedCycles) return;
        CompletedCycles+=(long)cycles;
        _elapsedSegments=elapsed%SegmentCount;
        SetPosition(Position+segments%SegmentCount);
    }
    public Color Ambient
    {
        get
        {
            // 黎明、白昼、黄昏与月夜连续插值，格界不跳色；午夜仍保留通路与人物辨识度。
            var dawn=_settings.DawnAmbient; var day=_settings.DayAmbient;
            var dusk=_settings.DuskAmbient; var night=_settings.NightAmbient;
            double p=Position;
            if(p<1) return Blend(dawn,day,p);
            if(p<5) return day;
            if(p<6) return Blend(day,dusk,p-5);
            if(p<7) return Blend(dusk,night,p-6);
            if(p<11) return night;
            return Blend(night,dawn,p-11);
        }
    }
    private static Color Blend(Color a,Color b,double t) => a.Lerp(b,Mathf.SmoothStep(0,1,(float)t));
}
