using Godot;
using System;

/// <summary>12 格为一个昼夜，前六格日光、后六格月光；单位为真实游戏秒，不补算离线时间。</summary>
public sealed class DayNightClock
{
    public const int SegmentCount=12;
    private readonly GameSettings _settings;
    public double Position { get; private set; }
    public int Segment => (int)Position;
    public float Fraction => (float)(Position-Segment);
    public bool IsDay => Segment<6;
    public DayNightClock(GameSettings settings) { _settings=settings; SetPosition(settings.DayNightStartSegment); }
    public void SetPosition(double value) => Position=double.IsFinite(value) ? ((value%12)+12)%12 : 0;
    public void Advance(double seconds)
    {
        if(!double.IsFinite(seconds) || seconds<=0) return;
        double duration=float.IsFinite(_settings.DayNightSecondsPerSegment) ? Math.Max(1,_settings.DayNightSecondsPerSegment) : 300;
        SetPosition(Position+seconds/duration);
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
