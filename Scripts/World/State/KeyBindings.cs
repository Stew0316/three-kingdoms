using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>统一键盘映射及独立用户设置；修改键盘映射时保留鼠标和手柄事件。</summary>
public sealed class KeyBindings
{
    public static readonly (string Action, string Title, Key Default)[] Entries = {
        (InputActions.MoveUp,"向上移动",Key.W),(InputActions.MoveLeft,"向左移动",Key.A),
        (InputActions.MoveDown,"向下移动",Key.S),(InputActions.MoveRight,"向右移动",Key.D),
        (InputActions.Skill1,"技能一",Key.Q),(InputActions.Skill2,"技能二",Key.E),
        (InputActions.Skill3,"技能三",Key.R),(InputActions.Skill4,"技能四",Key.F),
        ("item_1","物品一",Key.Key1),("item_2","物品二",Key.Key2),
        ("item_3","物品三",Key.Key3),("item_4","物品四",Key.Key4),
        (InputActions.Interact,"交互 / 点火",Key.G),(InputActions.Attack,"普通攻击",Key.J),
        (InputActions.Dodge,"闪避",Key.Space),(InputActions.Restart,"重开演武",Key.F5)
    };
    private readonly Dictionary<string,Key> _keys = new();
    private readonly bool _testing;
    private readonly string Path;
    public KeyBindings(bool testing, string path="user://key-bindings.cfg")
    {
        Path=path;
        _testing=testing;
        Reset(false);
        if (testing) return;
        var file=new ConfigFile();
        if(file.Load(Path)!=Error.Ok) return;
        // 整批校验后应用，允许交换两个默认键；损坏或冲突文件回退为默认映射。
        var loaded=new Dictionary<string,Key>();
        foreach(var entry in Entries)
        {
            var key=(Key)file.GetValue("keyboard",entry.Action,(long)entry.Default).AsInt64();
            if(!IsAllowed(key) || loaded.ContainsValue(key)) return;
            loaded[entry.Action]=key;
        }
        foreach(var pair in loaded) Apply(pair.Key,pair.Value);
    }
    public string Label(string action) => OS.GetKeycodeString(_keys[action]);
    public static bool IsAllowed(Key key) => Enum.IsDefined(key) && key is not (Key.None or Key.Escape or Key.M or Key.I or Key.X or Key.F3 or Key.F4 or Key.H or Key.Shift or Key.Ctrl or Key.Alt or Key.Meta);
    public bool TrySet(string action, Key key, out string message)
    {
        if(!_keys.ContainsKey(action) || !IsAllowed(key)) { message="此键保留给菜单、背包、坐骑或调试，请换一个键。"; return false; }
        string conflict=_keys.FirstOrDefault(p=>p.Key!=action && p.Value==key).Key;
        if(conflict!=null) { message="此键已用于“"+Entries.First(e=>e.Action==conflict).Title+"”。"; return false; }
        Apply(action,key);
        message=Save()==Error.Ok ? "已保存，立即生效。" : "已生效，但保存失败；重启后将恢复原设置。";
        return true;
    }
    public Error Reset(bool save=true)
    {
        foreach(var entry in Entries) Apply(entry.Action,entry.Default);
        return save ? Save() : Error.Ok;
    }
    private void Apply(string action,Key key)
    {
        if(!InputMap.HasAction(action)) InputMap.AddAction(action);
        foreach(var input in InputMap.ActionGetEvents(action))
            if(input is InputEventKey) InputMap.ActionEraseEvent(action,input);
        InputMap.ActionAddEvent(action,new InputEventKey { PhysicalKeycode=key });
        Input.ActionRelease(action);
        _keys[action]=key;
    }
    private Error Save()
    {
        if(_testing) return Error.Ok;
        var file=new ConfigFile();
        foreach(var pair in _keys) file.SetValue("keyboard",pair.Key,(long)pair.Value);
        var error=file.Save(Path+".tmp");
        return error==Error.Ok ? DirAccess.RenameAbsolute(ProjectSettings.GlobalizePath(Path+".tmp"),ProjectSettings.GlobalizePath(Path)) : error;
    }
}
