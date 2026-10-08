"""Builds the fight viewer page: viewer.html with the replays and the hero and enemy data baked in.

From the repository root:
    dotnet build tools/BattleSim -c Release
    dotnet tools/BattleSim/bin/Release/net8.0/BattleSim.dll --replays /tmp/replays.json
    python3 -I tools/FightViewer/build.py /tmp/replays.json fight-viewer.html
"""
import json
import pathlib
import sys

root = pathlib.Path(__file__).resolve().parents[2]
data_dir = root / "Unity/Assets/Resources/BattleData"
replays = json.loads(pathlib.Path(sys.argv[1]).read_text())
out = pathlib.Path(sys.argv[2] if len(sys.argv) > 2 else "fight-viewer.html")

data = {
    "generated": replays["generated"],
    "fights": replays["fights"],
    "heroes": json.loads((data_dir / "heroes.json").read_text()),
    "enemies": json.loads((data_dir / "enemies.json").read_text()),
}
template = (pathlib.Path(__file__).parent / "viewer.html").read_text()
page = template.replace("/*__DATA__*/null", json.dumps(data, separators=(",", ":")).replace("</", "<\\/"))
out.write_text(page)
print(f"Wrote {out} ({len(page) // 1024} KB, {len(data['fights'])} fights)")
