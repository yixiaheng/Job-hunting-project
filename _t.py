from pathlib import Path
p = Path(r"E:\unity\Job hunting project\Assets\Scripts\GamePlay\Battle\Common\BattleFlow.cs")
t = p.read_text(encoding="utf-8")
start = t.find("            var skill = action.skill;")
end = t.find("            var targets = selector.Select(")
print("start", start, "end", end)
print(repr(t[start:end]))
