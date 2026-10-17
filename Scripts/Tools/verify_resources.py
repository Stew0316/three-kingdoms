"""检查 Godot 静态资源引用与脚本 UID；不加载游戏、不修改文件。"""
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
EXTENSIONS = {".cs", ".tres", ".tscn", ".godot", ".cfg", ".gdshader"}


def main():
    files = [ROOT / "project.godot"]
    for directory in ("Scripts", "Resources", "Scene", "Components", "Source"):
        files.extend(p for p in (ROOT / directory).rglob("*") if p.is_file() and p.suffix in EXTENSIONS)

    errors, checked, dynamic = [], 0, 0
    for path in files:
        text = path.read_text(encoding="utf-8-sig")
        for match in re.finditer(r'res://[^"\s<>]+', text):
            reference = match.group().removeprefix("res://")
            # 预览和测试存档是运行时输出；插值路径由运行时集成测试覆盖。
            if reference.startswith("Build/"):
                continue
            if "{" in reference or "." not in Path(reference).name:
                dynamic += 1
                continue
            checked += 1
            if not (ROOT / reference).exists():
                line = text[:match.start()].count("\n") + 1
                errors.append(f"{path.relative_to(ROOT)}:{line} → {reference}")

    uids = {}
    for path in (ROOT / "Scripts").rglob("*.uid"):
        uid = path.read_text(encoding="utf-8-sig").strip()
        if not Path(str(path)[:-4]).is_file():
            errors.append(f"孤立 UID：{path.relative_to(ROOT)}")
        if uid in uids:
            errors.append(f"重复 UID：{path.relative_to(ROOT)} 与 {uids[uid]}")
        uids[uid] = path.relative_to(ROOT)

    for error in errors:
        print(error)
    print(f"静态引用 {checked} 条；脚本 UID {len(uids)} 个；动态路径 {dynamic} 处交由运行时验证；错误 {len(errors)} 条。")
    return bool(errors)


if __name__ == "__main__":
    sys.exit(main())
